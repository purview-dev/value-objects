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

The generated integration is compiled and tested against EF Core **7, 9, and 10** (EF Core 7 needs `net8.0`),
so version-specific API differences are resolved by the generator rather than surfacing in your build. EF Core
7 has no complex-type mapping, which is the one feature difference: a complex value object reports `VO1019`
there. See [Notes](#notes) for the full version matrix.

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

#### Value objects in assemblies that do not reference EF Core

The declaring assembly does not need to reference `Microsoft.EntityFrameworkCore`. A domain project that must
stay free of Entity Framework dependencies (for example a model assembly shipped as a WASM contract) emits no
`EF` members and no marker interfaces. The consumer still discovers those value objects — from their
`[Scalar]`/`[ValueObject]` attributes — and generates the converters and comparers **inline** in its own
registry, so all EF code is produced in the consuming project:

```csharp
// Domain/Models assembly (references Purview.ValueObjects only — no Entity Framework):
[Scalar]
public readonly partial record struct TenantId { public Guid Value { get; }

// Persistence assembly (references Microsoft.EntityFrameworkCore + Domain/Models):
services.AddDbContextFactory<ShopContext>(options =>
    options.UseSqlite("Data Source=shop.db").UseValueObjects());   // maps Domain/Models.TenantId inline
```

The inline conversion mirrors what the per-type `EF` members emit: scalars convert via
`vo => vo.Value` / `T.Hydrate(v)` for the provider-to-model path, JSON-mapped complex value objects serialize
to a string column, and complex-type-mapped value objects map as EF Core complex types. EF uses the hydrate
path even when the value object's `Create(...)` factory is strict, so query parameterization and persistence
remain safe for provider values such as `Guid`, strings, enums, and other EF-mappable primitives. Both paths
build their converter from the generated `ValueObjectConverter<TSelf, TProvider>`, which accepts either the
value object or an already provider-shaped value.

> **Discovery.** Referenced value objects are discovered through their marker interfaces when the declaring
> assembly references EF Core, or through their attributes when it does not. A complex value object is
> discovered when it opted into a mapping (complex-type or JSON) or a comparer, including a complex type with
> the default `ComplexType` mapping and `GenerateEFComparer = false`. Only a complex type that opted out
> entirely (`EFMapping = None` and `GenerateEFComparer = false`) is not auto-discovered across assemblies —
> configure it manually on the entity if you need it mapped.

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

- **Scalar value objects** (`[Scalar]`) map to their underlying primitive via a per-value-object generated
  converter class (a `ValueObjectConverter<TSelf, TProvider>`, exposed as
  `{Type}.EF.Converter`) + `ValueComparer`. The provider-to-model conversion uses `Hydrate(...)` so raw provider
  values can be materialized safely from queries and persisted rows, and the converter accepts either the value
  object or an already provider-shaped value so comparisons against the underlying primitive translate. An
  enum-backed scalar converts through the enum's **integral** type (for example
  `ValueConverter<OrderStatus, int>`), because leaving the enum as the provider type makes Entity Framework Core
  compose its own enum-to-number converter with the generated one — and the composite loses the provider
  tolerance. `EmailAddress` stores as a `TEXT` column.
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

