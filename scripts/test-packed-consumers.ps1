#!/usr/bin/env pwsh
<#
.SYNOPSIS
	Proves Purview.ValueObjects works for real consumers - Native AOT, Blazor WebAssembly (trimmed and AOT) and Blazor
	Server - built from the .nupkg alone, alongside the Purview.ZodSharp packages it integrates with.

.DESCRIPTION
	Modelled on purview-dev/zodsharp's scripts/test-packed-consumers.ps1. Every in-repository build references the
	generator and runtime as ProjectReferences, which exercises none of what ships: the packed runtime, the merged
	analyzer under analyzers/, the buildTransitive imports, and the generated code compiled into somebody else's
	trimmed or AOT application. That gap is how Purview.SourceGeneratorFramework.dll leaked into application output
	(and broke Blazor WebAssembly AOT) while every gate stayed green, and how the generated JSON converters reached
	every trimmed consumer with IL2026/IL3050.

	This gate consumes the package exactly as a user does:

	1. Preflight: checks the build-time Purview.SourceGeneratorFramework and the Purview.ZodSharp packages the
	   consumers use can be restored, and fails early, with what to do, if they cannot.
	2. Packs every shipping package (the ids in .config/purview-build.json PackValidation.RequiredContent) to an
	   isolated feed, with an isolated NUGET_PACKAGES, so no previously extracted package of the same version can be
	   reused. Package validation against the API baseline runs as part of the pack. The feed must hold exactly the
	   expected packages, and no package may carry a compiler-host assembly outside analyzers/.
	3. Copies the consumers in scripts/packed-consumers/ outside the repository (the Blazor ones with the shared
	   files in scripts/packed-consumers/Shared), beside guard Directory.Build.props/.targets and
	   Directory.Packages.props files that stop any inheritance, with a nuget.config that resolves Purview.ValueObjects
	   from the isolated feed only:
	   (a) AotConsole - a Native AOT console over value objects, [ZodSchema] DTOs and System.Text.Json source
	       generation. Published with -warnaserror, then run; it exits non-zero on any mismatch.
	   (b) BlazorWasm - a Blazor WebAssembly app with a <ZodValidator> form and a <DataAnnotationsValidator /> form over
	       a model of value objects. Published trimmed with -warnaserror, then with RunAOTCompilation=true. Each publish
	       is served and loaded in a headless Chromium-family browser, where the page validates both forms and reports
	       the outcome in the DOM.
	   (c) BlazorServer - a Blazor Web App. Published and started: a statically rendered form is posted invalid and
	       valid, and an interactive-server page is prerendered and then run over a live circuit in the browser.
	   (d) Net8Console - a net8.0 console without ZodSharp, built and (when the runtime is installed) run: the runtime
	       package still serves every target framework it ships.
	4. Scans every consumer bin and publish folder with scripts/assert-no-compiler-host-assemblies.ps1.

	Gaps in a dependency (Purview.ZodSharp prerelease behaviour the consumers rely on) are reported as KNOWN ISSUES,
	not failures, so they stay visible without blocking this repository; -StrictKnownIssues makes them failures.

	The temporary workspace is deleted afterwards unless -KeepArtifacts is passed. Packing restores the shipping
	projects against the isolated cache, so they are restored again against the normal cache at the end.

.PARAMETER Configuration
	Configuration the packages are packed in. Defaults to Release: this gate checks what ships.

.PARAMETER RuntimeIdentifier
	RID for the Native AOT console. Native AOT cannot cross-compile between operating systems, so it defaults to this
	machine's RID (win-x64, linux-x64, osx-arm64, ...).

.PARAMETER DependencyFeed
	A NuGet feed (folder or URL) to restore Purview.SourceGeneratorFramework* and Purview.ZodSharp* from, in addition
	to nuget.org - for versions that are not on nuget.org yet. Used for those packages only. Defaults to
	$env:VALUEOBJECTS_DEPENDENCY_FEED, then $env:LOCAL_NUGET_FEED_PATH.

.PARAMETER WasmAot
	Auto (default) runs the WebAssembly AOT publish when the wasm-tools workload is installed and says so when it is
	skipped. Require fails when the workload is missing (CI installs it). Skip never runs it.

.PARAMETER Browser
	Auto (default) loads the published Blazor apps in a headless Chromium-family browser (Edge, Chrome or Chromium;
	PACKED_CONSUMER_BROWSER names one explicitly) when one is found, and says so when it is not. Require fails when
	none is found. Skip never runs a browser.

.PARAMETER PackOutput
	Also copy the packed .nupkg/.snupkg files here, so a later step can inspect exactly what this gate consumed.

.PARAMETER PreflightOnly
	Run only the dependency restore check, so CI can fail on it before installing toolchains.

.PARAMETER StrictKnownIssues
	Fail the gate when a known dependency issue reproduces.

.PARAMETER KeepArtifacts
	Leave the temporary workspace in place for inspection.

.EXAMPLE
	just smoke-packed-consumers

.EXAMPLE
	./scripts/test-packed-consumers.ps1 -RuntimeIdentifier linux-x64 -WasmAot Require -Browser Require -KeepArtifacts
