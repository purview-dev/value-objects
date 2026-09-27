# Purview.ValueObjects Domain + Entity Framework sample

A console sample for the shape most applications end up with: a **domain project that does not reference
Entity Framework Core**, and a **persistence project** that maps its value objects.

## Run

```text
dotnet run --project src/src/EFDomainSample.Persistence
```

## Projects

| Project | References | Notes |
| --- | --- | --- |
| `EFDomainSample.Domain` | `Purview.ValueObjects` only | Value objects (`CustomerId`, `TenantId`, `TenantKey`, `EmailAddress`) and the `Customer` entity. No Entity Framework reference, so no `EF` members and no marker interfaces are generated here. |
| `EFDomainSample.Persistence` | Entity Framework Core (SQLite) and the domain project | `SampleDbContext`, persistence records, migrations. The consuming project discovers the domain value objects from their attributes and generates the converters and the key value generator convention **into this assembly**. |

## What it shows

- **Typed identifiers**: `CustomerId`, `TenantId`, and `TenantKey` used as keys, foreign keys, and columns.
- **Generated key values**: `CustomerId` opts into `GenerateEFValueGenerator`, so an unset key receives a
  time-ordered (version 7) identifier on save, while a key the domain set is left alone. The registration
  overload selects the ordering the store compares with — the default suits SQLite, PostgreSQL, and MySQL;
  `ValueObjectKeyOrdering.SqlServer` suits a clustered SQL Server key.
- **Application-minted identifiers**: `ValueObjectSequentialGuid` is generated as a public type, so the
  application can create the identifier an entity is saved with (and read its creation time back) instead of
  leaving the key unset — see the `Identifiers the application owns are not overwritten` section of the run.
- **Column facets** on converted properties (`HasMaxLength`, `IsRequired`) and a unique index over a value
  object column.
- **Query filters** comparing value objects directly — `record.TenantId == CurrentTenantId` — with no
  `.Value` and no manual converter wiring.
- **The domain/persistence split**: `modelBuilder.ConfigureValueObjects()` plus
  `configurationBuilder.UseValueObjectKeyGenerators()` in the persistence project, and nothing in the domain
  project that mentions Entity Framework Core.
