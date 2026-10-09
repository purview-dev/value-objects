# Purview.ValueObjects

Source-generated scalar and complex value objects for .NET.

Adds F#-style single-case types to C#. Mark a `partial` struct or class with `[Scalar]` or `[ValueObject]` and the
incremental source generator produces:

- `Create` / `Hydrate` / `TryCreate` factories with `OnNormalize` normalization and `OnValidate` validation
- `Empty` instances, equality, comparison, `CompareTo`, `ToString` (including the underlying value's `IFormattable`
  and format overloads), and implicit conversions
- The underlying value's standard interfaces — `IEquatable<T>`, `ISpanFormattable`, `IUtf8SpanFormattable`,
  `IParsable<T>`, `ISpanParsable<T>`, and `IUtf8SpanParsable<T>` — mirrored per type
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
generator declare it (and `[Scalar<string>(Nullable = true)]` expresses a nullable reference scalar).

## Formatting and parsing

A scalar mirrors the formatting and parsing surface of the type it wraps. When the underlying value implements
`IFormattable`, `ISpanFormattable`, `IUtf8SpanFormattable`, `IParsable<T>`, `ISpanParsable<T>`, or
`IUtf8SpanParsable<T>`, the generated value object implements the same interfaces and forwards to the wrapped
value:

```csharp
[Scalar<Guid>]
public readonly partial record struct InstallationId { }

var id = InstallationId.Create(Guid.NewGuid());

id.ToString("N", CultureInfo.InvariantCulture);   // forwards to Guid.ToString("N", ...)
InstallationId.Parse("2f8a…", CultureInfo.InvariantCulture);  // Create(...) — validates
InstallationId.TryParse("not-a-guid", null, out _);           // false
```

`Parse` validates through `Create`; `TryParse` returns `false` (through `TryCreate`) for input that does not
parse or fails domain validation. Detection is per type and per target framework, so a scalar only gets the
interfaces its underlying type actually implements (`string` is parsable but not span-formattable; an enum is
span-formattable but not parsable). Numeric/arithmetic interfaces, `IConvertible`, and collection interfaces are
intentionally not mirrored.

## JSON serialization

Scalar value objects serialize as their underlying value. Register the converter factory on your
`JsonSerializerOptions`:

```csharp
var options = new JsonSerializerOptions();
options.Converters.Add(new ScalarJsonConverterFactory());
```

The generator also emits a `[JsonConverter]` per value object, so scalar/complex value objects serialize
correctly even when the factory is not registered. That generated converter is reflection-free and trim- and
Native AOT-safe; register `ScalarJsonConverterFactory` only for a hand-written scalar, in a host that is neither
trimmed nor AOT-compiled.

## Entity Framework Core

When your project references `Microsoft.EntityFrameworkCore`, the generator emits an `EF` nested class per value
object (a `ValueConverter`/`ValueComparer`) and an assembly-level `ConfigureValueObjects` extension that maps
them automatically:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ConfigureValueObjects();   // generated into your project
}
```

Scalar value objects convert to their underlying primitive column; complex value objects map as EF Core complex
types (EF Core 8+) by default or JSON columns via `[ValueObject(EFMapping = EntityFrameworkMapping.Json)]`.
Queries compare the value object type directly — no `.Value` required — or the raw underlying value
(`c.Email == "..."`, `m.Id == guid`). See the Entity Framework guide for automatic + manual mapping, keys and
indexes, generated key values, query filters, schema/migrations, assembly defaults, and the opt-out levels.

## Validation with ZodSharp

Validate value objects with [Purview.ZodSharp](https://www.nuget.org/packages/Purview.ZodSharp), a C# port of Zod:

- **Generator-integrated** – a `[Scalar]`/`[ValueObject]` type that is also `[ZodSchema]` has its generated
  `Create` wired to the ZodSharp-generated schema (`Create` throws `ZodException` on invalid input).
  `ZodSchemaMode.InsteadOfHooks` opts out of the `OnValidate` hook; implement the generated
  `OnZodValidate(RefineCtx<T>)` hook to add Zod-compatible refinements.
- **Generated validators** – annotate a type with `[ZodSchema]` + DataAnnotations to get a zero-allocation
  `{Type}Schema` validator.
- **Custom rules on scalars** – Purview.ZodSharp ships a validation attribute for every built-in rule (for
  example `[NonSentinel(Message = "…")]`), and a custom `[ZodRule]`-mapped attribute validates the scalar as a
  unit and owns the reported `Code`/`Origin`. A rule written for the underlying value is adapted automatically
  via the generator-emitted `ScalarRuleAdapter`.
- **Schema-first** – build a schema for the underlying value (`Z.String().Email()`, `Z.Number()`, `Z.Enum<>()`)
  and construct the value object through its strict `Create` factory.

## How it works

- `Create(...)` is the strict creation path: normalize, validate, then construct.
- `Hydrate(...)` reconstructs from persisted data without re-validating and is the path used by EF provider
  conversions.
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

See the project documentation for the full guides: getting started, value object design, Entity Framework, and
ZodSharp validation.
