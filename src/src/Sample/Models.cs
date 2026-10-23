using Purview.ValueObjects.Serialization;

namespace Purview.ValueObjects.Sample;

[Scalar]
readonly partial record struct EmailAddress
{
	public string Value { get; }

	public string Domain => Value.Split('@')[1];

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase")]
	static partial void OnNormalize(ref string value) => value = value?.Trim().ToLowerInvariant()!;

	static partial void OnValidate(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
			throw new ArgumentException("Email is required.", nameof(value));

		if (!value.Contains('@', StringComparison.Ordinal))
			throw new ArgumentException("Invalid email format.", nameof(value));
	}
}

// The built-in string normalization replaces a hand-written OnNormalize hook.
[Scalar(Trim = true, Casing = StringCasing.UpperInvariant)]
readonly partial record struct CurrencyCode
{
	public string Value { get; }

	static partial void OnValidate(string value)
	{
		if (string.IsNullOrWhiteSpace(value) || value.Length != 3)
			throw new ArgumentException("Currency code must be a 3-letter ISO code.", nameof(value));
	}
}

[Scalar]
readonly partial record struct OrderId
{
	public Guid Value { get; }

	static partial void OnValidate(Guid value)
	{
		if (value == Guid.Empty)
			throw new ArgumentException("Order id cannot be empty.", nameof(value));
	}
}

// The automatic form: the generator declares `public Guid Value { get; init; }`.
[Scalar<Guid>]
readonly partial record struct CustomerId
{
	static partial void OnValidate(Guid value)
	{
		if (value == Guid.Empty)
			throw new ArgumentException("Customer id cannot be empty.", nameof(value));
	}
}

// A nullable reference scalar: the generator declares `public string? Value { get; init; }`.
// `Null()` is the domain value for "explicitly null" and is what a nullable column stores as SQL NULL.
[Scalar<string>(Nullable = true)]
readonly partial record struct NullableNickname
{
	// Boundary mapping to the non-nullable persistence scalar. The explicit cast on the null branch is
	// required: a bare `null` would otherwise convert through the implicit creation operator and become
	// a populated scalar whose Value is null.
	public static explicit operator NullableNickname(NonNullableNickname? value) =>
		value is null ? Null() : Hydrate(value.Value.Value);

	public static explicit operator NonNullableNickname?(NullableNickname value) =>
		value.Value is null ? (NonNullableNickname?)null : NonNullableNickname.Create(value.Value);
}

// The non-nullable persistence counterpart. Create trims and lowercases; Hydrate replays as-is.
[Scalar<string>(Trim = true, Casing = StringCasing.LowerInvariant)]
readonly partial record struct NonNullableNickname { }

[Scalar<DateTimeOffset>(Nullable = true)]
readonly partial record struct NullableTimestamp { }

[Scalar<DateTimeOffset>]
readonly partial record struct NonNullableTimestamp { }

[Scalar<int?>]
readonly partial record struct NullableQuantity { }

[Scalar<int>]
readonly partial record struct NonNullableQuantity { }

[ValueObject]
readonly partial record struct Money
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

enum OrderStatusKind
{
	Pending,
	Shipped,
	Delivered,
}

[Scalar]
readonly partial record struct OrderStatus
{
	public OrderStatusKind Value { get; }

	static partial void OnValidate(OrderStatusKind value)
	{
		if (!Enum.IsDefined(value))
			throw new ArgumentException("Invalid order status.", nameof(value));
	}
}

sealed class Order
{
	public OrderId Id { get; set; }

	public EmailAddress CustomerEmail { get; set; }

	public Money Total { get; set; }

	public OrderStatus Status { get; set; }

	// The direct nullable-scalar shape: the column is nullable because the CLR property is T?.
	public NullableNickname? Nickname { get; set; }

	// The two-scalar shape: a non-nullable scalar behind a nullable property. The DTO keeps
	// NullableNickname and maps at the boundary.
	public NonNullableNickname? PreferredName { get; set; }

	public NonNullableTimestamp? DeliveredAt { get; set; }

	public NonNullableQuantity? Quantity { get; set; }
}

// Complex members configure normalization with [StringNormalize] instead of an OnNormalize hook.
[ValueObject]
readonly partial record struct ShippingAddress
{
	[StringNormalize(Trim = true)]
	public string Line1 { get; }

	[StringNormalize(Trim = true)]
	public string City { get; }
}
