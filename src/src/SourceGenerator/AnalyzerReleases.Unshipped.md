; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md
;
; Every rule is recorded in AnalyzerReleases.Shipped.md. Add a new or changed diagnostic here, and move
; it into a new `## Release <version>` block when that version ships.

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
VO1023 | ValueObjects | Warning | Built-in string normalization is ignored because the value object implements OnNormalize or declares its own Create
VO1024 | ValueObjects | Warning | Built-in string normalization requires a string scalar or member

