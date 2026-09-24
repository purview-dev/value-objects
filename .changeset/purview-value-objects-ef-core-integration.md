---
"purview-value-objects": minor
---

feat: Entity Framework Core integration

- Reference `Microsoft.EntityFrameworkCore` and the generator emits an `EF` nested class per value object
  (a `ValueConverter`/`ValueComparer`), an assembly-level `ValueObjectEFExtensions.ConfigureValueObjects`
  extension for `OnModelCreating`, and a `UseValueObjects()` options-builder extension plus a generated
  `ModelCustomizer` so contexts registered via `AddDbContext`, `AddDbContextFactory`, or `AddDbContextPool`
  are mapped automatically without an `OnModelCreating` override.
- Scalar value objects convert to their underlying primitive column; complex value objects map as EF Core
  complex types (EF Core 8+) by default or JSON columns via `[ValueObject(EFMapping = EntityFrameworkMapping.Json)]`.
- Queries compare the value object type directly — no `.Value` required.
- Value objects declared in **referenced assemblies** (e.g. a shared domain models project) are discovered
  through their `IEFScalarValueObject`/`IEFComplexValueObject` marker interfaces and mapped by the consumer's
  registry automatically, as long as the provider assembly references `Microsoft.EntityFrameworkCore`.
  Provider-only assemblies can opt out of emitting their own registry with `DisableValueObjectsEFRegistry`.
- New options: `[Scalar(GenerateEFConverter, GenerateEFComparer)]`, `[ValueObject(EFMapping, GenerateEFComparer)]`,
  and matching `[ValueObjectDefaults]` assembly defaults; `EntityFrameworkMapping` enum; `IEFScalarValueObject<,>` and
  `IEFComplexValueObject<>` markers.
- Opt out via the `DisableValueObjectsEFGeneration` MSBuild property, `DisableValueObjectsEFRegistry`, per-assembly defaults, or per-type options.
- Diagnostics `VO1009` (EF requested without `Microsoft.EntityFrameworkCore`) and `VO1010` (auto-conversion
  skipped for a non-mappable underlying type).