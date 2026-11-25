; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md
;
; Every rule is recorded in AnalyzerReleases.Shipped.md. Add a new or changed diagnostic here, and move
; it into a new `## Release <version>` block when that version ships.

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
VO1032 | ValueObjects | Error | A closed or abstract type cannot be a value object; mark a concrete derived type instead.
VO1033 | ValueObjects | Error | A union declaration cannot be a value object; use the union as a member type instead.