#>
[CmdletBinding()]
param (
	[string] $Configuration = 'Release',
	[string] $RuntimeIdentifier = [System.Runtime.InteropServices.RuntimeInformation]::RuntimeIdentifier,
	[string] $DependencyFeed = ($env:VALUEOBJECTS_DEPENDENCY_FEED ? $env:VALUEOBJECTS_DEPENDENCY_FEED : $env:LOCAL_NUGET_FEED_PATH),
	[ValidateSet('Auto', 'Require', 'Skip')]
	[string] $WasmAot = 'Auto',
	[ValidateSet('Auto', 'Require', 'Skip')]
	[string] $Browser = 'Auto',
	[string] $PackOutput,
	[switch] $PreflightOnly,
	[switch] $StrictKnownIssues,
	[switch] $KeepArtifacts
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$leakScan = Join-Path $PSScriptRoot 'assert-no-compiler-host-assemblies.ps1'
$consumerTemplates = Join-Path $PSScriptRoot 'packed-consumers'

$version = (Get-Content (Join-Path $repositoryRoot 'package.json') -Raw | ConvertFrom-Json).version
$packageVersions = [xml] (Get-Content (Join-Path $repositoryRoot 'Directory.Packages.props') -Raw)
$sgfVersion = $packageVersions.SelectSingleNode('//PurviewSGFVersion')?.InnerText
$zodSharpVersion = $packageVersions.SelectSingleNode('//PurviewZodSharpVersion')?.InnerText
$sdk = (Get-Content (Join-Path $repositoryRoot 'global.json') -Raw | ConvertFrom-Json).sdk

if (-not $sgfVersion) { throw 'Could not read PurviewSGFVersion from Directory.Packages.props.' }
if (-not $zodSharpVersion) { throw 'Could not read PurviewZodSharpVersion from Directory.Packages.props.' }

# The shipping packages are exactly the ids pack validation knows about, so a new package is covered without editing
# this script. Each id maps to src/src/<id without "Purview."> (Purview.ValueObjects to src/src/ValueObjects).
$packValidation = (Get-Content (Join-Path $repositoryRoot '.config/purview-build.json') -Raw | ConvertFrom-Json).PackValidation
$sourceProjects = Get-ChildItem (Join-Path $repositoryRoot 'src/src') -Directory
$shipping = foreach ($id in $packValidation.RequiredContent.PSObject.Properties.Name) {
	$folder = $id -ireplace '^purview\.', ''
	$directory = $sourceProjects | Where-Object { $_.Name -ieq $folder }
	$project = $directory ? (Join-Path $directory.FullName "$($directory.Name).csproj") : $null
	if (-not $project -or -not (Test-Path $project)) {
		throw "No project for the shipping package '$id' (expected src/src/$folder/$folder.csproj)."
	}

	[pscustomobject]@{ Id = $id; Project = $project }
}

$workspace = Join-Path ([System.IO.Path]::GetTempPath()) "vo-packed-consumers-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
$feed = Join-Path $workspace 'feed'
$packages = Join-Path $workspace 'packages'
$logs = Join-Path $workspace 'logs'
$consumers = Join-Path $workspace 'consumers'
$output = Join-Path $workspace 'out'
$exe = $IsWindows ? '.exe' : ''

$timings = [System.Collections.Generic.List[object]]::new()
$scanned = [System.Collections.Generic.List[string]]::new()
$knownIssues = [System.Collections.Generic.List[string]]::new()
$previousNuGetPackages = $env:NUGET_PACKAGES
$repositoryRestoreTouched = $false

function Write-Failure([string] $message) {
	if ($env:GITHUB_ACTIONS) { Write-Host "::error::$message" } else { Write-Host "ERROR: $message" -ForegroundColor Red }
}

function Write-KnownIssue([string] $message) {
	$knownIssues.Add($message)
	if ($env:GITHUB_ACTIONS) { Write-Host "::warning::Known issue: $message" } else { Write-Host "KNOWN ISSUE: $message" -ForegroundColor Yellow }
}

function Invoke-Step([string] $name, [scriptblock] $body) {
	if ($env:GITHUB_ACTIONS) { Write-Host "::group::$name" } else { Write-Host "`n=== $name" -ForegroundColor Cyan }
	$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
	$status = 'passed'
	try {
		& $body
	}
	catch {
		$status = 'FAILED'
		throw
	}
	finally {
		$stopwatch.Stop()
		$timings.Add([pscustomobject]@{ Step = $name; Result = $status; Seconds = [math]::Round($stopwatch.Elapsed.TotalSeconds, 1) })
		if ($env:GITHUB_ACTIONS) { Write-Host '::endgroup::' }
	}
}

function Invoke-Skipped([string] $name, [string] $reason) {
	Write-Host "`n=== $name - SKIPPED: $reason" -ForegroundColor Yellow
	if ($env:GITHUB_ACTIONS) { Write-Host "::warning::$name skipped: $reason" }
	$timings.Add([pscustomobject]@{ Step = $name; Result = 'skipped'; Seconds = 0 })
}

# Runs dotnet, tees its output to a log, and fails on a non-zero exit. -warnaserror already fails the publish on any
# trim or AOT warning; the log check is a second gate in case one is ever logged without failing the command.
function Invoke-Dotnet([string] $logName, [string[]] $arguments) {
	$log = Join-Path $logs "$logName.log"
	Write-Host "dotnet $($arguments -join ' ')" -ForegroundColor DarkGray
	& dotnet @arguments 2>&1 | Tee-Object -FilePath $log | Out-Host
	if ($LASTEXITCODE -ne 0) { throw "dotnet $($arguments[0]) failed with exit code $LASTEXITCODE (log: $log)." }

	$warnings = Select-String -Path $log -Pattern 'warning IL[23][0-9]{3}' | Select-Object -First 5
	if ($warnings) {
		throw "dotnet $($arguments[0]) reported trim or AOT warnings:`n$($warnings.Line -join "`n")"
	}
}

function Assert-NoLeaks([string[]] $directories) {
	$global:LASTEXITCODE = 0
	& $leakScan -Path $directories
	if ($LASTEXITCODE -ne 0) { throw "The compiler-host leak scan failed for: $($directories -join ', ')." }
	$scanned.AddRange($directories)
}

function Assert-Contains([string] $directory, [string] $pattern, [string] $what) {
	$found = @(Get-ChildItem $directory -File -ErrorAction SilentlyContinue | Where-Object { $_.Name -match $pattern })
	if ($found.Count -eq 0) { throw "$what is missing from '$directory' (no file matches '$pattern')." }
	Write-Host "  found $what ($($found[0].Name))"
}

function New-NuGetConfig([string] $path, [hashtable[]] $sources) {
	$sourceXml = ($sources | ForEach-Object { "`t`t<add key=`"$($_.Key)`" value=`"$($_.Value)`" />" }) -join "`n"
	$mappingXml = ($sources | ForEach-Object {
			$patterns = ($_.Patterns | ForEach-Object { "`t`t`t<package pattern=`"$_`" />" }) -join "`n"
			"`t`t<packageSource key=`"$($_.Key)`">`n$patterns`n`t`t</packageSource>"
		}) -join "`n"

	@"
<?xml version="1.0" encoding="utf-8"?>
<!-- Generated by scripts/test-packed-consumers.ps1. Used as RestoreConfigFile, so no other configuration applies. -->
<configuration>
	<packageSources>
		<clear />
$sourceXml
	</packageSources>
	<fallbackPackageFolders>
		<clear />
	</fallbackPackageFolders>
	<packageSourceMapping>
$mappingXml
	</packageSourceMapping>
</configuration>
"@ | Set-Content $path -Encoding utf8
}

function Start-App([string] $executable, [string[]] $arguments, [string] $directory, [int] $port, [string] $logName) {
	$env:ASPNETCORE_URLS = "http://127.0.0.1:$port"
	try {
		$parameters = @{
			FilePath = $executable
			WorkingDirectory = $directory
			PassThru = $true
			RedirectStandardOutput = (Join-Path $logs "$logName.stdout.log")
			RedirectStandardError = (Join-Path $logs "$logName.stderr.log")
		}
		if ($arguments) { $parameters.ArgumentList = $arguments }
		return Start-Process @parameters
	}
	finally {
		$env:ASPNETCORE_URLS = $null
	}
}

function Stop-App($process) {
	if ($process -and -not $process.HasExited) { $process.Kill($true) }
	if ($process) { $process.WaitForExit() }
}

function Wait-Http($process, [string] $url) {
	$deadline = [DateTime]::UtcNow.AddSeconds(30)
	while ([DateTime]::UtcNow -lt $deadline) {
		if ($process.HasExited) { throw "The app exited with $($process.ExitCode) before $url answered." }
		try { return Invoke-WebRequest $url -TimeoutSec 5 -SkipHttpErrorCheck }
		catch { Start-Sleep -Milliseconds 500 }
	}
	throw "$url did not answer within 30 seconds."
}

function Get-FreePort {
	$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
	$listener.Start()
	try { return ([System.Net.IPEndPoint] $listener.LocalEndpoint).Port } finally { $listener.Stop() }
}

function Find-Browser {
	if ($env:PACKED_CONSUMER_BROWSER) {
		if (Test-Path $env:PACKED_CONSUMER_BROWSER) { return $env:PACKED_CONSUMER_BROWSER }
		throw "PACKED_CONSUMER_BROWSER is set to '$($env:PACKED_CONSUMER_BROWSER)', which does not exist."
	}

	$candidates = @(
		"${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe"
		"$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe"
		"$env:ProgramFiles\Google\Chrome\Application\chrome.exe"
		"${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe"
		'/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'
		'/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge'
	)
	foreach ($candidate in $candidates) {
		if ($candidate -and (Test-Path $candidate)) { return $candidate }
	}

	foreach ($command in 'google-chrome', 'google-chrome-stable', 'chromium', 'chromium-browser', 'microsoft-edge') {
		$found = Get-Command $command -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
		if ($found) { return $found.Source }
	}

	return $null
}

# Loads $url in a headless browser and polls the page's #self-test element over the Chrome DevTools Protocol until it
# reports passed or failed. The page runs its own checks once it is interactive (WebAssembly started, or the server
# circuit connected), so this proves the app actually runs - not only that it published.
function Invoke-BrowserSelfTest([string] $url, [string] $name) {
	$profile = Join-Path $workspace "browser-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
	New-Item -ItemType Directory -Path $profile -Force | Out-Null
	$arguments = @(
		'--headless=new', '--disable-gpu', '--no-first-run', '--no-default-browser-check', '--disable-extensions',
		'--remote-debugging-port=0', "--user-data-dir=$profile", 'about:blank'
	)
	if ($IsLinux) { $arguments = @('--no-sandbox') + $arguments }

	$process = Start-Process -FilePath $script:browserPath -ArgumentList $arguments -PassThru `
		-RedirectStandardOutput (Join-Path $logs "$name.browser.stdout.log") `
		-RedirectStandardError (Join-Path $logs "$name.browser.stderr.log")
	$socket = $null
	try {
		$portFile = Join-Path $profile 'DevToolsActivePort'
		$deadline = [DateTime]::UtcNow.AddSeconds(30)
		while (-not (Test-Path $portFile) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 200 }
		if (-not (Test-Path $portFile)) { throw "The browser did not open a DevTools port within 30 seconds." }
		$port = (Get-Content $portFile -TotalCount 1).Trim()

		$target = Invoke-RestMethod -Method Put "http://127.0.0.1:$port/json/new?$([uri]::EscapeDataString($url))"
		$socket = [System.Net.WebSockets.ClientWebSocket]::new()
		[void] $socket.ConnectAsync([uri] $target.webSocketDebuggerUrl, [Threading.CancellationToken]::None).GetAwaiter().GetResult()

		$requestId = 0
		$expression = "(() => { const e = document.getElementById('self-test'); " +
			"return e ? JSON.stringify({ result: e.dataset.result, report: e.textContent }) : null; })()"
		$state = $null
		$deadline = [DateTime]::UtcNow.AddSeconds(90)
		while ([DateTime]::UtcNow -lt $deadline) {
			$requestId++
			$payload = @{ id = $requestId; method = 'Runtime.evaluate'; params = @{ expression = $expression; returnByValue = $true } } |
				ConvertTo-Json -Depth 5 -Compress
			$bytes = [Text.Encoding]::UTF8.GetBytes($payload)
			[void] $socket.SendAsync([ArraySegment[byte]]::new($bytes), 'Text', $true, [Threading.CancellationToken]::None).GetAwaiter().GetResult()

			$response = $null
			while (-not $response) {
				$buffer = [byte[]]::new(1MB)
				$builder = [Text.StringBuilder]::new()
				do {
					$received = $socket.ReceiveAsync([ArraySegment[byte]]::new($buffer), [Threading.CancellationToken]::None).GetAwaiter().GetResult()
					[void] $builder.Append([Text.Encoding]::UTF8.GetString($buffer, 0, $received.Count))
				} while (-not $received.EndOfMessage)
				$message = $builder.ToString() | ConvertFrom-Json
				if ($message.PSObject.Properties['id'] -and $message.id -eq $requestId) { $response = $message }
			}

			$value = $response.PSObject.Properties['result'] ? $response.result.result.PSObject.Properties['value']?.Value : $null
			if ($value) {
				$state = $value | ConvertFrom-Json
				if ($state.result -ne 'running') { break }
			}
			Start-Sleep -Milliseconds 500
		}

		if (-not $state) { throw "$url never rendered #self-test within 90 seconds: the app did not start in the browser." }
		$state.report | Set-Content (Join-Path $logs "$name.self-test.log")
		$state.report -split "\r?\n" | Where-Object { $_.Trim() } | ForEach-Object { Write-Host "  $_" }
		if ($state.result -eq 'running') { throw "$url did not finish its self-test within 90 seconds." }

		foreach ($line in ($state.report -split "\r?\n" | Where-Object { $_ -match '^KNOWN ' })) {
			Write-KnownIssue "[$name] $($line.Substring(6).Trim())"
		}

		if ($state.result -ne 'passed') {
			$failed = ($state.report -split "\r?\n" | Where-Object { $_ -match '^FAIL ' }) -join "`n"
			throw "The self-test at $url failed:`n$failed"
		}
	}
	finally {
		if ($socket) { $socket.Dispose() }
		if (-not $process.HasExited) { $process.Kill($true) }
		$process.WaitForExit()
	}
}

$exitCode = 0
$totalStopwatch = [System.Diagnostics.Stopwatch]::StartNew()

try {
	New-Item -ItemType Directory -Path $feed, $packages, $logs, $consumers, $output -Force | Out-Null

	Write-Host "Packed-consumer gate: Purview.ValueObjects $version ($Configuration), RID $RuntimeIdentifier" -ForegroundColor Cyan
	Write-Host "Purview.ZodSharp $zodSharpVersion, Purview.SourceGeneratorFramework $sgfVersion"
	Write-Host "Workspace: $workspace"

	# Guards at the workspace root: MSBuild stops at the first Directory.Build.props/.targets and
	# Directory.Packages.props it finds walking up, so nothing above the workspace - a stray file in the temp folder,
	# a user profile - can change how the consumers build. The consumers must see the packages and nothing else.
	foreach ($guard in 'Directory.Build.props', 'Directory.Build.targets') {
		@"
<Project>
	<!-- Guard written by scripts/test-packed-consumers.ps1: stops $guard inheritance from any parent folder. -->
</Project>
"@ | Set-Content (Join-Path $workspace $guard) -Encoding utf8
	}

	@'
<Project>
	<!-- Guard written by scripts/test-packed-consumers.ps1: the consumers version their packages themselves. -->
	<PropertyGroup>
		<ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
	</PropertyGroup>
</Project>
'@ | Set-Content (Join-Path $workspace 'Directory.Packages.props') -Encoding utf8

	# The repository's SDK, without its MSBuild SDKs (Purview.BuildSdk): the consumers are plain projects.
	[ordered]@{ sdk = [ordered]@{ version = $sdk.version; rollForward = $sdk.rollForward; allowPrerelease = $true } } |
		ConvertTo-Json | Set-Content (Join-Path $workspace 'global.json') -Encoding utf8

	# The dependency feed serves the build-time framework and ZodSharp only; everything else comes from nuget.org.
	$sgfPatterns = @('Purview.SourceGeneratorFramework', 'Purview.SourceGeneratorFramework.*')
	$zodSharpPatterns = @('Purview.ZodSharp', 'Purview.ZodSharp.*')
	$dependencySources = $DependencyFeed ? @(@{ Key = 'dependencies'; Value = $DependencyFeed; Patterns = $sgfPatterns + $zodSharpPatterns }) : @()
	$nugetOrg = @{ Key = 'nuget.org'; Value = 'https://api.nuget.org/v3/index.json'; Patterns = @('*') + $sgfPatterns + $zodSharpPatterns }

	$packConfig = Join-Path $workspace 'pack.nuget.config'
	New-NuGetConfig $packConfig (@($nugetOrg) + $dependencySources)

	# Consumer sources: Purview.ValueObjects from the package just packed and nowhere else - not nuget.org, not a local
	# feed holding an older build of the same version.
	$consumerConfig = Join-Path $consumers 'nuget.config'
	New-NuGetConfig $consumerConfig (@(
			@{ Key = 'packed'; Value = $feed; Patterns = @('Purview.ValueObjects') }
			$nugetOrg
		) + $dependencySources)

	# An isolated extraction directory: NuGet reuses an already-extracted package of the same id and version, so a
	# stale copy from an earlier run would be used instead.
	$env:NUGET_PACKAGES = $packages

	Invoke-Step "Preflight: restore Purview.SourceGeneratorFramework $sgfVersion and Purview.ZodSharp $zodSharpVersion" {
		$probe = Join-Path $workspace 'dependency-probe'
		New-Item -ItemType Directory -Path $probe -Force | Out-Null
		@"
<Project Sdk="Microsoft.NET.Sdk">
	<PropertyGroup>
		<TargetFramework>net10.0</TargetFramework>
	</PropertyGroup>
	<ItemGroup>
		<PackageDownload Include="Purview.SourceGeneratorFramework" Version="[$sgfVersion]" />
		<PackageDownload Include="Purview.ZodSharp" Version="[$zodSharpVersion]" />
		<PackageDownload Include="Purview.ZodSharp.SystemTextJson" Version="[$zodSharpVersion]" />
		<PackageDownload Include="Purview.ZodSharp.Blazor" Version="[$zodSharpVersion]" />
	</ItemGroup>
</Project>
"@ | Set-Content (Join-Path $probe 'probe.csproj') -Encoding utf8

		& dotnet restore (Join-Path $probe 'probe.csproj') "-p:RestoreConfigFile=$packConfig" 2>&1 |
			Tee-Object -FilePath (Join-Path $logs 'dependency-probe.log') | Out-Host
		if ($LASTEXITCODE -ne 0) {
			$from = ((@($nugetOrg) + $dependencySources) | ForEach-Object Value) -join ', '
			throw (
				"Purview.SourceGeneratorFramework $sgfVersion or Purview.ZodSharp $zodSharpVersion (Directory.Packages.props) " +
				"could not be restored from $from. The generator is built against the first and the consumers use the " +
				'second, so nothing can be checked. If a version is not on nuget.org yet, publish it there, or point this ' +
				'gate at a feed that has it: -DependencyFeed <folder or URL>, or set VALUEOBJECTS_DEPENDENCY_FEED ' +
				'(LOCAL_NUGET_FEED_PATH is used when set).'
			)
		}

		# The Blazor consumers use the ASP.NET Core version Purview.ZodSharp.Blazor was built against.
		$blazorNuspec = Join-Path $packages "purview.zodsharp.blazor/$($zodSharpVersion.ToLowerInvariant())/purview.zodsharp.blazor.nuspec"
		$nuspec = [xml] (Get-Content $blazorNuspec -Raw)
		$namespace = [System.Xml.XmlNamespaceManager]::new($nuspec.NameTable)
		$namespace.AddNamespace('n', $nuspec.DocumentElement.NamespaceURI)
		$script:aspNetCoreVersion = $nuspec.SelectSingleNode(
			"//n:dependency[@id='Microsoft.AspNetCore.Components.Web']", $namespace
		)?.GetAttribute('version') -replace '^\[|[,\]].*$', ''
		if (-not $script:aspNetCoreVersion) { throw "Could not read the ASP.NET Core version from $blazorNuspec." }
		Write-Host "  ASP.NET Core $($script:aspNetCoreVersion) (from Purview.ZodSharp.Blazor)"
	}

	if ($PreflightOnly) {
		Write-Host 'The dependencies restore; stopping after the preflight (-PreflightOnly).' -ForegroundColor Green
		return
	}

	if ($IsLinux -and -not (Get-Command clang -ErrorAction SilentlyContinue)) {
		throw 'Native AOT on Linux needs clang and the zlib headers: sudo apt-get install -y clang zlib1g-dev.'
	}

	Invoke-Step 'Pack every shipping package to the isolated feed' {
		$script:repositoryRestoreTouched = $true
		foreach ($package in $shipping) {
			Invoke-Dotnet "restore-$($package.Id)" @('restore', $package.Project, "-p:RestoreConfigFile=$packConfig", '-nr:false')
			Invoke-Dotnet "pack-$($package.Id)" @(
				'pack', $package.Project, '--configuration', $Configuration, '--output', $feed, '--no-restore', '-m:4', '-nr:false'
			)
		}

		$expected = @($shipping.Id | ForEach-Object { "$_.$version.nupkg".ToLowerInvariant() } | Sort-Object)
		$actual = @(Get-ChildItem $feed -Filter '*.nupkg' | ForEach-Object { $_.Name.ToLowerInvariant() } | Sort-Object)
		$difference = Compare-Object $expected $actual
		if ($difference) { throw "The feed does not hold exactly the shipping packages:`n$($difference | Out-String)" }

		# The bytes the consumers restore: a compiler-host assembly anywhere but analyzers/, or a generator or code-fix
		# assembly under lib/, would reach every application that references the package.
		Add-Type -AssemblyName System.IO.Compression.FileSystem
		$hostOnly = '(^|/)(Purview\.SourceGeneratorFramework|Microsoft\.CodeAnalysis)[^/]*$'
		$generatorOnly = '^(lib|ref|runtimes)/.*Purview\.ValueObjects\.SourceGenerator[^/]*$'
		foreach ($nupkg in Get-ChildItem $feed -Filter '*.nupkg') {
			$archive = [System.IO.Compression.ZipFile]::OpenRead($nupkg.FullName)
			try {
				$bad = @($archive.Entries.FullName | Where-Object {
						($_ -match $hostOnly -and $_ -notmatch '^analyzers/') -or $_ -match $generatorOnly
					})
			}
			finally { $archive.Dispose() }

			if ($bad.Count -gt 0) { throw "$($nupkg.Name) ships compiler-host content outside analyzers/: $($bad -join ', ')." }
			Write-Host "  $($nupkg.Name): no compiler-host content outside analyzers/"
		}

		if ($PackOutput) {
			New-Item -ItemType Directory -Path $PackOutput -Force | Out-Null
			Copy-Item (Join-Path $feed '*') $PackOutput -Force
			Write-Host "  copied the packages to $PackOutput"
		}
	}

	Copy-Item (Join-Path $consumerTemplates '*') $consumers -Recurse -Force -Exclude 'Shared'
	foreach ($blazor in 'BlazorWasm', 'BlazorServer') {
		Copy-Item (Join-Path $consumerTemplates 'Shared/*') (Join-Path $consumers $blazor) -Force
	}

	$consumerProperties = @(
		"-p:ValueObjectsPackageVersion=$version"
		"-p:ZodSharpPackageVersion=$zodSharpVersion"
		"-p:AspNetCorePackageVersion=$($script:aspNetCoreVersion)"
		"-p:RestoreConfigFile=$consumerConfig"
		'-nr:false'
	)

	Invoke-Step '(a) Native AOT console: publish and run' {
		$project = Join-Path $consumers 'AotConsole/AotConsole.csproj'
		$publish = Join-Path $output 'aot-console'

		# A framework-dependent build first: its bin holds the deps.json and runtimeconfig.json that list every
		# runtime dependency, which is where an analyzer-only assembly that leaks shows up first.
		Invoke-Dotnet 'aot-console-build' (@('build', $project, '--configuration', 'Release', '-warnaserror') + $consumerProperties)
		Assert-Contains (Join-Path $consumers 'AotConsole/bin/Release/net11.0') '^PackedValueObjectsAot\.deps\.json$' 'the build deps.json'

		Invoke-Dotnet 'aot-console-publish' (@(
				'publish', $project, '--configuration', 'Release', '--runtime', $RuntimeIdentifier, '--output', $publish,
				'-warnaserror'
			) + $consumerProperties)

		& (Join-Path $publish "PackedValueObjectsAot$exe") | Out-Host
		if ($LASTEXITCODE -ne 0) { throw "The Native AOT console exited with ${LASTEXITCODE}: a packed behaviour is wrong." }

		Assert-NoLeaks @((Join-Path $consumers 'AotConsole/bin'), $publish)
	}

	$script:browserPath = $null
	if ($Browser -ne 'Skip') {
		$script:browserPath = Find-Browser
		if (-not $script:browserPath -and $Browser -eq 'Require') {
			throw 'No Chromium-family browser was found (-Browser Require): install Chrome or Edge, or set PACKED_CONSUMER_BROWSER.'
		}
	}
	$browserSkipReason = 'disabled with -Browser Skip.'
	if ($Browser -ne 'Skip') {
		$browserSkipReason = 'no Chromium-family browser was found, so the published app was NOT run. Install Chrome or ' +
			'Edge, set PACKED_CONSUMER_BROWSER, or pass -Browser Require to make this an error.'
	}
	if ($script:browserPath) { Write-Host "`nBrowser: $($script:browserPath)" }

	# A static file host for the published WebAssembly apps; it references no package.
	$wasmHost = Join-Path $output 'wasm-host'
	if ($script:browserPath) {
		Invoke-Step 'WebAssembly static file host: publish' {
			Invoke-Dotnet 'wasm-host-publish' @(
				'publish', (Join-Path $consumers 'WasmHost/WasmHost.csproj'), '--configuration', 'Release', '--output', $wasmHost,
				"-p:RestoreConfigFile=$consumerConfig", '-nr:false'
			)
		}
	}

	function Assert-WasmOutput([string] $publish) {
		$framework = Join-Path $publish 'wwwroot/_framework'
		Assert-Contains $framework '^Purview\.ValueObjects\.[a-z0-9]+\.wasm$' 'Purview.ValueObjects'
		Assert-Contains $framework '^Purview\.ZodSharp\.[a-z0-9]+\.wasm$' 'Purview.ZodSharp'
		Assert-Contains $framework '^Purview\.ZodSharp\.Blazor\.[a-z0-9]+\.wasm$' 'Purview.ZodSharp.Blazor'
		Assert-Contains $framework '^PackedValueObjectsWasm\.[a-z0-9]+\.wasm$' 'the app assembly'
	}

	function Test-WasmInBrowser([string] $publish, [string] $name) {
		if (-not $script:browserPath) {
			Write-Host "  browser self-test SKIPPED: $browserSkipReason" -ForegroundColor Yellow
			return
		}

		$port = Get-FreePort
		$process = Start-App (Join-Path $wasmHost "PackedWasmHost$exe") @("`"$(Join-Path $publish 'wwwroot')`"") $wasmHost $port $name
		try {
			$index = Wait-Http $process "http://127.0.0.1:$port/"
			if ($index.StatusCode -ne 200) { throw "The WebAssembly host answered $($index.StatusCode) for /." }
			Invoke-BrowserSelfTest "http://127.0.0.1:$port/" $name
		}
		finally { Stop-App $process }
	}

	$wasmProject = Join-Path $consumers 'BlazorWasm/BlazorWasm.csproj'
	Invoke-Step '(b) Blazor WebAssembly: trimmed publish, run in a browser' {
		$publish = Join-Path $output 'wasm-trimmed'
		Invoke-Dotnet 'wasm-trimmed-publish' (@(
				'publish', $wasmProject, '--configuration', 'Release', '--output', $publish, '-warnaserror'
			) + $consumerProperties)

		Assert-WasmOutput $publish
		Assert-NoLeaks @((Join-Path $consumers 'BlazorWasm/bin'), $publish)
		Test-WasmInBrowser $publish 'wasm-trimmed'
	}

	$wasmTools = $false
	if ($WasmAot -ne 'Skip') {
		Push-Location $workspace
		try { $wasmTools = [bool] (& dotnet workload list 2>$null | Select-String -Pattern '^\s*wasm-tools\s') }
		finally { Pop-Location }
	}

	if ($WasmAot -eq 'Skip') {
		Invoke-Skipped '(b) Blazor WebAssembly: RunAOTCompilation publish' 'disabled with -WasmAot Skip.'
	}
	elseif (-not $wasmTools -and $WasmAot -eq 'Auto') {
		Invoke-Skipped '(b) Blazor WebAssembly: RunAOTCompilation publish' (
			'the wasm-tools workload is not installed, so the WebAssembly AOT compile was NOT checked. ' +
			'Install it with `dotnet workload install wasm-tools` and rerun, or pass -WasmAot Require to make this an error.'
		)
	}
	else {
		function Publish-WasmAot([string] $name, [string[]] $extra) {
			if (-not $wasmTools) {
				throw 'The wasm-tools workload is not installed (-WasmAot Require): run `dotnet workload install wasm-tools`.'
			}

			# A copy of the project, so each AOT build starts from clean obj/bin rather than another build's.
			$directory = Join-Path $consumers $name
			Copy-Item (Join-Path $consumers 'BlazorWasm') $directory -Recurse -Force -Exclude 'bin', 'obj'
			$publish = Join-Path $output $name
			Invoke-Dotnet "$name-publish" (@(
					'publish', (Join-Path $directory 'BlazorWasm.csproj'), '--configuration', 'Release',
					'--output', $publish, '-p:RunAOTCompilation=true', '-warnaserror'
				) + $extra + $consumerProperties)
			return [pscustomobject]@{ Directory = $directory; Publish = $publish }
		}

		Invoke-Step '(b) Blazor WebAssembly: RunAOTCompilation publish, run in a browser' {
			# Without the type-level rule the Mono AOT compiler crashes on; see PlanId in Shared/Models.cs and the
			# known-issue probe below.
			$aot = Publish-WasmAot 'wasm-aot' @('-p:ExcludeTypeLevelRules=true')
			Assert-WasmOutput $aot.Publish
			Assert-Contains (Join-Path $aot.Publish 'wwwroot/_framework') '^dotnet\.native\.[a-z0-9]+\.wasm$' 'the AOT-compiled runtime'
			Assert-NoLeaks @((Join-Path $aot.Directory 'bin'), $aot.Publish)
			Test-WasmInBrowser $aot.Publish 'wasm-aot'
		}

		Invoke-Step '(b) Known-issue probe: RunAOTCompilation with a type-level [ZodRule] on a scalar' {
			try {
				$probe = Publish-WasmAot 'wasm-aot-type-level-rules' @()
				Write-Host '  the publish succeeded: the Mono AOT crash no longer reproduces. Remove EXCLUDE_TYPE_LEVEL_RULES from the gate.' -ForegroundColor Green
				Assert-NoLeaks @((Join-Path $probe.Directory 'bin'), $probe.Publish)
			}
			catch {
				$log = Join-Path $logs 'wasm-aot-type-level-rules-publish.log'
				if (-not (Test-Path $log) -or -not (Select-String -Path $log -Pattern 'Precompiling failed' -Quiet)) { throw }
				Write-KnownIssue (
					'[wasm-aot] RunAOTCompilation crashes the Mono AOT compiler (Precompiling failed, exit code -1073741819) ' +
					'on a scalar with a type-level [ZodRule] such as [NonSentinel]: Purview.ZodSharp 3.0.0-prerelease.1 ' +
					'emits ZodRuleErrors.Build(..., (object)rule as IZodRule) for it.'
				)
			}
		}
	}

	Invoke-Step '(c) Blazor Server: publish, start, post the static form, run the interactive page' {
		$project = Join-Path $consumers 'BlazorServer/BlazorServer.csproj'
		$publish = Join-Path $output 'blazor-server'
		Invoke-Dotnet 'blazor-server-publish' (@(
				'publish', $project, '--configuration', 'Release', '--output', $publish, '-warnaserror'
			) + $consumerProperties)

		$port = Get-FreePort
		$base = "http://127.0.0.1:$port"
		$process = Start-App (Join-Path $publish "PackedValueObjectsServer$exe") @() $publish $port 'blazor-server'
		try {
			$session = $null
			$page = Wait-Http $process "$base/register"
			if ($page.StatusCode -ne 200) { throw "/register answered $($page.StatusCode)." }
			$session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
			$page = Invoke-WebRequest "$base/register" -WebSession $session
			$token = [regex]::Match($page.Content, 'name="__RequestVerificationToken" value="([^"]+)"').Groups[1].Value
			if (-not $token) { throw 'The statically rendered form carries no antiforgery token.' }

			function Send-Registration([hashtable] $fields) {
				$body = @{ '__RequestVerificationToken' = $token; '_handler' = 'register' }
				foreach ($key in $fields.Keys) { $body["Model.$key"] = $fields[$key] }
				$response = Invoke-WebRequest "$base/register" -Method Post -Body $body -WebSession $session -SkipHttpErrorCheck
				if ($response.StatusCode -ne 200) { throw "Posting the form answered $($response.StatusCode)." }
				return [System.Net.WebUtility]::HtmlDecode($response.Content)
			}

			$invalid = Send-Registration @{ Name = 'x'; Email = 'not-an-email'; Postcode = 'abc'; Plan = [guid]::NewGuid().ToString() }
			$messages = @([regex]::Matches($invalid, '<div class="validation-message"[^>]*>(.*?)</div>') | ForEach-Object { $_.Groups[1].Value -replace '<[^>]+>', ' ' })
			$messages | ForEach-Object { Write-Host "  invalid post: $_" }
			foreach ($expected in @(
					@{ Text = "Field 'Name' must contain at least 2 characters."; What = "the form's own rule, through <ZodValidator>" }
					@{ Text = "The value 'not-an-email' is not valid for 'Email'."; What = "EmailAddress rejecting the posted address (its generated TryParse runs Create)" }
					@{ Text = "The value 'abc' is not valid for 'Postcode'."; What = 'PostalCode rejecting the posted postcode' }
				)) {
				if (-not ($messages | Where-Object { $_.Contains($expected.Text) })) {
					throw "The invalid post does not show $($expected.What): '$($expected.Text)'."
				}
			}
			if ($invalid.Contains('id="registered"')) { throw 'The invalid post was accepted.' }
			if (-not $invalid.Contains('invalid_length')) { throw "The <ZodValidationMessage> does not show the rule's code (invalid_length)." }

			$valid = Send-Registration @{ Name = 'Ada Lovelace'; Email = ' Ada@Example.COM '; Postcode = '12345'; Plan = [guid]::NewGuid().ToString() }
			$registered = [regex]::Match($valid, '<p id="registered"[^>]*>(.*?)</p>').Groups[1].Value
			Write-Host "  valid post: $registered"
			if ($registered -ne 'Registered Ada Lovelace <ada@example.com> at 12345.') {
				throw "The valid post did not register the normalized value objects (got '$registered')."
			}

			$interactive = Invoke-WebRequest "$base/interactive"
			if ($interactive.StatusCode -ne 200 -or -not $interactive.Content.Contains('id="zod-email"')) {
				throw 'The interactive page did not prerender its form.'
			}
			Write-Host '  /interactive prerendered its form'

			if ($script:browserPath) { Invoke-BrowserSelfTest "$base/interactive" 'blazor-server' }
			else { Write-Host "  browser self-test SKIPPED: $browserSkipReason" -ForegroundColor Yellow }
		}
		finally { Stop-App $process }

		# The server publishes the WebAssembly debugging proxy only when it hosts a WebAssembly client, which this one
		# does not, so nothing needs allowing.
		Assert-NoLeaks @((Join-Path $consumers 'BlazorServer/bin'), $publish)
	}

	Invoke-Step '(d) net8.0 console without ZodSharp: build and run' {
		$project = Join-Path $consumers 'Net8Console/Net8Console.csproj'
		Invoke-Dotnet 'net8-console-build' (@('build', $project, '--configuration', 'Release', '-warnaserror') + $consumerProperties)
		$bin = Join-Path $consumers 'Net8Console/bin/Release/net8.0'

		$net8 = [bool] (& dotnet --list-runtimes 2>$null | Select-String -Pattern '^Microsoft\.NETCore\.App 8\.')
		if ($net8) {
			& dotnet (Join-Path $bin 'PackedValueObjectsNet8.dll') | Out-Host
			if ($LASTEXITCODE -ne 0) { throw "The net8.0 console exited with ${LASTEXITCODE}." }
		}
		else {
			Write-Host '  the .NET 8 runtime is not installed: built for net8.0 but NOT run.' -ForegroundColor Yellow
			if ($env:GITHUB_ACTIONS) { Write-Host '::warning::The net8.0 consumer was built but not run: no .NET 8 runtime.' }
		}

		Assert-NoLeaks @((Join-Path $consumers 'Net8Console/bin'))
	}

	if ($StrictKnownIssues -and $knownIssues.Count -gt 0) {
		throw "$($knownIssues.Count) known issue(s) reproduced (-StrictKnownIssues)."
	}
}
catch {
	$exitCode = 1
	Write-Failure "Packed-consumer gate failed: $($_.Exception.Message)"
	if (Test-Path $logs) { Write-Host "Logs: $logs" }
}
finally {
	if ($null -eq $previousNuGetPackages) { $env:NUGET_PACKAGES = $null }
	else { $env:NUGET_PACKAGES = $previousNuGetPackages }

	# Packing restored the shipping projects against the isolated cache that is about to be deleted; restore them
	# against the normal cache, so a later --no-restore build does not point at a missing folder.
	if ($repositoryRestoreTouched) {
		Write-Host "`nRestoring the shipping projects against the normal package cache..." -ForegroundColor Cyan
		foreach ($package in $shipping) {
			& dotnet restore $package.Project -nr:false | Out-Null
			if ($LASTEXITCODE -ne 0) { Write-Failure "Restoring $($package.Project) against the normal cache failed; run dotnet restore." }
		}
	}

	$totalStopwatch.Stop()
	Write-Host "`nPacked-consumer gate timings:" -ForegroundColor Cyan
	$timings | Format-Table -AutoSize | Out-Host
	Write-Host ("Total: {0:N1}s" -f $totalStopwatch.Elapsed.TotalSeconds)
	if ($scanned.Count -gt 0) { Write-Host "Leak-scanned $($scanned.Count) folder(s)." }
	if ($knownIssues.Count -gt 0) {
		# One line per distinct issue, naming every consumer it reproduced in.
		$grouped = $knownIssues | ForEach-Object {
			$match = [regex]::Match($_, '^\[(?<where>[^\]]+)\] (?<what>.*)$')
			[pscustomobject]@{ Where = $match.Groups['where'].Value; What = $match.Groups['what'].Value }
		} | Group-Object What
		Write-Host "`nKnown dependency issues ($($grouped.Count) distinct, $($knownIssues.Count) occurrences):" -ForegroundColor Yellow
		foreach ($issue in $grouped) {
			$where = ($issue.Group.Where | Sort-Object -Unique) -join ', '
			Write-Host "  - [$where] $($issue.Name)" -ForegroundColor Yellow
		}
	}

	if ($KeepArtifacts) {
		Write-Host "Artifacts kept at $workspace" -ForegroundColor Yellow
	}
	else {
		Remove-Item $workspace -Recurse -Force -ErrorAction SilentlyContinue
	}
}

if ($exitCode -ne 0) { exit $exitCode }

Write-Host 'Packed-consumer gate passed.' -ForegroundColor Green
