# Purview.ValueObjects

[![NuGet](https://img.shields.io/nuget/v/Purview.ValueObjects.svg)](https://www.nuget.org/packages/Purview.ValueObjects)
[![Release](https://github.com/purview-dev/value-objects/actions/workflows/release.yml/badge.svg)](https://github.com/purview-dev/value-objects/actions/workflows/release.yml)

Source-generated scalar and complex value objects for .NET.

Adds F#-style single-case types to C#. Mark a `partial` struct or class with `[Scalar]` or `[ValueObject]` and the
incremental source generator produces:

- `Create` / `Hydrate` / `TryCreate` factories with `OnNormalize` normalization and `OnValidate` validation
- `Empty` instances, equality, comparison, `CompareTo`, `ToString`, and implicit conversions
- JSON converters (scalar value objects serialize as their underlying value)
- Contextual creation via `IContextualValueObject<,>` + `ValueObjectContext<T>`

**Use cases**

- **DTOs** – strong, self-validating types with serialization/deserialization and business rules.
- **Entity Framework** – reference `Microsoft.EntityFrameworkCore` and the generator emits mapping members
  (value converters, comparers, complex-type mapping) plus a `ConfigureValueObjects` extension for automatic
  mapping. Queries use the value object type directly — no `.Value` required.
- **Domain models** – the F#-style single-case union pattern in C#.

## Install

```text
dotnet add package Purview.ValueObjects
```

The package ships the runtime contracts (`[Scalar]`, `[ValueObject]`, `IValueObject`, ...), the source generator,
the diagnostic analyzer, and code fixes for the analyzer's diagnostics (for example `VO1001` offers
**Add 'partial' modifier**). There is no dependency on any event-sourcing library.

## Quick start

```csharp
using Purview.ValueObjects.Serialization;

[Scalar]
public readonly partial record struct EmailAddress
{
    public string Value { get; }

    static partial void OnNormalize(ref string value) => value = value?.Trim().ToLowerInvariant()!;

    static partial void OnValidate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Email is required.", nameof(value));
    }
}

var email = EmailAddress.Create("Demo@Example.com");
// email.Value == "demo@example.com"
```

`[Scalar]` wraps a single primitive; `[ValueObject]` wraps multiple members. The manual `[Scalar]` form
declares the underlying property; the automatic `[Scalar<T>]` / `[Scalar(typeof(T))]` forms have the
generator declare it (and `[Scalar<string>(Nullable = true)]` expresses a nullable reference scalar). See
[Getting Started](docs/Getting-Started.md#automatic-vs-manual-underlying-property).

## JSON serialization

Scalar value objects serialize as their underlying value. Register the converter factory on your
`JsonSerializerOptions`:

```csharp
var options = new JsonSerializerOptions();
options.Converters.Add(new ScalarJsonConverterFactory());
```

The generator also emits a `[JsonConverter]` per value object, so scalar/complex value objects serialize correctly
even when the factory is not registered.

Use the same options for Entity Framework JSON columns:

```csharp
modelBuilder
    .Entity<Customer>()
    .Property(c => c.Email)
    .HasColumnType("jsonb");
```

## Entity Framework Core

When your project references `Microsoft.EntityFrameworkCore`, the generator emits an `EF` nested class per value
object and an assembly-level `ConfigureValueObjects` extension that maps them automatically:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ConfigureValueObjects();   // generated into your project
}
```

Scalar value objects convert to their underlying primitive column; complex value objects map as EF Core complex
types (EF Core 8+) by default or JSON columns via `[ValueObject(EFMapping = EntityFrameworkMapping.Json)]`. Queries compare
the value object type directly — no `.Value` required — or the raw underlying value:

```csharp
EmailAddress email = "demo@example.com";
var customers = await db.Customers.Where(c => c.Email == email).ToListAsync();
var byRawString = await db.Customers.Where(c => c.Email == "demo@example.com").ToListAsync();
var byRawGuid = await db.Customers.Where(c => c.Id == customerId).ToListAsync();
var bigOrders = await db.Orders.Where(o => o.Total.Amount > 100m).ToListAsync();
```

Keys, foreign keys, and generated key values:

```csharp
[Scalar(GenerateEFValueGenerator = true)]
public readonly partial record struct CustomerId
{
    public Guid Value { get; }
}

protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
{
    configurationBuilder.UseValueObjectKeyGenerators();   // generated into your project
}
```

An unset key receives a time-ordered (version 7) identifier on save, a key the domain set is never
overwritten, and an entity configuration such as `ValueGeneratedNever()` always wins. Where the store compares
identifiers differently — notably a clustered SQL Server key — pass the ordering:
`UseValueObjectKeyGenerators(ValueObjectKeyOrdering.SqlServer)`. See
[Entity Framework](docs/Entity-Framework.md#key-ordering) for the ordering table.

See [Entity Framework](docs/Entity-Framework.md) for the full guide (automatic + manual mapping, keys and
indexes, generated key values, query filters, schema/migrations, assembly defaults, and the opt-out levels).

See the `src/src/Sample`, `src/src/EFDomainSample.Persistence` (domain + persistence split), and
`src/src/ZodSharpSample` projects for end-to-end examples and `docs/` for guidance.

## Validation with ZodSharp

Validate value objects with [Purview.ZodSharp](https://www.nuget.org/packages/Purview.ZodSharp), a C# port of
Zod. Several patterns are supported:

- **Generator-integrated** – a value object annotated with both `[Scalar]`/`[ValueObject]` and `[ZodSchema]` has
  its generated `Create` wired to the ZodSharp-generated schema (`Create` throws `ZodException` on invalid input).
  Implement the generated `OnZodValidate(RefineCtx<T>)` hook to add your own Zod-compatible rules;
  `ZodSchemaMode.InsteadOfHooks` opts out of the `OnValidate` hook.
- **Generated validators** – annotate a value object or DTO with `[ZodSchema]` + DataAnnotations; a source
  generator emits a zero-allocation `{Type}Schema` validator (`EmailAddressSchema.Validate(email)`).
- **Custom rules on scalars** – Purview.ZodSharp ships a validation attribute for every built-in rule (for
  example `[NonSentinel(Message = "…")]`), and a custom `[ZodRule]`-mapped attribute validates the scalar as a
  unit and owns the reported `Code`/`Origin`. A rule written for the underlying value is adapted automatically
  via the generator-emitted `ScalarRuleAdapter`.
- **Schema-first** – build a schema for the scalar's underlying value (`Z.String().Email()`, `Z.Number()`,
  `Z.Enum<>()`) and construct the value object through its strict `Create` factory.

```csharp
using ZodSharp;

[Scalar]
[ZodSchema]
public readonly partial record struct EmailAddress
{
    [EmailAddress]
    public string Value { get; }
}

var result = EmailAddressSchema.Validate(EmailAddress.Create("demo@example.com"));
```

See [ZodSharp Validation](docs/ZodSharp-Validation.md), the `src/src/ZodSharpSample` project, and
the `src/src/ZodSharp.AspNetCoreSample` project (ASP.NET Core Problem Details for strict deserialization failures).

## How it works

- `Create(...)` is the strict creation path: normalize, validate, then construct.
- `Hydrate(...)` reconstructs from persisted data without re-validating and is the path used by EF
  provider conversions.
- `ValueObjectDeserializationMode` controls which factory JSON deserialization uses (`Hydrate` by default,
  `Strict` re-runs validation).
- Contextual value objects (`IContextualValueObject<TSelf, TValue, TOwner>`) validate against the owning instance
  through `ValueObjectContext<TOwner>`.

## Disabling the generator

Set `DisableValueObjectsSourceGenerator` to `true` in your project:

```xml
<PropertyGroup>
    <DisableValueObjectsSourceGenerator>true</DisableValueObjectsSourceGenerator>
</PropertyGroup>
```

## Repository

- `src/src/ValueObjects` – runtime contracts and the `ScalarJsonConverterFactory`.
- `src/src/EFDomainSample.Domain` / `src/src/EFDomainSample.Persistence` – a domain project without Entity
  Framework and the persistence project that maps its value objects.
- `src/src/SourceGenerator` – incremental source generator + analyzer.
- `src/src/SourceGenerator.Refactorings` – code fixes for the generator's diagnostics (the "must be partial"
  fix). It consumes the generator's diagnostic identities through **public** members only: the shipped analyzer
  is the merged, self-contained artifact the `Purview.SourceGeneratorFramework` merge pass produces, and that
  pass strips every `InternalsVisibleTo` declaration.
- `src/tests` – unit and source-generator tests.
- `docs` – design and usage guidance.
