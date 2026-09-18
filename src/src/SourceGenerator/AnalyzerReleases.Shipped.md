## Release 1.0.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
VO1001 | ValueObjects | Error | Value objects must be partial
VO1002 | ValueObjects | Error | Nested value objects are not supported
VO1003 | ValueObjects | Error | Generic value objects are not supported
VO1004 | ValueObjects | Error | Scalar value objects must declare the configured scalar property
VO1005 | ValueObjects | Error | Scalar value objects must declare a constructor matching their scalar value
VO1006 | ValueObjects | Warning | Scalar value objects should be readonly record structs
VO1007 | ValueObjects | Warning | Strict deserialization mode requires a Create overload
VO1008 | ValueObjects | Error | [Scalar] and [ValueObject] cannot be combined