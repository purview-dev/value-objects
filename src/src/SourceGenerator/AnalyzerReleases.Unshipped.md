### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
VO1009 | ValueObjects | Warning | Entity Framework mapping requested but Microsoft.EntityFrameworkCore is not referenced
VO1010 | ValueObjects | Warning | Entity Framework auto-conversion skipped for a value object whose underlying type is not mappable
VO1013 | ValueObjects | Warning | OnValidate is not invoked because ZodSchemaMode.InsteadOfHooks is set
VO1015 | ValueObjects | Error | ZodSharp SchemaName is not a valid identifier
VO1016 | ValueObjects | Warning | Value object member is mutable
VO1017 | ValueObjects | Warning | Entity Framework JSON mapping requires the JSON converter
VO1018 | ValueObjects | Warning | Entity Framework complex mapping cannot convert a member
VO1019 | ValueObjects | Warning | Entity Framework complex type mapping requires Entity Framework Core 8 or later
VO1021 | ValueObjects | Warning | Entity Framework key value generation is unavailable for the value object