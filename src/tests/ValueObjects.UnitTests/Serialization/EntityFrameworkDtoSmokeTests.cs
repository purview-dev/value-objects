using System.Text.Json;

namespace Purview.ValueObjects.Serialization;

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
