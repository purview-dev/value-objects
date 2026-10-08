# Getting Started

This guide walks through modeling DTOs and domain values with `Purview.ValueObjects`.

## 1. Reference the package

```text
dotnet add package Purview.ValueObjects
```

The package includes the runtime contracts, the source generator, the diagnostic analyzer, and code fixes for
the analyzer's diagnostics (for example `VO1001` offers **Add 'partial' modifier**).

## 2. Scalar value objects

A scalar value object wraps a single primitive value. It is the F#-style single-case union for C#.

```csharp
using Purview.ValueObjects.Serialization;

[Scalar]
public readonly partial record struct EmailAddress
{
    public string Value { get; }

    static partial void OnNormalize(ref string value) => value = value?.Trim().ToLowerInvariant()!;

    static partial void OnValidate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Email is required.", nameof(value));

        if (!value.Contains('@', StringComparison.Ordinal))
            throw new ArgumentException("Invalid email format.", nameof(value));
    }
}
```

The generator adds:

- `EmailAddress.Create(string)` – normalizes, validates, then constructs. Throws on invalid input.
- `EmailAddress.Hydrate(string)` – constructs without re-validating (for persisted data).
- `EmailAddress.TryCreate(string, out EmailAddress)` – returns `false` instead of throwing.
- `EmailAddress.Empty` – a default instance.
- Equality, comparison, `CompareTo`, `ToString`, implicit conversions, and a JSON converter.

```csharp
var email = EmailAddress.Create("  Demo@Example.COM  ");
// email.Value == "demo@example.com"

bool ok = EmailAddress.TryCreate("not-an-email", out _);
// ok == false

string json = System.Text.Json.JsonSerializer.Serialize(email);
// json == "\"demo@example.com\""
```

### Automatic vs manual underlying property

`[Scalar]` is the **manual** form: you declare the property. `[Scalar<T>]` and `[Scalar(typeof(T))]` are
the **automatic** form: the generator declares it. They are mutually exclusive for a given type —
declaring the property with an automatic form is an error (`VO1022`).

```csharp
// Automatic: the generator emits `public Guid Value { get; init; }`.
[Scalar<Guid>]                     // or [Scalar(typeof(Guid))]
public readonly partial record struct InstallationId { }

// Manual: you declare the property.
[Scalar]
public readonly partial record struct InstallationId
{
    public Guid Value { get; init; }
}
```

Use `Nullable` when the automatic property should be a nullable reference type. Nullable reference types
cannot be generic attribute arguments (`[Scalar<string?>]` does not compile — `CS8970`) or `typeof`
operands (`typeof(string?)` does not compile — `CS8639`), so express nullability on the attribute:

```csharp
[Scalar<string>(Nullable = true)]              // emits `public string? Value { get; init; }`
[Scalar(typeof(string), Nullable = true)]      // same
public readonly partial record struct Handle { }
```

Nullable value types are written directly, for example `[Scalar<int?>]` or `[Scalar(typeof(int?))]`.

A nullable scalar round-trips JSON `null`: `[Scalar<string>(Nullable = true)]` and `[Scalar<int?>]`
deserialize `null` and serialize back to `null`. A non-nullable reference scalar still rejects `null` with
a `JsonException`. Validation is yours to write, so make it null-tolerant — for a value that must be
`null` or non-whitespace:

```csharp
[Scalar<string>(Nullable = true)]
public readonly partial record struct Nickname
{
    static partial void OnValidate(string? value)
    {
        if (value is not null && string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value must be null or non-whitespace.", nameof(value));
    }
}
```

