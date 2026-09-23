set quiet

root_folder := "./src/"
solution_file := root_folder + "ValueObjects.slnx"
test_solution := solution_file
build_configuration := "Debug"

pipeline_feed := "https://api.nuget.org/v3/index.json"
pipeline_tool := ".tools/purview-build/purview-build"

artifact_folder := "./artifacts/"

# Displays the list of available commands

[private]
default:
    just --list

# Install the shared Purview.Build tool (authenticated to the Purview-Dev feed) if not present
[private]
ensure-pipeline-tool:
    if [ ! -x "{{ pipeline_tool }}" ]; then \
        dotnet tool install Purview.Build --tool-path .tools/purview-build --add-source "{{ pipeline_feed }}"; \
    fi

# Run the PR pipeline (restore, build, lint, tests, pack, validate)
[group('Pipeline')]
pipeline-pr *args:
    just ensure-pipeline-tool
    echo "Running PR pipeline..."
    "{{ pipeline_tool }}" {{ args }}

# Run the build pipeline (restore, build, lint)
[group('Pipeline')]
pipeline-build *args:
    just ensure-pipeline-tool
    echo "Running build pipeline..."
    "{{ pipeline_tool }}" --Build:RunTests=false --Release:Mode=None {{ args }}

# Run the release pipeline (restore, build, lint, tests, pack, publish, GitHub release)
[group('Pipeline')]
pipeline-release *args:
    just ensure-pipeline-tool
    echo "Running release pipeline..."
    "{{ pipeline_tool }}" --Release:Mode=NuGet {{ args }}

# Run the release pipeline (restore, build, lint, tests, pack, local nuget publish)
# Note: `just` runs recipes through the shell, which strips backslashes from unquoted arguments.
# Use the LOCAL_NUGET_FEED_PATH environment variable or forward slashes, e.g.
# just pipeline-local-release --PublishLocalNuGet:LocalFeedPath=p:/_sync-projects/.local-nuget/
[group('Pipeline')]
pipeline-local-release *args:
    just ensure-pipeline-tool
    echo "Running local release pipeline..."
    "{{ pipeline_tool }}" --Release:Mode=LocalNuGet {{ args }}

# Run the pipeline with tests enabled
[group('Pipeline')]
pipeline-tests *args:
    just ensure-pipeline-tool
    echo "Running tests pipeline..."
    "{{ pipeline_tool }}" --Build:RunTests=true --Release:Mode=None {{ args }}

# Run the pipeline through pack + validate (restore, build, lint, tests, pack, validate pack contents) without publishing/releasing
[group('Pipeline')]
pipeline-pack-validate *args:
    just ensure-pipeline-tool
    echo "Running pack + validate pipeline..."
    "{{ pipeline_tool }}" --Build:RunPack=true --Build:ValidatePack=true --Release:Mode=None {{ args }}

# -----------------------------------------------------------------------------
# Build and Test
# -----------------------------------------------------------------------------

# Builds the solution with the specified configuration (default: Debug)
[group('Build and Test')]
build *args:
    echo "Building {{ BLUE }}{{ solution_file }}{{ NORMAL }} with {{ YELLOW }}{{ build_configuration }}{{ NORMAL }}..."
    dotnet build "{{ solution_file }}" --configuration "{{ build_configuration }}" {{ args }}

# Restore NuGet packages for the solution
[group('Build and Test')]
restore *args:
    dotnet restore {{ solution_file }} {{ args }}

# Runs tests for the solution with the specified configuration (default: Debug)
[group('Build and Test')]
test filter="/*/*/*/*/" *args:
    echo "Running tests for {{ BLUE }}{{ test_solution }}{{ NORMAL }} with {{ YELLOW }}{{ build_configuration }}{{ NORMAL }}..."
    dotnet test --solution "{{ test_solution }}" --configuration "{{ build_configuration }}" --treenode-filter={{ filter }} {{ args }}

# Cleans the solution with the specified configuration (default: Debug)
[group('Build and Test')]
clean *args:
    echo "Cleaning {{ BLUE }}{{ solution_file }}{{ NORMAL }} with {{ YELLOW }}{{ build_configuration }}{{ NORMAL }}..."
    dotnet clean "{{ solution_file }}" --configuration "{{ build_configuration }}" {{ args }}

# Packs the package into a NuGet package and outputs it to the specified folder
[group('Build and Test')]
pack *args:
    just build
    echo "Packing {{ BLUE }}{{ solution_file }}{{ NORMAL }} with {{ YELLOW }}{{ build_configuration }}{{ NORMAL }}..."
    dotnet pack "{{ solution_file }}" --configuration "{{ build_configuration }}" --no-restore --output "{{ artifact_folder }}" {{ args }}

# -----------------------------------------------------------------------------
# Formatting
# -----------------------------------------------------------------------------

# Checks for linting issues in the root folder
[group('Formatting')]
lint-check:
    echo "Linting checking {{ BLUE }}root folder{{ NORMAL }}..."
    dotnet csharpier check .

# Fixes linting issues in the root folder
[group('Formatting')]
lint-fix:
    echo "Linting fixing {{ BLUE }}root folder{{ NORMAL }}..."
    dotnet csharpier format .

# -----------------------------------------------------------------------------
# Versioning and Release
# -----------------------------------------------------------------------------

# Displays the current version of the project.
# Requires Bun.
[group('Versioning and Release')]
version:
    bun -e "console.log('Current Version: {{ GREEN }}' + require('./package.json').version + '{{ NORMAL }}')"

# -----------------------------------------------------------------------------
# System / Shell
# -----------------------------------------------------------------------------

# Opens the solution in the default associated application
[group('Utilities')]
vs:
    echo "Opening {{ BLUE }}{{ solution_file }}{{ NORMAL }}..."
    open "{{ solution_file }}"

# Opens the root folder in Visual Studio Code
[group('Utilities')]
code:
    echo "Opening {{ BLUE }}Visual Studio Code{{ NORMAL }}..."
    code "{{ root_folder }}"

# Clean up the repository by removing build artifacts, bin/obj folders etc, and shutting down the build server
[group('Utilities')]
scrub:
    find . -type d \( -name bin -o -name obj \) -exec rm -rf {} +
    just clean
    just restore --force-evaluate
    dotnet build-server shutdown
