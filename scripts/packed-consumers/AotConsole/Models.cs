using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Purview.ValueObjects.Serialization;
using ZodSharp;
using ZodSharp.Rules;

namespace PackedValueObjects;

/// <summary>A scalar validated by both generators: <c>Create</c> normalizes, then runs <c>EmailAddressSchema</c>.</summary>
[Scalar]
[ZodSchema]
public readonly partial record struct EmailAddress
{
	[Required]
	[EmailAddress]
	[StringLength(254, MinimumLength = 3)]
	public string Value { get; }

	static partial void OnNormalize(ref string value) => value = value?.Trim().ToLowerInvariant()!;
}

/// <summary>A Guid scalar rejected by the built-in, type-level <c>[NonSentinel]</c> rule.</summary>
[Scalar]
[ZodSchema]
[NonSentinel(Message = "CustomerId must not be empty.")]
public readonly partial record struct CustomerId
{
	public Guid Value { get; }
}

/// <summary>A scalar validated by the value-object hook alone, with no ZodSharp involvement.</summary>
[Scalar]
public readonly partial record struct Quantity
{
	public int Value { get; }

	static partial void OnValidate(int value)
	{
		if (value is < 1 or > 100)
			throw new ArgumentOutOfRangeException(nameof(value), value, "Quantity must be between 1 and 100.");
	}
}

/// <summary>A complex value object.</summary>
[ValueObject]
public readonly partial record struct Money
{
	public decimal Amount { get; }

	public string Currency { get; }

	partial void OnValidate(decimal amount, string currency)
	{
		if (amount < 0)
			throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount cannot be negative.");

		if (currency is not { Length: 3 })
			throw new ArgumentException("Currency must be a three-letter code.", nameof(currency));
	}
}

/// <summary>A request DTO over value objects, validated by the ZodSharp generator.</summary>
[ZodSchema]
public sealed class Order
{
	[Required]
	public CustomerId? Customer { get; set; }

	// A [Required] nullable member of a string-backed scalar: the generated null test must not bind the scalar's
	// operator ==(string?, EmailAddress), and a present address is validated by EmailAddress's own schema.
	[Required]
	public EmailAddress? ContactEmail { get; set; }

	[MinCount(1)]
	public List<OrderLine> Lines { get; set; } = [];
}

[ZodSchema]
public sealed class OrderLine
{
	[Required]
	[RegularExpression("^[A-Z]{3}-[0-9]{4}$")]
	public string? Sku { get; set; }

	public Quantity Quantity { get; set; }

	public Money Price { get; set; }
}

[JsonSerializable(typeof(Order))]
[JsonSerializable(typeof(EmailAddress))]
[JsonSerializable(typeof(Money))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public sealed partial class ConsumerJsonContext : JsonSerializerContext { }
