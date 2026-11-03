#!/usr/bin/env pwsh
<#
.SYNOPSIS
	Restores the repository's .NET local tools (.config/dotnet-tools.json), retrying a failed attempt.

.DESCRIPTION
	`dotnet tool restore` can fail on a cold package cache, exit 1, and still leave every tool installed. The
	.NET 11 SDK (rc.3) mis-attributes the command of a RID-specific tool to the entry beside it in the manifest:
	dotnet-inspect 0.26.0 declares DotNetCliTool version 2 with RuntimeIdentifierPackages, and the purview.build
	entry after it is then rejected with

	    The command "purview-build" specified in the tool manifest file is not contained in the package with
	    Package Id purview.build. The commands contained in the package are dotnet-inspect.

	although Purview.Build 0.8.1 declares <Command Name="purview-build"> and restores correctly on its own. The
	failed attempt has downloaded and extracted the packages by then, so the next attempt succeeds - the bug is
	an ordering/state artefact of a cold cache, not a wrong manifest.

	Retrying is bounded and never hides a genuine failure: every attempt must fail before this script fails, and a
	failed attempt's output is printed when none succeeds. It is quiet on success, because
	scripts/test-pack-validation-rules.ps1 restores once per validation run. This is the only place the repository
	restores its local tools, so `just` and CI behave the same way.

.PARAMETER Attempts
	Total attempts, including the first. Defaults to 3.

.PARAMETER DelaySeconds
	Seconds to wait between attempts. Defaults to 2.

.PARAMETER ToolManifest
	Manifest to restore. Defaults to the repository's .config/dotnet-tools.json. A caller that made its own copy -
	scripts/test-pack-validation-rules.ps1 builds a scratch repository - passes that copy instead.

.EXAMPLE
	./scripts/restore-dotnet-tools.ps1

.EXAMPLE
	./scripts/restore-dotnet-tools.ps1 -Attempts 5 -DelaySeconds 10
#>
[CmdletBinding()]
param (
	[ValidateRange(1, 10)]
	[int] $Attempts = 3,
	[ValidateRange(0, 60)]
	[int] $DelaySeconds = 2,
	[string] $ToolManifest
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$manifest = $ToolManifest ? $ToolManifest : (Join-Path $repositoryRoot '.config/dotnet-tools.json')
if (-not (Test-Path $manifest)) { throw "No local tool manifest at '$manifest'." }

# Each failed attempt's output is buffered, not streamed: the retry is expected once on a cold cache, and a caller
# such as scripts/test-pack-validation-rules.ps1 restores 27 times. A genuine failure still prints every attempt.
$failures = [System.Collections.Generic.List[string]]::new()
for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
	$output = & dotnet tool restore --tool-manifest $manifest 2>&1 | ForEach-Object { "$_" }
	if ($LASTEXITCODE -eq 0) {
		# A retried attempt leaves the failed attempt's 1 in $LASTEXITCODE; a caller run as a CI step exits with it.
		$global:LASTEXITCODE = 0
		Write-Host 'Local tools restored.' -ForegroundColor Green
		return
	}

	$failures.Add(($output -join "`n"))
	if ($attempt -lt $Attempts) {
		Write-Host (
			"dotnet tool restore failed (attempt $attempt of $Attempts); retrying in $DelaySeconds s. " +
			"The attempt still populates the package cache, which is what the next one needs - see this script's help."
		) -ForegroundColor Yellow
		Start-Sleep -Seconds $DelaySeconds
	}
}

$failures | ForEach-Object { Write-Host $_ }
throw "dotnet tool restore failed after $Attempts attempt(s)."