The manual form is the more explicit option when you prefer the type and nullability to appear in a
property declaration. Both forms are supported, and neither is reported as a diagnostic. Other properties
are allowed in either form; only the scalar property (and the interface's `Value` forwarding) is reserved.

## 3. Complex value objects

A complex value object wraps multiple members and validates them together.

```csharp
[ValueObject]
public readonly partial record struct Money
{
    public decimal Amount { get; }

    public CurrencyCode Currency { get; }

    partial void OnValidate(decimal amount, CurrencyCode currency)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount cannot be negative.");

        if (currency == CurrencyCode.Empty)
            throw new ArgumentException("Currency cannot be empty.", nameof(currency));
    }
}
```

## 4. Contextual value objects

When validity depends on the owning instance, implement `IContextualValueObject<TSelf, TValue, TOwner>`:

```csharp
[Scalar]
public readonly partial record struct OrderStatus
    : IContextualValueObject<OrderStatus, OrderStatusCode, Order>
{
    public OrderStatusCode Value { get; }

    public static OrderStatus Create(OrderStatusCode value, in ValueObjectContext<Order> context)
    {
        var current = context.Owner.Status.Value;
        return IsValidTransition(current, value)
            ? new(value)
            : throw new InvalidOperationException($"Invalid transition {current} -> {value}");
    }
}
```

`ValueObjectContext<TOwner>` carries the owner instance, the member name being assigned, and an optional reason.

## 5. JSON serialization

Scalar value objects serialize as their underlying value. The generator emits a `[JsonConverter]` per value object,
so they round-trip with default options. For a shared options instance, register
`ScalarJsonConverterFactory`:

```csharp
var options = new JsonSerializerOptions();
options.Converters.Add(new ScalarJsonConverterFactory());
```

`ValueObjectDeserializationMode` controls which factory deserialization uses:

- `Hydrate` (default) – reconstructs via `Hydrate`, skipping validation.
- `Strict` – reconstructs via `Create`, re-running validation.

```csharp
[Scalar(DeserializationMode = ValueObjectDeserializationMode.Strict)]
public readonly partial record struct EmailAddress
{
    // ...
}
```

## 6. Assembly-level defaults

Use `[ValueObjectDefaults]` to set generic defaults for the whole assembly. Every option that can be set on
`[Scalar]`/`[ValueObject]` can be defaulted here (`GenerateJsonConverter`, `GenerateComparable`,
`GenerateComparisonOperators`, `GenerateEnumProperties`, `GenerateImplicitFromPrimitive`,
`GenerateImplicitToPrimitive`, `GenerateEmpty`, `GenerateConstructor`, `GenerateEFConverter`,
`GenerateEFComparer`, `EFMapping`, `DeserializationMode`, and `ZodSchemaMode`):

```csharp
[assembly: ValueObjectDefaults(
    GenerateConstructor = false,
    GenerateJsonConverter = false,
    ZodSchemaMode = ZodSchemaMode.InsteadOfHooks
)]
```

Assembly defaults apply to every value object in the assembly; an option explicitly set on an individual
`[Scalar]`/`[ValueObject]` attribute always overrides it.

## 7. Validate with ZodSharp

[Purview.ZodSharp](https://www.nuget.org/packages/Purview.ZodSharp) is a C# port of Zod that can validate
value objects. Add `[ZodSchema]` (plus DataAnnotations on the underlying value) to generate a
zero-allocation validator, or build a schema for the raw value with `Z.String()`, `Z.Number()`, `Z.Enum()`:

```csharp
using System.ComponentModel.DataAnnotations;
using ZodSharp;

[Scalar]
[ZodSchema]
public readonly partial record struct EmailAddress
{
    [EmailAddress]
    [StringLength(254, MinimumLength = 3)]
    public string Value { get; }

    // ...
}

var email = EmailAddress.Create("demo@example.com");
var result = EmailAddressSchema.Validate(email);   // ValidationResult<EmailAddress>
```

Because `EmailAddress` is both `[Scalar]` and `[ZodSchema]`, the generated `Create` **also** validates the
constructed instance through `EmailAddressSchema` — `EmailAddress.Create("not-an-email")` throws a
`ZodException`. Use `ZodSchemaMode.InsteadOfHooks` on the attribute to run the schema instead of the
`OnValidate` hook.

For rules that must observe the value object rather than its `Value`, map a `[ZodRule(typeof(...))]` attribute to
a rule constrained to the value object. A rule written for the underlying value is adapted automatically (via
the generator-emitted `ScalarRuleAdapter`) when applied to a `[Scalar]` type. The rule's `Code`/`Origin` flow
through `Create` unchanged. See
[Custom rules on scalars](ZodSharp-Validation.md#custom-rules-on-scalars) and
[Reusing a normal rule for a scalar](ZodSharp-Validation.md#reusing-a-normal-rule-for-a-scalar).

In ASP.NET Core, `Purview.ZodSharp.AspNetCore` converts those `ZodException`s into standard Problem
Details responses — combine `ValueObjectDeserializationMode.Strict` with
`AddZodSharpProblemDetails()` + `UseExceptionHandler()` so invalid request bodies return
`HttpValidationProblemDetails` automatically. See the `src/src/ZodSharp.AspNetCoreSample` project.

See `ZodSharp-Validation.md` and the `src/src/ZodSharpSample` project.

## Next steps

- `Entity-Framework.md` – mapping value objects to EF Core: automatic `ConfigureValueObjects()`, keys,
  generated key values, query filters, manual control, and schema/migration notes.
- `Value-Object-Design.md` – where validation lives, the `Create`/`Hydrate` split, and how value objects sit
  in a domain model next to entities.
- `ZodSharp-Validation.md` – validating value objects with Purview.ZodSharp.
- The `src/src/Sample`, `src/src/EFDomainSample.Persistence` (domain + persistence split), and
  `src/src/ZodSharpSample` projects for runnable examples.