> **Comparing to a raw primitive.** A query may compare a scalar value object property to either the value
> object **or** its raw underlying value — both translate:
>
> ```csharp
> EmailAddress email = "demo@example.com";                    // value object (implicit conversion)
> .Where(c => c.Email == email)
>
> .Where(c => c.Email == "demo@example.com")                  // raw underlying string
> .Where(m => m.Id == guid)                                   // raw underlying Guid
> ```
>
> This works because every generated converter is built from a per-value-object `ValueObjectConverter` type that
> accepts either shape (enum-backed scalars convert through the enum's integral type). Entity Framework Core
> hands the raw provider value to a converted property's converter in this case, and its built-in converter
> coerces that value with `Convert.ChangeType`, which throws for provider types that do not implement
> `IConvertible` (`Guid`, `DateTimeOffset`, `TimeSpan`, `DateOnly`, `TimeOnly`) or cannot be converted at all
> (strings). See [dotnet/efcore#32030](https://github.com/dotnet/efcore/issues/32030).
>
> **Compiled models.** Entity Framework Core's design-time generator rebuilds a converter as
> `new ValueConverter<TSelf, TProvider>(…)` — the built-in type — unless the converter exposes a
> `JsonValueReaderWriter`-taking constructor and a `JsonReaderWriter` property. Every generated converter does,
> so a compiled model (`dotnet ef dbcontext optimize`) keeps the same provider tolerance. That detection is an
> undocumented Entity Framework Core implementation detail: if it ever changes, compiled models silently fall
> back to the built-in converter, and only raw-primitive comparisons are affected.

## Keys, foreign keys, and indexes

A value object key needs no special handling: the generated converter maps the value object to its
primitive column, so a key or foreign key typed as a value object persists as that primitive. Convention
then makes `{Type}Id` the primary key, exactly as it would for a `Guid` or `string`.

```csharp
sealed class Customer
{
    public CustomerId Id { get; set; }          // primary key, stored as uniqueidentifier
    public TenantId TenantId { get; set; }      // foreign key to Tenant.Id
    public Tenant Tenant { get; set; } = default!;
}

sealed class Tenant
{
    public TenantId Id { get; set; }
    public TenantKey Key { get; set; }          // a scalar value object stored as a single column
}
```

Column facets are configured on the value object property and apply to the converted column, so keep
configuring them the way you would on a primitive:

```csharp
public sealed class TenantConfiguration : IEntityTypeConfiguration<TenantEntity>
{
    public void Configure(EntityTypeBuilder<TenantEntity> builder)
    {
        builder.Property(entity => entity.Id).ValueGeneratedNever();       // when the domain owns the key
        builder.Property(entity => entity.Key).HasMaxLength(100).IsRequired();
        builder.HasIndex(entity => new { entity.TenantId, entity.Key }).IsUnique();
    }
}
```

Because the converter is expression-based, indexes, unique constraints, and comparisons over value object
properties behave like their primitive equivalents — including in composite indexes and `HasQueryFilter`.

## Generating key values

A Guid-backed scalar value object can generate its own key values. Opt in per type, or once for the whole
assembly:

```csharp
[Scalar(GenerateEFValueGenerator = true)]
public readonly partial record struct CustomerId
{
    public Guid Value { get; }
}

// or: [assembly: ValueObjectDefaults(GenerateEFValueGenerator = true)]
```

The generator then emits an `EF.ValueGeneratorFactory` on the value object and registers every opted-in
type in the generated `ValueObjectKeyValueGeneratorConvention`. Register that convention from
`ConfigureConventions` — one line, and no per-entity configuration:

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
{
    base.ConfigureConventions(configurationBuilder);
    configurationBuilder.UseValueObjectKeyGenerators();
}

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ConfigureValueObjects();
}
```

What this gives you:

- An **unset** key (still `Guid.Empty`) is assigned a time-ordered identifier when the entity is added, so
  keys stay unique without a database round trip and sort by creation time in stores that compare identifiers
  byte by byte.
- A key the **domain already set** is never touched: Entity Framework Core only invokes a generator while
  the property holds its CLR default.
- The convention runs at the lowest configuration source, so an entity configuration that owns its keys —
  `ValueGeneratedNever()`, an explicitly configured generator, or a store-generated default — always wins.

The generated identifier is a time-ordered UUID created without a database round trip, using only base
Entity Framework Core APIs, so the convention holds for every provider. Which bytes carry the timestamp
depends on the store, and you choose that with the registration overload.

### Key ordering

Where a store compares the identifier's sixteen bytes in order — PostgreSQL, SQLite, MySQL, and non-clustered
SQL Server keys — version 7 ordering is what you want. SQL Server's `uniqueidentifier` compares the **trailing
six bytes first**, so plain version 7 values are effectively random there and a clustered key fragments. Pass
the ordering the store needs:

```csharp
// Default: version 7, ascending in plain byte order.
configurationBuilder.UseValueObjectKeyGenerators();

// SQL Server: the timestamp moves into the trailing six bytes, which SQL Server compares first.
configurationBuilder.UseValueObjectKeyGenerators(ValueObjectKeyOrdering.SqlServer);
```

| Ordering | Timestamp location | Ascends by creation time in | Well-formed version 7 UUID |
| --- | --- | --- | --- |
| `ValueObjectKeyOrdering.UuidV7` (default) | bytes 0-5 | PostgreSQL, SQLite, MySQL, and non-clustered SQL Server keys | Yes |
| `ValueObjectKeyOrdering.SqlServer` | bytes 10-15 | SQL Server's `uniqueidentifier` ordering | No — a version 4 UUID |

Both orderings produce unique identifiers in process, keep the domain's own value when it set one, and start
from a random value so nothing leaks about the sequence.

The generated `ValueObjectSequentialGuid` helper is **public** in the `Microsoft.EntityFrameworkCore`
namespace, so application code — including code in another assembly — can mint the identifier it wants an
entity to carry, before the round trip that would otherwise assign one:

```csharp
// The same value the convention would have generated, created where the domain needs it.
var customerId = CustomerId.Create(ValueObjectSequentialGuid.NewGuid());
var customer = Customer.Create(customerId, tenantId, email, "Contoso");
```

Because Entity Framework Core only invokes a generator while the property still holds its CLR default, the key
the application set is persisted as-is. The helper exposes:

| Member | Purpose |
| --- | --- |
| `NewGuid()` / `NewGuid(DateTimeOffset)` | Creates a version 7 identifier, from now or a given creation time. |
| `NewSqlServerGuid()` / `NewSqlServerGuid(DateTimeOffset)` | Creates a SQL Server-ordered identifier. |
| `TryGetTimestamp(Guid, out DateTimeOffset)` | Reads the creation time out of a version 7 identifier; false for any other shape. |
| `TryGetSqlServerTimestamp(Guid, out DateTimeOffset)` | Reads the creation time out of a SQL Server-ordered identifier. |
| `MinSqlServerGuidFor(DateTimeOffset)` / `MaxSqlServerGuidFor(DateTimeOffset)` | Inclusive bounds for a creation time, so `id >= MinSqlServerGuidFor(t) && id <= MaxSqlServerGuidFor(t)` is an index seek. |

Value generation is supported for **Guid-backed** scalars only, and requires the Entity Framework
converter. Requesting it anywhere else reports `VO1021`. A value object declared in an assembly that does
not reference Entity Framework Core still gets a generator: the consuming project emits it alongside the
inline converters.

## Query filters and translated predicates

Tenant and soft-delete filters compare value objects directly, because each converted property keeps a
translatable converter:

```csharp
modelBuilder.Entity<Invoice>().HasQueryFilter(invoice => invoice.TenantId == currentTenant.TenantId);
```

The same applies to raw primitives — a filter or predicate may compare the value object to the underlying
value, because `Guid.Empty`, `"USD"`, or an enum member converts implicitly:

```csharp
modelBuilder.Entity<Tenant>().HasQueryFilter(tenant => tenant.Id == Platform.SystemTenantId);
```

## Schema and migrations

- A scalar value object is a **single column** of its provider primitive, so `dotnet ef migrations add`
  sees the primitive: renaming the value object's property does not change the schema, and changing the
  underlying type is a column type change you review like any other.
- A complex value object mapped as a complex type is a **set of columns** named after its members, and one
  mapped with `EFMapping = Json` is a **single JSON column**. Moving a value object between those shapes is
  a schema change: add a migration and consider the data path (a JSON column usually needs a data migration
  to reshape existing values).
- Multi-provider repositories keep one migration set per provider. The generated converters and the key
  generator convention are provider-independent, so the same model works for SQL Server, PostgreSQL, and
  SQLite; only the primitive column types differ.
- Design-time factories (`IDesignTimeDbContextFactory<TContext>`) and model-cache keys that depend on
  runtime state (for example a query filter built from the current tenant) must be applied consistently,
  because a cached model is reused for every context instance created the same way.


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
- `VO1016` — a value object member has a setter. Value objects must be immutable; declare the member
  get-only or init-only.
- `VO1017` — a complex value object maps to a JSON column while JSON converter generation is disabled, so
  the column content would be produced by reflection serialization and can differ from the value object's
  own JSON contract.
- `VO1018` — a complex value object maps as an Entity Framework Core complex type but a member cannot be
  converted by the generated mapping (a collection, or a type that is neither a mappable primitive, a
  string, an enum, nor a value object with Entity Framework support). Map it manually or use
  `EFMapping = Json`.
- `VO1019` — a complex value object maps as a complex type but the project's Entity Framework Core version
  is older than 8, so no Entity Framework mapping is generated. Use `EFMapping = Json` or configure the
  property manually.
- `VO1021` — `GenerateEFValueGenerator` was requested for a value object that is not Guid-backed, or whose
  Entity Framework converter is disabled; no value generator is emitted.

## Notes

- Only `[Scalar]`/`[ValueObject]` types are given a conversion; a plain `enum` property keeps Entity Framework
  Core's own enum mapping untouched.
- EF Core 8+ is required for **complex type mapping**. An EF Core 7 reference set reports `VO1019` and leaves
  complex value objects unmapped; use `EFMapping = Json` (a value converter, so it works on every version) or
  configure the property manually. The complex-type block in the registry and the compiled-model members the
  generated converters expose (`JsonReaderWriter`) are EF Core 8+ only as well. Everything else the generator
  emits — converters, the key value generator, the convention, and `ValueObjectSequentialGuid` — works on
  EF Core 7 and later.
- A generated key value generator is typed as the **value object**, not as its provider value, because Entity
  Framework Core assigns what a generator returns straight to the property. Application code that mints keys
  before `SaveChanges` calls the generated `ValueObjectSequentialGuid` helper, which is public in the
  `Microsoft.EntityFrameworkCore` namespace and produces the same values the convention would assign.
- The generated code is compiled against EF Core 7, 9, and 10 in
  `src/tests/ValueObjects.EFCompatibility.IntegrationTests`, so version-specific API shifts (for example
  `ValueGeneratorFactory.Create`'s second parameter changing to `ITypeBase`) are caught by the build rather
  than by a consumer.
- Value objects are immutable; EF tracks them by value like any struct/record. The generator emits a
  parameterless constructor for `[ValueObject]` types to support EF Core materialization.
- **Materialization is a replay path.** Entity Framework Core rebuilds a value object with `Hydrate(...)`,
  so neither `OnValidate` nor a ZodSharp schema runs when a row is read — the same guarantee as
  `ValueObjectDeserializationMode.Hydrate`. `ValueObjectDeserializationMode.Strict` applies to the JSON wire
  format, not to the database: validate before persisting, or enforce the invariant in the schema.
- See `src/src/Sample` for a runnable EF Core (SQLite) example, and
  `src/tests/ValueObjects.IntegrationTests/Serialization/EntityFrameworkIntegrationTests.cs` for integration tests.
