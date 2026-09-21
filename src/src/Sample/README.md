# Purview.ValueObjects Sample

A console sample demonstrating source-generated scalar and complex value objects, JSON serialization, and the
Entity Framework Core integration.

## Run

```text
dotnet run --project src/src/Sample
```

## What it shows

- `[Scalar]` value objects: `Create`/`Hydrate`/`TryCreate`, normalization, validation, implicit conversions,
  equality with the primitive.
- `[ValueObject]` complex value objects with cross-field validation.
- JSON round-trip of an entity whose members are value objects, using a `JsonSerializerOptions` configured with
  `ScalarJsonConverterFactory` (the shape EF stores in a JSON column).
- Entity Framework Core:
  - `modelBuilder.ConfigureValueObjects()` automatically maps every value object in the assembly:
    scalar value objects convert to their underlying primitive column, complex value objects map as EF Core
    complex types (EF Core 8+).
  - Queries use the value object type directly — no `.Value` required — including nested members of complex
    value objects (`o.Total.Amount > 20`).
  - See `SampleDbContext` for the `OnModelCreating` wiring.