# Purview.ValueObjects

Source-generated scalar and complex value objects for .NET.

Adds F#-style single-case types to C#. Mark a `partial` struct or class with `[Scalar]` or `[ValueObject]` and the
source generator produces `Create`/`Hydrate` factories, normalization (`OnNormalize`), validation (`OnValidate`),
`Empty` instances, equality, comparison, implicit conversions, and JSON converters.

- **DTOs** – strong types with serialization/deserialization and business rules.
- **Entity Framework** – reference `Microsoft.EntityFrameworkCore` and the generator emits mapping members
  (value converters, comparers, complex-type mapping) plus a `ConfigureValueObjects` extension for automatic
  mapping. Queries use the value object type directly.
- **Domain models** – the F#-style single-case union pattern in C#.

## Quick start

```csharp
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

## JSON serialization

Scalar value objects serialize as their underlying value. Register the converter factory on your
`JsonSerializerOptions`:

```csharp
var options = new JsonSerializerOptions();
options.Converters.Add(new ScalarJsonConverterFactory());
```

Use the same options for Entity Framework JSON columns:

```csharp
modelBuilder
    .Entity<Customer>()
    .Property(c => c.Email)
    .HasColumnType("jsonb");
```

## Entity Framework Core

When your project references `Microsoft.EntityFrameworkCore`, the generator emits an `Ef` nested class per value
object (a `ValueConverter`/`ValueComparer`) and an assembly-level `ConfigureValueObjects` extension that maps
them automatically:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ConfigureValueObjects();   // generated into your project
}
```

Scalar value objects convert to their underlying primitive column; complex value objects map as EF Core complex
types (EF Core 8+) by default or JSON columns via `[ValueObject(EfMapping = EfMapping.Json)]`. Queries compare
the value object type directly — no `.Value` required. See `docs/Entity-Framework.md` for the full guide.

See the `src/src/Sample` and `src/src/ZodSharpSample` projects for end-to-end examples.