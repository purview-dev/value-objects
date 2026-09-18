# Entity Framework

`Purview.ValueObjects` value objects work well as Entity Framework property types, especially with JSON columns on
SQL Server (`json`/`jsonb`) and Postgres (`jsonb`).

## JSON columns

Scalar value objects serialize as their underlying primitive, so they store naturally in a JSON column. Use a
`JsonSerializerOptions` that registers `ScalarJsonConverterFactory` and assign it to the JSON column.

```csharp
public static readonly JsonSerializerOptions EntityJsonOptions = CreateOptions();

static JsonSerializerOptions CreateOptions()
{
    var options = new JsonSerializerOptions();
    options.Converters.Add(new ScalarJsonConverterFactory());
    return options;
}
```

In your `DbContext`:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder
        .Entity<Customer>()
        .Property(c => c.Email)
        .HasColumnType("jsonb")
        .HasConversion(
            v => JsonSerializer.Serialize(v, EntityJsonOptions),
            v => JsonSerializer.Deserialize<EmailAddress>(v, EntityJsonOptions)!
        );
}
```

Because scalar value objects serialize to a single primitive, the stored JSON is compact and query-friendly.

## Value converters

For scalar value objects you can also use a plain EF `ValueConverter` without JSON, mapping directly to the
underlying primitive:

```csharp
builder
    .Entity<Customer>()
    .Property(c => c.Email)
    .HasConversion(
        v => v.Value,
        v => EmailAddress.Hydrate(v)
    );
```

`Hydrate` is used so that already-validated persisted values are not re-validated on read.

## Complex value objects

Complex `[ValueObject]` types serialize as an object graph. Store them in a JSON column with the same pattern,
using the generated `[JsonConverter]` (present by default) or the shared options.

## Queryability notes

- Scalar value objects with primitive inner values map naturally to the underlying primitive for filtering.
- For complex values stored as JSON, deep predicates translate depending on the provider and column type.
  Test the exact predicate against your provider before relying on it.

## EF Core compatibility

- `[ValueObject]` types generate a private parameterless constructor by default (see `[ValueObjectDefaults]`)
  to support EF Core materialization.
- Value objects are immutable; EF tracks them by value like any struct/record.

See `samples/` for a runnable DTO + JSON-column example.