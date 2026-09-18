using System.Text.Json;
using Purview.ValueObjects.Serialization;

namespace Purview.ValueObjects;

/// <summary>
/// End-to-end smoke tests for the standalone DTO experience: source-generated scalar and complex
/// value objects used as Entity Framework style JSON-column members, serialized via a custom
/// <see cref="JsonSerializerOptions"/> that registers <see cref="ScalarJsonConverterFactory"/>.
/// </summary>
public sealed class EntityFrameworkDtoSmokeTests
{
	static readonly JsonSerializerOptions EntityJsonOptions = CreateEntityJsonOptions();

	[Test]
	public async Task GeneratedScalar_SerializesAsUnderlyingPrimitive()
	{
		Customer customer = new()
		{
			Id = CustomerId.Hydrate(Guid.NewGuid()),
			Email = EmailAddress.Create("Demo@Example.COM"),
		};

		var json = JsonSerializer.Serialize(customer, EntityJsonOptions);

		await Assert.That(json).Contains("\"email\":\"demo@example.com\"");
	}

	[Test]
	public async Task GeneratedScalar_DeserializesBackWithValidationApplied()
	{
		Customer customer = new()
		{
			Id = CustomerId.Hydrate(Guid.NewGuid()),
			Email = EmailAddress.Create("demo@example.com"),
		};
		var json = JsonSerializer.Serialize(customer, EntityJsonOptions);

		var roundTripped = JsonSerializer.Deserialize<Customer>(json, EntityJsonOptions);

		await Assert.That(roundTripped!.Email).IsEqualTo(EmailAddress.Create("demo@example.com"));
	}

	[Test]
	public async Task GeneratedComplexValueObject_SerializesAsObjectGraph()
	{
		Order order = new()
		{
			Total = Money.Create(19.99m, CurrencyCode.Create("USD")),
			Status = OrderStatus.Hydrate(OrderStatusKind.Shipped),
		};

		var json = JsonSerializer.Serialize(order, EntityJsonOptions);

		await Assert.That(json).Contains("\"amount\":19.99");
		await Assert.That(json).Contains("\"currency\":\"USD\"");
		await Assert.That(json).Contains("\"status\":1");
	}

	[Test]
	public async Task GeneratedScalar_SerializesWithoutFactory_ViaGeneratedJsonConverter()
	{
		// The generated [JsonConverter] attribute handles scalar value objects even when the
		// ScalarJsonConverterFactory is not registered on the options.
		var email = EmailAddress.Create("Demo@Example.COM");

		var json = JsonSerializer.Serialize(email);

		await Assert.That(json).IsEqualTo("\"demo@example.com\"");
	}

	[Test]
	public async Task GeneratedScalar_ValidationRejectsInvalidValue()
	{
		await Assert.That(() => EmailAddress.Create("not-an-email")).Throws<ArgumentException>();
	}

	static JsonSerializerOptions CreateEntityJsonOptions()
	{
		JsonSerializerOptions options = new()
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			WriteIndented = false,
		};
		options.Converters.Add(new ScalarJsonConverterFactory());
		return options;
	}

	sealed class Customer
	{
		public CustomerId Id { get; set; }

		public EmailAddress Email { get; set; }
	}

	sealed class Order
	{
		public Money Total { get; set; }

		public OrderStatus Status { get; set; }
	}
}

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
			throw new ArgumentException("Email address cannot be empty.", nameof(value));
		if (!value.Contains('@', StringComparison.Ordinal))
			throw new ArgumentException("Invalid email address format.", nameof(value));
	}
}

[Scalar]
public readonly partial record struct CustomerId
{
	public Guid Value { get; }

	static partial void OnValidate(Guid value)
	{
		if (value == Guid.Empty)
			throw new ArgumentException("Customer id cannot be empty.", nameof(value));
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
