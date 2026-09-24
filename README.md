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
and the diagnostic analyzer. There is no dependency on any event-sourcing library.

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

`[Scalar]` wraps a single primitive; `[ValueObject]` wraps multiple members.

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
the value object type directly — no `.Value` required:

```csharp
EmailAddress email = "demo@example.com";
var customers = await db.Customers.Where(c => c.Email == email).ToListAsync();
var bigOrders = await db.Orders.Where(o => o.Total.Amount > 100m).ToListAsync();
```

See [Entity Framework](docs/Entity-Framework.md) for the full guide (automatic + manual mapping, assembly
defaults, and the three opt-out levels).

See the `src/src/Sample` and `src/src/ZodSharpSample` projects for end-to-end examples and `docs/` for guidance.

## Validation with ZodSharp

Validate value objects with [Purview.ZodSharp](https://www.nuget.org/packages/Purview.ZodSharp), a C# port of
Zod. Three patterns are supported:

- **Generator-integrated** – a value object annotated with both `[Scalar]`/`[ValueObject]` and `[ZodSchema]` has
  its generated `Create` wired to the ZodSharp-generated schema (`Create` throws `ZodException` on invalid input).
  `ZodSchemaMode.InsteadOfHooks` opts out of the `OnValidate` hook.
- **Generated validators** – annotate a value object or DTO with `[ZodSchema]` + DataAnnotations; a source
  generator emits a zero-allocation `{Type}Schema` validator (`EmailAddressSchema.Validate(email)`).
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
- `src/src/SourceGenerator` – incremental source generator + analyzer.
- `src/src/SourceGenerator.Refactorings` – code fix for the "must be partial" diagnostic.
- `src/tests` – unit and source-generator tests.
- `docs` – design and usage guidance.
