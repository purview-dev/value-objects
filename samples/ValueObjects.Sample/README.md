# Purview.ValueObjects Sample

A console sample demonstrating source-generated scalar and complex value objects, JSON serialization, and the
Entity Framework JSON-column shape.

## Run

```text
dotnet run --project samples/ValueObjects.Sample
```

## What it shows

- `[Scalar]` value objects: `Create`/`Hydrate`/`TryCreate`, normalization, validation, implicit conversions,
  equality with the primitive.
- `[ValueObject]` complex value objects with cross-field validation.
- JSON round-trip of an entity whose members are value objects, using a `JsonSerializerOptions` configured with
  `ScalarJsonConverterFactory` (the shape EF stores in a JSON column).
- Scalar value objects serialize as their underlying primitive.