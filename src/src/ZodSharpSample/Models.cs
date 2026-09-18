using System.ComponentModel.DataAnnotations;
using Purview.ValueObjects.Serialization;
using ZodSharp;

namespace Purview.ValueObjects.ZodSharpSample;

/// <summary>
/// A scalar value object whose underlying <see cref="Value"/> is validated by both the value-object
/// pipeline (<c>OnNormalize</c>/<c>OnValidate</c>) and a source-generated ZodSharp validator
/// (<c>EmailAddressSchema</c>). The <c>[ZodSchema]</c> + DataAnnotations combination lets
/// <c>EmailAddressSchema.Validate(email)</c> validate the value object directly.
/// </summary>
[Scalar]
[ZodSchema]
readonly partial record struct EmailAddress
{
	[EmailAddress]
	[StringLength(254, MinimumLength = 3)]
	public string Value { get; }

	public string Domain => Value.Contains('@', StringComparison.Ordinal) ? Value.Split('@')[1] : string.Empty;

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

/// <summary>
/// A scalar value object validated with a ZodSharp-generated schema using
/// <c>[RegularExpression]</c> on its underlying value.
/// </summary>
[Scalar]
[ZodSchema]
readonly partial record struct CurrencyCode
{
	[RegularExpression("^[A-Z]{3}$")]
	public string Value { get; }

	static partial void OnNormalize(ref string value) => value = value?.Trim().ToUpperInvariant()!;

	static partial void OnValidate(string value)
	{
		if (string.IsNullOrWhiteSpace(value) || value.Length != 3)
			throw new ArgumentException("Currency code must be a 3-letter ISO code.", nameof(value));
	}
}

/// <summary>
/// A scalar value object whose validation is delegated entirely to the ZodSharp-generated schema:
/// <c>ZodSchemaMode.InsteadOfHooks</c> skips the <c>OnValidate</c> hook in the generated
/// <c>Create</c>, so the schema is the single gate on input.
/// </summary>
[Scalar(ZodSchemaMode = ZodSchemaMode.InsteadOfHooks)]
[ZodSchema]
readonly partial record struct PhoneNumber
{
	[RegularExpression(@"^\+?\d{7,15}$")]
	public string Value { get; }
}

enum OrderStatusKind
{
	Pending,

	Shipped,

	Delivered,
}

/// <summary>
/// A scalar value object validated with a hand-built ZodSharp schema
/// (<c>Z.Enum&lt;OrderStatusKind&gt;()</c>) rather than a generated one.
/// </summary>
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

/// <summary>
/// A complex value object. Its members are validated with hand-built ZodSharp schemas in
/// <see cref="ScalarSchemas"/> (the <c>[ZodSchema]</c> generator is used for the scalar types above).
/// </summary>
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
