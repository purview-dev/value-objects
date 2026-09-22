# Entity Framework

`Purview.ValueObjects` integrates with Entity Framework Core by source-generating the mapping members into your
project **when** `Microsoft.EntityFrameworkCore` is referenced. Scalar value objects convert to their underlying
primitive column; complex value objects map as EF Core complex types (EF Core 8+) or JSON columns.

The runtime package stays free of Entity Framework dependencies: all Entity Framework code is generated into the
consuming project, and everything below is opt-in per feature with an opt-out hierarchy
(`DisableValueObjectsEFGeneration` MSBuild property → assembly defaults → per-type options).

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

### Value objects in referenced assemblies

The mapping is not limited to value objects declared in the same project. When a referenced assembly (for
example a shared domain models project) references `Microsoft.EntityFrameworkCore`, the generator emits each
of its value objects an `EF` nested class and an `IEFScalarValueObject`/`IEFComplexValueObject` marker
interface. Your project's generated registry discovers those markers and maps the shared value objects just
like locally-declared ones — so `EmailAddress` from a `SharedModels` assembly is automatically converted on
your entities:

```csharp
// SharedModels assembly (references Microsoft.EntityFrameworkCore):
[Scalar]
public readonly partial record struct EmailAddress { public string Value { get; } }

// Consumer assembly (references Microsoft.EntityFrameworkCore + SharedModels):
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ConfigureValueObjects();   // maps SharedModels.EmailAddress too
}
```

A provider assembly that only *defines* value objects (and does not own any `DbContext`) can opt out of
emitting its own registry so consumer projects aren't affected by duplicate `ValueObjectEFExtensions`/
`ValueObjectModelCustomizer` types in the `Microsoft.EntityFrameworkCore` namespace:

```xml
<PropertyGroup>
    <!-- SharedModels: emit per-type EF members + markers, but not the assembly-level registry. -->
    <DisableValueObjectsEFRegistry>true</DisableValueObjectsEFRegistry>
</PropertyGroup>
```

> **Limitation.** Referenced value objects are discovered through their marker interfaces. A complex value
> object in another assembly is only discovered when it emitted at least one EF member (a comparer or a JSON
> column converter); a complex type with `EFMapping` set but both `GenerateEFComparer = false` and no JSON
> mapping is not auto-discovered across assemblies — configure it manually on the entity.

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
- Complex value objects with `[ValueObject(EFMapping = EntityFrameworkMapping.Json)]` map to a single JSON column using the
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

The generator exposes per value object a nested static `EF` class. Use it for per-property configuration instead
of (or alongside) the automatic registry:

```csharp
builder.Entity<Customer>()
    .Property(c => c.Email)
    .HasConversion(EmailAddress.EF.Converter, EmailAddress.EF.Comparer);
```

Complex value objects can be configured explicitly with `ComplexProperty`:

```csharp
builder.Entity<Order>()
    .ComplexProperty(o => o.Total, money =>
    {
        money.Property(m => m.Amount);
        money.Property(m => m.Currency).HasConversion(CurrencyCode.EF.Converter, CurrencyCode.EF.Comparer);
    });
```

## Options

### Per type

```csharp
[Scalar(GenerateEFConverter = false, GenerateEFComparer = false)]   // opt this scalar out of EF support
public readonly partial record struct InternalCode { ... }

[ValueObject(EFMapping = EntityFrameworkMapping.Json)]                           // map as a JSON column instead of complex type
public readonly partial record struct Audit { ... }

[ValueObject(EFMapping = EntityFrameworkMapping.None, GenerateEFComparer = false)] // no EF support for this type
public readonly partial record struct Notes { ... }
```

### Per assembly (`[ValueObjectDefaults]`)

Assembly-level defaults apply to every value object and can be overridden per type. This is also how you set
the complex/JSON mapping mode as the assembly default:

```csharp
[assembly: ValueObjectDefaults(EFMapping = EntityFrameworkMapping.Json)]
[assembly: ValueObjectDefaults(GenerateEFConverter = false, GenerateEFComparer = false)] // opt the whole assembly out
```

### MSBuild property (whole project)

Disable all Entity Framework generation for the compilation:

```xml
<PropertyGroup>
    <DisableValueObjectsEFGeneration>true</DisableValueObjectsEFGeneration>
</PropertyGroup>
```

Disable only the assembly-level registry (keeping per-type `EF` members and marker interfaces) — useful for
value-object provider assemblies referenced by EF consumers:

```xml
<PropertyGroup>
    <DisableValueObjectsEFRegistry>true</DisableValueObjectsEFRegistry>
</PropertyGroup>
```

## Diagnostics

- `VO1009` — an Entity Framework option was set explicitly but the project does not reference
  `Microsoft.EntityFrameworkCore`.
- `VO1010` — a scalar value object wraps an underlying type EF Core cannot map natively, so automatic
  conversion is skipped (map the property manually, or store it as JSON).

## Notes

- EF Core 8+ is required for complex type mapping; on older EF references, complex value objects fall back to
  no automatic mapping (use `EntityFrameworkMapping.Json` or configure manually).
- Value objects are immutable; EF tracks them by value like any struct/record. The generator emits a
  parameterless constructor for `[ValueObject]` types to support EF Core materialization.
- See `src/src/Sample` for a runnable EF Core (SQLite) example, and
  `src/tests/ValueObjects.IntegrationTests/Serialization/EntityFrameworkIntegrationTests.cs` for integration tests.