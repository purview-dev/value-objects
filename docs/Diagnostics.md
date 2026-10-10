# Diagnostics

`Purview.ValueObjects` ships a Roslyn analyzer and source generator that report the rules below. Every rule is
enabled by default.

Suppress a rule the usual ways — `#pragma warning disable VO1006`, a `NoWarn` entry, or an `.editorconfig`
severity override (`dotnet_diagnostic.VO1006.severity = none`). Errors cannot be downgraded to warnings: they
mark shapes the generator cannot emit code for.

## Core shape rules

These govern the declaration shape a value object must have for the generator to emit anything.

| Rule | Severity | Reported when | Fix |
| --- | --- | --- | --- |
| `VO1001` | Error | A `[Scalar]` or `[ValueObject]` type is not declared `partial`. | Add `partial`. The code fix in `Purview.ValueObjects.SourceGenerator.Refactorings` does this for you. |
| `VO1002` | Error | The value object is nested inside another type. | Move it to its own top-level type. Nesting is not supported. |
| `VO1003` | Error | The value object is generic. | Remove the type parameters, or declare one concrete value object per closed type. |
| `VO1004` | Error | A `[Scalar]` type does not declare the configured scalar property. | Declare the property, or set `ScalarAttribute.PropertyName` to the one you have. |
| `VO1006` | Warning | A `[Scalar]` type is not a `readonly record struct`. | Prefer `readonly record struct` so equality, immutability and allocation behaviour match the contract. |
| `VO1007` | Warning | `ValueObjectDeserializationMode.Strict` is set but no `Create` overload exists to re-validate through. | Add the `Create` overload, or use the default `Hydrate` mode. |
| `VO1008` | Error | `[Scalar]` and `[ValueObject]` are both applied to the same type. | Pick one. A scalar wraps a single primitive; a value object composes members. |
| `VO1016` | Warning | A value object member is mutable. | Make the member `readonly`/`init`-only. A mutable member breaks immutability and makes equality unstable. |
| `VO1022` | Error | A `[Scalar<T>]` type declares a member with the name of the property the generator owns. | Remove the declaration, or use `[Scalar]` (without a type argument) to own the property yourself. |

## ZodSharp validation rules

See [ZodSharp validation](ZodSharp-Validation.md) for the integration itself.

| Rule | Severity | Reported when | Fix |
| --- | --- | --- | --- |
| `VO1013` | Warning | `OnValidate` is declared but never invoked because `ZodSchemaMode.InsteadOfHooks` is set. | Remove `OnValidate`, or choose a `ZodSchemaMode` that keeps the hooks. |
| `VO1015` | Error | The ZodSharp `SchemaName` is not a valid C# identifier. | Give it a valid identifier. |

## String normalization rules

See [Value object design](Value-Object-Design.md) for the built-in string normalization options.

| Rule | Severity | Reported when | Fix |
| --- | --- | --- | --- |
| `VO1023` | Warning | Built-in string normalization is configured but the value object implements `OnNormalize` or declares its own `Create`. | Remove the option, or move the normalization into the hook/`Create`. |
| `VO1024` | Warning | Built-in string normalization is configured on a non-string scalar or member. | Remove the option; it only applies to `string` values. |

## Entity Framework rules

See [Entity Framework integration](Entity-Framework.md) for the mapping model.

| Rule | Severity | Reported when | Fix |
| --- | --- | --- | --- |
| `VO1009` | Warning | Entity Framework mapping is requested but `Microsoft.EntityFrameworkCore` is not referenced. | Add the package reference, or stop requesting EF mapping. |
| `VO1010` | Warning | EF auto-conversion was skipped because the value object's underlying type is not mappable. | Map it by hand with a custom `ValueConverter`. |
| `VO1017` | Warning | EF JSON mapping is requested without the JSON converter. | Register the JSON converter for the value object. |
| `VO1018` | Warning | EF complex mapping cannot convert one of the members. | Simplify the member, or map it explicitly. |
| `VO1019` | Warning | EF complex-type mapping is requested but the project resolves to EF Core 7 or earlier. | Target EF Core 8+, or use a different `EntityFrameworkMapping`. |
| `VO1021` | Warning | EF key value generation is unavailable for the value object. | Assign keys explicitly instead of relying on store generation. |

## Retired identifiers

`VO1005`, `VO1011`, `VO1012`, `VO1014` and `VO1020` were used during development and withdrawn before release.
They are **not** reported by any version and are **never reused**, so the numbering is intentionally
discontinuous. Treat an occurrence of one of these IDs as stale tooling or a stale suppression, and remove it.

`VO1005` ("scalar constructor is missing") is worth calling out, because it was listed as an `Error` in the
analyzer catalogue and documented here as requiring you to declare a constructor. It never had a reporting
site, and the requirement it described was the opposite of how the generator works: when a `[Scalar]` type
declares no constructor taking the scalar's underlying type, the generator **emits a private one**. You do not
need to declare it, and nothing ever asked you to.

## Release tracking

Rules are tracked in `src/src/SourceGenerator/AnalyzerReleases.Shipped.md` and
`AnalyzerReleases.Unshipped.md`, which the Roslyn `RS2008` catalogue rule enforces. A new or changed diagnostic
must be recorded there in the same change, and the unshipped entries move into a new `## Release <version>`
block when that version ships.
