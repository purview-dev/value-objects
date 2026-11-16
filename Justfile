# Recipes for Purview.ValueObjects. Run `just` for the list.
#
# The shared recipes come from .build/just/, copied verbatim from the purview-buildsdk-justfile
# skill. Change them there, not here; this file holds what belongs to this repository.

set quiet

# Let a definition here override a module's default variable or no-op hook.
set allow-duplicate-variables
set allow-duplicate-recipes

import ".build/just/index.just"

# The one solution. Purview.Build names the same path in .config/purview-build.json.
solution_file := "src/ValueObjects.slnx"

# Every test, matching TestFilter in .config/purview-build.json so a local run selects what CI does.
test_filter := "/*/*/*/*"

root_folder := "./src/"

[private]
default:
    just --list

# Restore the repository's .NET local tools (.config/dotnet-tools.json)
#
# The shared recipe runs a plain `dotnet tool restore`. This repository's script also retries the
# cold-cache failure the .NET 11 SDK can produce, and is the only place the tools are restored, so
# `just` and CI behave the same way.
[doc("Restore the repository's .NET local tools (.config/dotnet-tools.json)")]
[group('Build and Test')]
ensure-tools:
    pwsh -NoProfile -File scripts/restore-dotnet-tools.ps1

# Prove the packed package works for real consumers: packs Purview.ValueObjects to an isolated feed, then publishes
# and runs a Native AOT console, a Blazor WebAssembly app (trimmed and RunAOTCompilation, run in a headless browser),
# a Blazor Server app and a net10.0 console against it, and leak-scans every output for compiler-host assemblies.
# Needs the Native AOT toolchain, the wasm-tools workload for the WebAssembly AOT step, and Chrome or Edge for the
# browser steps. Purview.ZodSharp/Purview.SourceGeneratorFramework versions not yet on nuget.org restore from
# LOCAL_NUGET_FEED_PATH (or VALUEOBJECTS_DEPENDENCY_FEED). See scripts/test-packed-consumers.ps1 for the options.
[doc('Pack, then publish and run Native AOT, Blazor WebAssembly and Blazor Server consumers of the package')]
[group('Build and Test')]
smoke-packed-consumers *args:
    echo "Running the packed consumer gate..."
    pwsh -NoProfile -File scripts/test-packed-consumers.ps1 {{ args }}

# Prove every PackValidation ForbiddenContent rule in .config/purview-build.json rejects a package: injects a matching
# file into scratch copies of the packed packages and runs the real purview-build validation on them. Reads
# ./artifacts, so pack first (`just pipeline-pack-validate`); `just pipeline-local-release` moves its packages to the
# local feed, so pass -PackagesFolder <feed> after it.
[doc('Prove every PackValidation ForbiddenContent rule in .config/purview-build.json rejects a package')]
[group('Build and Test')]
verify-pack-rules *args: ensure-tools
    echo "Proving the pack-validation forbidden-content rules..."
    pwsh -NoProfile -File scripts/test-pack-validation-rules.ps1 {{ args }}

# Open the source folder in Visual Studio Code
[group('Utilities')]
code:
    echo "Opening {{ BLUE }}Visual Studio Code{{ NORMAL }}..."
    code "{{ root_folder }}"
