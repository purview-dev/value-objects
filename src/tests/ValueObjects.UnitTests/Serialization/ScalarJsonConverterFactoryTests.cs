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
		var options = CreateOptions();
		var threw = false;
		try
		{
			_ = JsonSerializer.Deserialize<StrictEmailAddress>("\"not-an-email\"", options);
		}
		catch (ArgumentException)
		{
			threw = true;
		}

		await Assert.That(threw).IsTrue();
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
