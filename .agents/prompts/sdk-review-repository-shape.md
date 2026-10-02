# sdk-review-repository-shape (generic prompt spec)

Review a repository that uses `Purview.BuildSdk` for naming, placement, and test-structure alignment.

## Required behaviour

1. Inspect the repository's `Directory.Build.props`, `Directory.Build.targets`, solution entry point, and
   project layout before making assumptions.
2. Identify whether the repository follows the SDK-friendly structure:
   - source projects under `src/`
   - test projects under `tests/`
   - `.csproj` filenames matching directory names
   - short project names with `NamespacePrefix` carrying the repo identity
3. Check whether test project names use recognised `*Tests` suffixes and whether shared/shared-testing
   projects use exact SDK-recognised names.
4. Explain the consequences of deviations in terms of automatic `RootNamespace`, `AssemblyName`,
   `PackageId`, `TargetProjectName`, and automatic project references.
5. Review test readability conventions:
   - subject-based `{SubjectName}Tests`
   - subject-based `{SubjectOrMemberUnderTest}_{Scenario}_{Expectation}` method names
   - non-subject-based suites named clearly for their broader role
   - appropriate use of TUnit categories and display names
6. Distinguish between:
   - acceptable existing variance worth preserving
   - structural debt that blocks the SDK's automatic behavior
   - incremental rationalisation opportunities

## Suggested output

- A concise summary of whether the repo broadly fits the SDK conventions.
- A list of concrete mismatches, ordered by impact.
- A list of low-risk rationalisation steps for naming, placement, identity, or test readability.
- Explicit note of which behaviors are already automatic defaults and which require manual configuration.
