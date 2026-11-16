## Release 1.0.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
VO1001 | ValueObjects | Error | Value objects must be partial
VO1002 | ValueObjects | Error | Nested value objects are not supported
VO1003 | ValueObjects | Error | Generic value objects are not supported
VO1004 | ValueObjects | Error | Scalar value objects must declare the configured scalar property
VO1006 | ValueObjects | Warning | Scalar value objects should be readonly record structs
VO1007 | ValueObjects | Warning | Strict deserialization mode requires a Create overload
VO1008 | ValueObjects | Error | [Scalar] and [ValueObject] cannot be combined
VO1009 | ValueObjects | Warning | Entity Framework mapping requested but Microsoft.EntityFrameworkCore is not referenced
VO1010 | ValueObjects | Warning | Entity Framework auto-conversion skipped for a value object whose underlying type is not mappable
VO1013 | ValueObjects | Warning | OnValidate is not invoked because ZodSchemaMode.InsteadOfHooks is set
VO1015 | ValueObjects | Error | ZodSharp SchemaName is not a valid identifier
VO1016 | ValueObjects | Warning | Value object member is mutable
VO1017 | ValueObjects | Warning | Entity Framework JSON mapping requires the JSON converter
VO1018 | ValueObjects | Warning | Entity Framework complex mapping cannot convert a member
VO1019 | ValueObjects | Warning | Entity Framework complex type mapping requires Entity Framework Core 8 or later
VO1021 | ValueObjects | Warning | Entity Framework key value generation is unavailable for the value object

## Release 1.1.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
VO1022 | ValueObjects | Error | The automatic [Scalar<T>]/[Scalar(typeof(T))] form declares the scalar property, so the author must not declare it
VO1023 | ValueObjects | Warning | Built-in string normalization is ignored because the value object implements OnNormalize or declares its own Create
VO1024 | ValueObjects | Warning | Built-in string normalization requires a string scalar or member

## Release 2.0.0

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
