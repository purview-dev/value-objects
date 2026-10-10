# Value Object Design

## The `Create` / `Hydrate` split

Every value object exposes two static factories:

- `Create(value)` – the strict creation path. It runs `OnNormalize` then `OnValidate`, then constructs. Callers use
  this for command/input data.
- `Hydrate(value)` – the reconstruction path. It constructs without re-validating. Use this for persisted state,
  replay, and deserialization where validation has already happened.

`TryCreate(value, out result)` wraps `Create` and returns `false` (instead of throwing) when validation fails.

## Normalization

`OnNormalize(ref T value)` transforms the raw input before validation. Common uses:

- trimming whitespace
- casing canonicalization (email, currency codes)
- stripping separators

Normalization is deterministic and runs on every `Create`.

### Built-in string normalization

Trimming and casing are common enough to be configured rather than hand-written. On a string-backed
scalar, set `Trim` and/or `Casing`; on a complex `[ValueObject]`, annotate the string member with
`[StringNormalize]`:

```csharp
[Scalar<string>(Trim = true, Casing = StringCasing.LowerInvariant)]
public readonly partial record struct EmailAddress { }

[ValueObject]
public readonly partial record struct Contact
{
    [StringNormalize(Trim = true)]
    public string Email { get; init; }

    public string Name { get; init; }
}
```

The generated `Create` applies the trim first, then the casing, before validation. `Hydrate` is never
normalized, matching the hand-written hook. The option is only meaningful for `string` values; setting it
on any other scalar or member is reported (`VO1024`) and ignored. The option is applied by the generated
`Create`, so a hand-written `OnNormalize` hook or a hand-written `Create` takes precedence: a configured
option alongside either is reported (`VO1023`) and ignored.

An assembly-level default applies to every string-backed scalar and every string member of a
`[ValueObject]`, and can be overridden per type or per member:

```csharp
[assembly: ValueObjectDefaults(Trim = true, Casing = StringCasing.LowerInvariant)]
```

Casing uses the invariant culture only, because a value object's canonical form must not depend on the
current thread culture.

## Validation

`OnValidate(T value)` (scalar) or `partial void OnValidate(...)` (complex) enforces invariants and throws
domain-appropriate exceptions. Keep validation pure and deterministic; it must not perform I/O.

## Where validation lives

| Concern | Location |
| --- | --- |
| Primitive format and canonicalization | `OnNormalize` / `OnValidate` in the value object |
| Cross-field invariants | `partial void OnValidate(...)` in a `[ValueObject]` |
| Owner/state-machine transitions | contextual `Create(TValue, in ValueObjectContext<TOwner>)` |
| External schema rules / DTO validation | Purview.ZodSharp schemas (see `ZodSharp-Validation.md`) |
| Per-scalar custom rules (ZodSharp) | `[ZodRule]`-mapped attribute closed with the value object (see `ZodSharp-Validation.md`) |

Use the value object hooks for invariants that must hold for every construction path. Use ZodSharp when you need
schema-driven validation (DataAnnotations-based `[ZodSchema]` validators, hand-built `Z.*` schemas, or DTO
validation before mapping to value objects).

When a value object is annotated with both `[Scalar]`/`[ValueObject]` and `[ZodSchema]`, the generator runs the
ZodSharp schema automatically inside `Create` (construct → `{Type}Schema.Validate(instance)` → throw
`ZodException` on failure). `ZodSchemaMode` controls whether the `OnValidate` hook also runs (`InAdditionToHooks`,
the default) or is replaced (`InsteadOfHooks`); `OnNormalize` always runs.

## Deserialization modes

`[Scalar]` and `[ValueObject]` default to `ValueObjectDeserializationMode.Hydrate`, so JSON reads do not re-validate.
Use `Strict` when round-trip fidelity requires re-running validation on read.

## Standard interfaces

A scalar value object wraps one value, so it mirrors that value's standard interfaces and behaves like it in
generic code — equality, sorting, formatting, and parsing:

| Interface | Mirrored when | Forwarded member |
| --- | --- | --- |
| `IEquatable<TValue>` | the underlying type implements it | `Equals(TValue)` |
| `IFormattable` and `ToString(string?)` | the underlying type implements/declares them | `ToString(format[, provider])` |
| `ISpanFormattable` / `IUtf8SpanFormattable` | the underlying type implements them | `TryFormat` |
| `IParsable<T>` / `ISpanParsable<T>` / `IUtf8SpanParsable<T>` | the underlying type implements them | `Parse` / `TryParse` |

`Parse` is a strict creation path (`Create`, so normalization and validation run); `TryParse` returns `false`
through `TryCreate`. Detection is per type and per target framework, so a scalar only gets the interfaces its
underlying type actually implements (`string` is parsable but not span-formattable; an enum is span-formattable
but not parsable), and an interface is skipped when you declare one of its members yourself.

