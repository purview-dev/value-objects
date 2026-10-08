using System.Text.Json;

namespace Purview.ValueObjects.Serialization;

public sealed class ScalarJsonConverterFactoryTests
{
	[Test]
	public async Task Deserialize_DefaultScalarMode_UsesHydrate()
	{
		var options = CreateOptions();
		var value = JsonSerializer.Deserialize<HydratingEmailAddress>("\"not-an-email\"", options);

		await Assert.That(value.Value).IsEqualTo("not-an-email");
	}

	[Test]
	public async Task Deserialize_StrictScalarMode_UsesCreate()
	{
		// The validation failure surfaces as JsonException, with the factory's own exception as the inner
		// one. It used to escape as a bare ArgumentException, which ASP.NET Core treats as an unhandled
		// exception — a 500 rather than a 400, and with no JSON path identifying the offending value.
		var options = CreateOptions();

		var exception = Assert.Throws<JsonException>(() =>
			JsonSerializer.Deserialize<StrictEmailAddress>("\"not-an-email\"", options)
		);

		await Assert.That(exception).IsNotNull();
		await Assert.That(exception!.InnerException).IsTypeOf<ArgumentException>();
	}

	[Test]
	public async Task Deserialize_StrictScalarMode_FailureCarriesTheJsonPath()
	{
		// Arrange — the whole point of reporting JsonException is that System.Text.Json attaches the
		// position, so a caller can tell which member of the payload was rejected.
		var options = CreateOptions();

		// Act
		var exception = Assert.Throws<JsonException>(() =>
			JsonSerializer.Deserialize<StrictEmailHolder>("""{"Email":"not-an-email"}""", options)
		);

		// Assert
		await Assert.That(exception).IsNotNull();
		await Assert.That(exception!.Path).IsEqualTo("$.Email");
	}

	[Test]
	public async Task Deserialize_NonStringScalar_UsesHydrateFactory()
	{
		var options = CreateOptions();
		var id = Guid.NewGuid();
		var json = JsonSerializer.Serialize(id);
		var value = JsonSerializer.Deserialize<CustomerId>(json, options);

		await Assert.That(value.Value).IsEqualTo(id);
	}

	[Test]
	public async Task Deserialize_NullableScalarProperty_AcceptsNull()
	{
		var options = CreateOptions();
		var value = JsonSerializer.Deserialize<NullableEmailAddress>("null", options);

		await Assert.That(value.Value).IsNull();
	}

	[Test]
	public async Task Deserialize_NonNullableScalarProperty_RejectsNull()
	{
		var options = CreateOptions();

		var exception = Assert.Throws<JsonException>(() =>
			JsonSerializer.Deserialize<HydratingEmailAddress>("null", options)
		);

		await Assert.That(exception).IsNotNull();
	}

	[Test]
	public async Task Deserialize_AutomaticScalarAttribute_IsRecognizedByTheFactory()
	{
		// The automatic [Scalar<T>] form derives from the shared ScalarOptionsAttribute base, so the
		// reflection factory has to match the base rather than the non-generic ScalarAttribute type.
		var options = CreateOptions();
		var value = JsonSerializer.Deserialize<AutomaticEmailAddress>("\"TEST@Example.COM\"", options);

		await Assert.That(value.Value).IsEqualTo("TEST@Example.COM");
	}

	[Test]
	public async Task Serialize_DefaultScalarMode_WritesUnderlyingValue()
	{
		var options = CreateOptions();
		var json = JsonSerializer.Serialize(HydratingEmailAddress.Create("Test@Example.COM"), options);

		await Assert.That(json).IsEqualTo("\"test@example.com\"");
	}

	static JsonSerializerOptions CreateOptions()
	{
		JsonSerializerOptions options = new();
		options.Converters.Add(new ScalarJsonConverterFactory());
		return options;
	}
}
