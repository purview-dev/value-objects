# Getting Started

This guide walks through modeling DTOs and domain values with `Purview.ValueObjects`.

## 1. Reference the package

```text
dotnet add package Purview.ValueObjects
```

The package includes the runtime contracts, the source generator, and the diagnostic analyzer.

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

Use `[ValueObjectDefaults]` to set defaults for the whole assembly:

```csharp
[assembly: ValueObjectDefaults(GenerateConstructor = false)]
```

Individual attributes override assembly defaults.

## Next steps

- `Entity-Framework.md` – mapping value objects to EF JSON columns.
- `Value-Object-Design.md` – where validation lives and the `Create`/`Hydrate` split.
- The `samples/` folder for runnable examples.