The value object always implements `IComparable<TSelf>` and `IComparable` (and a scalar always exposes
`CompareTo(TValue)`) because the value-object contract requires it. Numeric and arithmetic interfaces,
`IConvertible`, and collection interfaces are intentionally not mirrored.

## Contextual value objects

`IContextualValueObject<TSelf, TValue, TOwner>` lets a value object validate against the owning instance:

```csharp
public static OrderStatus Create(OrderStatusCode value, in ValueObjectContext<Order> context) { ... }
```

`ValueObjectContext<TOwner>` provides `Owner`, `MemberName`, and an optional `Reason`. Keep the contextual
`Create` deterministic and side-effect free.

## Queryability

Primitive scalar values are the most query-friendly shape for database filters. Scalar values that wrap complex CLR
types preserve invariants and serialization, but deep predicates through `.Value` may not translate to SQL in all
providers. If you need deep filtering, expose a separately mapped mirror property derived from canonical state.

## Value objects in a domain model

Value objects carry identity and invariants; entities and aggregates carry lifecycle. The two compose, and
the split that keeps the dependency graph clean is:

| Project | References | Contains |
| --- | --- | --- |
| Domain (e.g. `MyApp.Core`) | `Purview.ValueObjects`, `Purview.ZodSharp` | Value objects, entities/aggregates, domain services. **No Entity Framework.** |
| Persistence (e.g. `MyApp.Persistence`) | Entity Framework Core, the domain project | `DbContext`, entity POCOs, `ConfigureValueObjects()`, migrations |

A domain project that does not reference Entity Framework gets no `EF` members and no marker interfaces.
That is intentional: the consuming project discovers its value objects from their attributes and generates
the converters **into the persistence assembly**, so no Entity Framework code leaks into the domain.

```csharp
// Domain project — no Entity Framework reference.
[Scalar]
public readonly partial record struct CustomerId
{
    public Guid Value { get; }
}

public sealed class Customer
{
    Customer(CustomerId id, EmailAddress email) => (Id, Email) = (id, email);

    public CustomerId Id { get; }

    public EmailAddress Email { get; private set; }

    public static Customer Create(CustomerId id, EmailAddress email) => new(id, email);
}
```

**Typed identifiers.** Every identity is a scalar value object, so a `CustomerId` can never be passed where
an `OrderId` is expected. Where the identifier is generated for you, opt the type into Entity Framework key
value generation (see `Entity-Framework.md`) and keep the domain free of identifier plumbing; where the
domain owns the identifier, create it in the factory and keep `ValueGeneratedNever()` on the entity. Which
bytes the generated identifier orders by is a store concern, so the ordering is chosen where the convention is
registered — in the persistence project, not on the value object.

**Entities next to value objects.** Entities are ordinary classes with a private constructor and a static
factory that validates, and they hold value objects rather than primitives. An entity's `Create` is a
command boundary; its mutation methods enforce the invariants that span members.

## Choosing a persistence shape

| Shape | Use when | Notes |
| --- | --- | --- |
| Scalar value object → single column (default) | The value wraps one primitive (ids, codes, emails). | Query-friendly: predicates compare the value object directly. |
| `[ValueObject]` → EF complex type (default) | A small, fixed group of members that belongs to one row. | One column per member; nested scalars convert; not a key. |
| `[ValueObject(EFMapping = Json)]` | The group is wide, optional, or does not need to be queried by member. | One JSON column; content follows the value object's JSON contract. |
| Flat columns on the entity | The members participate in keys, unique constraints, or frequent predicates. | Map them individually and compose the value object in a mapper. |

Identity that spans two or more members (for example "provider connection + external id") is awkward as a
complex type: complex types cannot be keys. Either flatten the members into the entity and put a unique
index over them, or store the value object as a JSON column and index the derived columns you actually
query.

## Failure contract

| Path | Behavior |
| --- | --- |
| `Create(...)` | Throws on invalid input: the hook's exception, or a `ZodException` when a ZodSharp schema is generated for the type. |
| `TryCreate(...)` | Returns `false` instead of throwing. |
| `Hydrate(...)` | Never validates. Persistence, replay, and deserialization use this path. |

A ZodSharp `ZodException` carries one or more `ValidationError` entries with a code, message, path, and an
optional structured origin, so the same error codes you use in hooks (`ErrorFactory`-style constants) — or
that a custom `[ZodRule]`-mapped rule reports — flow to an ASP.NET Core Problem Details response when
`Purview.ZodSharp.AspNetCore` is registered. See `ZodSharp-Validation.md`.