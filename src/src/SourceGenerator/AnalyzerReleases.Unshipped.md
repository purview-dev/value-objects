; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md
;
; Every rule is recorded in AnalyzerReleases.Shipped.md. Add a new or changed diagnostic here, and move
; it into a new `## Release <version>` block when that version ships.

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
VO1025 | ValueObjects | Error | [StringNormalize] is only supported on a string member of a complex [ValueObject]; on a [Scalar] or any other type it has no effect
VO1026 | ValueObjects | Error | [ValueObjectDefaults] repeats a default already supplied by the project's MSBuild configuration
VO1027 | ValueObjects | Error | A [Scalar]/[ValueObject] option repeats the effective assembly default
