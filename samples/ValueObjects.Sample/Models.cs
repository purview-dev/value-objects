using Purview.ValueObjects.Serialization;

namespace Purview.ValueObjects.Sample;

[Scalar]
public readonly partial record struct EmailAddress
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

[Scalar]
public readonly partial record struct CurrencyCode
{
	public string Value { get; }

	static partial void OnNormalize(ref string value) => value = value?.Trim().ToUpperInvariant()!;

	static partial void OnValidate(string value)
	{
		if (string.IsNullOrWhiteSpace(value) || value.Length != 3)
			throw new ArgumentException("Currency code must be a 3-letter ISO code.", nameof(value));
	}
}

[Scalar]
public readonly partial record struct OrderId
{
	public Guid Value { get; }

	static partial void OnValidate(Guid value)
	{
		if (value == Guid.Empty)
			throw new ArgumentException("Order id cannot be empty.", nameof(value));
	}
}

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

public enum OrderStatusKind
{
	Pending,
	Shipped,
	Delivered,
}

[Scalar]
public readonly partial record struct OrderStatus
{
	public OrderStatusKind Value { get; }

	static partial void OnValidate(OrderStatusKind value)
	{
		if (!Enum.IsDefined(value))
			throw new ArgumentException("Invalid order status.", nameof(value));
	}
}
