---
"purview-value-objects": minor
---

feat: Entity Framework Core integration

- Reference `Microsoft.EntityFrameworkCore` and the generator emits an `Ef` nested class per value object
  (a `ValueConverter`/`ValueComparer`), an assembly-level `ValueObjectEfExtensions.ConfigureValueObjects`
  extension for `OnModelCreating`, and a `UseValueObjects()` options-builder extension plus a generated
  `ModelCustomizer` so contexts registered via `AddDbContext`, `AddDbContextFactory`, or `AddDbContextPool`
  are mapped automatically without an `OnModelCreating` override.
- Scalar value objects convert to their underlying primitive column; complex value objects map as EF Core
  complex types (EF Core 8+) by default or JSON columns via `[ValueObject(EfMapping = EfMapping.Json)]`.
- Queries compare the value object type directly — no `.Value` required.
- New options: `[Scalar(GenerateEfConverter, GenerateEfComparer)]`, `[ValueObject(EfMapping, GenerateEfComparer)]`,
  and matching `[ValueObjectDefaults]` assembly defaults; `EfMapping` enum; `IEfScalarValueObject<,>` and
  `IEfComplexValueObject<>` markers.
- Opt out via the `DisableValueObjectsEfGeneration` MSBuild property, per-assembly defaults, or per-type options.
- Diagnostics `VO1009` (EF requested without `Microsoft.EntityFrameworkCore`) and `VO1010` (auto-conversion
  skipped for a non-mappable underlying type).