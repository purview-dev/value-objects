# Purview.ValueObjects

Source-generated scalar and complex value objects for .NET.

Adds F#-style single-case types to C#. Mark a `partial` struct or class with `[Scalar]` or `[ValueObject]` and the
source generator produces `Create`/`Hydrate` factories, normalization (`OnNormalize`), validation (`OnValidate`),
`Empty` instances, equality, comparison, implicit conversions, and JSON converters.

- **DTOs** – strong types with serialization/deserialization and business rules.
- **Entity Framework** – value objects map cleanly onto JSON columns via `ScalarJsonConverterFactory`.
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

See the `samples/` folder for end-to-end examples.