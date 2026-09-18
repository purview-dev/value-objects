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

Use the value object hooks for invariants that must hold for every construction path. Use ZodSharp when you need
schema-driven validation (DataAnnotations-based `[ZodSchema]` validators, hand-built `Z.*` schemas, or DTO
validation before mapping to value objects).

## Deserialization modes

`[Scalar]` and `[ValueObject]` default to `ValueObjectDeserializationMode.Hydrate`, so JSON reads do not re-validate.
Use `Strict` when round-trip fidelity requires re-running validation on read.

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