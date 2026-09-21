# Entity Framework

`Purview.ValueObjects` integrates with Entity Framework Core by source-generating the mapping members into your
project **when** `Microsoft.EntityFrameworkCore` is referenced. Scalar value objects convert to their underlying
primitive column; complex value objects map as EF Core complex types (EF Core 8+) or JSON columns.

The runtime package stays free of Entity Framework dependencies: all Entity Framework code is generated into the
consuming project, and everything below is opt-in per feature with an opt-out hierarchy
(`DisableValueObjectsEfGeneration` MSBuild property → assembly defaults → per-type options).

## Prerequisite

Reference Entity Framework Core (the integration activates automatically when the project references it):

```text
dotnet add package Microsoft.EntityFrameworkCore
```

For the examples below, also add a provider such as SQLite:

```text
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
```

## Automatic mapping

Add one call in `OnModelCreating`. The generated `ConfigureValueObjects` extension is emitted into your project
in the `Microsoft.EntityFrameworkCore` namespace (the same namespace as `ModelBuilder`), so no extra `using`
is required when that namespace is already imported, and it maps every value object it finds on your entities:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ConfigureValueObjects();
}
```

### Configure from DI registration (`AddDbContext`, `AddDbContextFactory`, `AddDbContextPool`)

Instead of overriding `OnModelCreating` per context, chain the generated `UseValueObjects()` extension on the
options builder when you register the context. It registers a generated `ModelCustomizer` that runs
`ConfigureValueObjects` automatically after `OnModelCreating`:

```csharp
services.AddDbContextFactory<ShopContext>(options =>
    options.UseSqlite("Data Source=shop.db").UseValueObjects());

// or:
services.AddDbContext<ShopContext>(options =>
    options.UseSqlServer(connectionString).UseValueObjects());
```

This works for `AddDbContext`, `AddDbContextFactory`, `AddDbContextPool`, and manual construction (add
`UseValueObjects()` to the options builder there too). With it, no `OnModelCreating` override is required —
the mapping applies to every context created from that registration.

What the mapping does:

- **Scalar value objects** (`[Scalar]`) map to their underlying primitive via a generated
  `ValueConverter<TSelf, TUnderlying>` + `ValueComparer`. `EmailAddress` stores as a `TEXT` column.
- **Complex value objects** (`[ValueObject]`) map as **EF Core complex types** (EF Core 8+) by default, producing
  a column per member — including nested scalar value objects (e.g. `Money.Currency` converts to its primitive).
- Complex value objects with `[ValueObject(EfMapping = EfMapping.Json)]` map to a single JSON column using the
  generated JSON converter.

## Queries — no `.Value` required

Because scalar value objects convert to their underlying primitive column, queries compare the value object
type directly and translate to SQL:

```csharp
EmailAddress email = EmailAddress.Create("demo@example.com");

var customers = await db.Customers
    .Where(c => c.Email == email)              // translates to [email] = @p
    .ToListAsync();
```

Complex value objects map as complex types, so nested members are queryable too:

```csharp
var orders = await db.Orders
    .Where(o => o.Total.Amount > 20m)                          // o.Total.Amount > 20.0
    .Where(o => o.Total.Currency == CurrencyCode.Create("USD"))
    .ToListAsync();
```

> **Note on comparing to a raw primitive literal.** EF Core translates equality against a value-converted
> property only when the other side is the value object type. `c.Email == "demo@example.com"` (comparing the
> `EmailAddress` property to a `string` literal) does **not** translate — it throws at query time. Use the value
> object type instead:
>
> ```csharp
> EmailAddress email = "demo@example.com";                 // implicit conversion
> .Where(c => c.Email == email)
>
> // or inline:
> .Where(c => c.Email == EmailAddress.Create("demo@example.com"))
> ```

## Manual control

The generator exposes per value object a nested static `Ef` class. Use it for per-property configuration instead
of (or alongside) the automatic registry:

```csharp
builder.Entity<Customer>()
    .Property(c => c.Email)
    .HasConversion(EmailAddress.Ef.Converter, EmailAddress.Ef.Comparer);
```

Complex value objects can be configured explicitly with `ComplexProperty`:

```csharp
builder.Entity<Order>()
    .ComplexProperty(o => o.Total, money =>
    {
        money.Property(m => m.Amount);
        money.Property(m => m.Currency).HasConversion(CurrencyCode.Ef.Converter, CurrencyCode.Ef.Comparer);
    });
```

## Options

### Per type

```csharp
[Scalar(GenerateEfConverter = false, GenerateEfComparer = false)]   // opt this scalar out of EF support
public readonly partial record struct InternalCode { ... }

[ValueObject(EfMapping = EfMapping.Json)]                           // map as a JSON column instead of complex type
public readonly partial record struct Audit { ... }

[ValueObject(EfMapping = EfMapping.None, GenerateEfComparer = false)] // no EF support for this type
public readonly partial record struct Notes { ... }
```

### Per assembly (`[ValueObjectDefaults]`)

Assembly-level defaults apply to every value object and can be overridden per type. This is also how you set
the complex/JSON mapping mode as the assembly default:

```csharp
[assembly: ValueObjectDefaults(EfMapping = EfMapping.Json)]
[assembly: ValueObjectDefaults(GenerateEfConverter = false, GenerateEfComparer = false)] // opt the whole assembly out
```

### MSBuild property (whole project)

Disable all Entity Framework generation for the compilation:

```xml
<PropertyGroup>
    <DisableValueObjectsEfGeneration>true</DisableValueObjectsEfGeneration>
</PropertyGroup>
```

## Diagnostics

- `VO1009` — an Entity Framework option was set explicitly but the project does not reference
  `Microsoft.EntityFrameworkCore`.
- `VO1010` — a scalar value object wraps an underlying type EF Core cannot map natively, so automatic
  conversion is skipped (map the property manually, or store it as JSON).

## Notes

- EF Core 8+ is required for complex type mapping; on older EF references, complex value objects fall back to
  no automatic mapping (use `EfMapping.Json` or configure manually).
- Value objects are immutable; EF tracks them by value like any struct/record. The generator emits a
  parameterless constructor for `[ValueObject]` types to support EF Core materialization.
- See `src/src/Sample` for a runnable EF Core (SQLite) example, and
  `src/tests/ValueObjects.UnitTests/Serialization/EntityFrameworkIntegrationTests.cs` for integration tests.