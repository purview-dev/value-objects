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
VO1028 | ValueObjects | Error | An automatic [Scalar<T>(Nullable = true)] also declares the property, so nullability has two sources; remove one of them
VO1029 | ValueObjects | Error | An automatic [Scalar<T>(Nullable = false)] declares a nullable Value property, contradicting the option
VO1030 | ValueObjects | Error | An automatic [Scalar<T>] declares a Value property whose type does not match the underlying type
VO1031 | ValueObjects | Warning | A nullable scalar is used as a non-nullable entity/complex property, which Entity Framework Core maps to a required column
