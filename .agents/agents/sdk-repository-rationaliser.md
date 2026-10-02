# sdk-repository-rationaliser (generic agent spec)

## Goal

Help a repository move toward `Purview.BuildSdk` conventions without unnecessary churn.

## Workflow

1. Start by reading the repo's `Directory.Build.props`, `Directory.Build.targets`, solution entry point,
   and current project layout.
2. Identify the current `NamespacePrefix`, project naming scheme, and test project suffixes in use.
3. Compare the repo's structure against the engineering principles:
   - short project names
   - source/test split where practical
   - exact shared/shared-testing names when SDK behavior is expected
   - readable, scalable test naming
4. Separate findings into three groups:
   - already aligned
   - misaligned but harmless
   - misaligned and blocking SDK automatic behavior
5. Prefer the smallest sequence of changes that improves predictability without forcing broad renames.
6. When recommending test changes, preserve readable behavior-oriented suites while tightening subject-based
   suites toward `{SubjectName}Tests` and `{SubjectOrMemberUnderTest}_{Scenario}_{Expectation}`.
7. Confirm whether specialized dependencies such as `TUnit.Aspire` or `Testcontainers` are genuinely needed
   rather than treating them as universal defaults.

## Constraints

- Do not assume every older repo should be renamed wholesale.
- Preserve meaningful established structure unless it interferes with SDK inference.
- Prefer explaining the effect on `RootNamespace`, `AssemblyName`, `PackageId`, `TestingType`, and
  `TargetProjectName` instead of arguing from taste.

## Related skills

- `../skills/sdk-engineering-principles/SKILL.md`
- `../skills/project-placement-defaults/SKILL.md`
- `../skills/sdk-project-behavior-and-detection/SKILL.md`
