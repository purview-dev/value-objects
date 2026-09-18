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

[Scalar(
	GenerateJsonConverter = false,
	GenerateComparable = false,
	GenerateComparisonOperators = false,
	GenerateEnumProperties = false,
	GenerateImplicitFromPrimitive = false,
	GenerateImplicitToPrimitive = false,
	GenerateEmpty = false
)]
readonly partial record struct HydratingEmailAddress
{
	public string Value { get; }

	HydratingEmailAddress(string value) => Value = value;

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase")]
	public static HydratingEmailAddress Create(string value)
	{
		return value.Contains('@', StringComparison.Ordinal)
			? new(value.Trim().ToLowerInvariant())
			: throw new ArgumentException("Invalid email address.", nameof(value));
	}

	public static HydratingEmailAddress Hydrate(string value) => new(value);
}

[Scalar(
	DeserializationMode = ValueObjectDeserializationMode.Strict,
	GenerateJsonConverter = false,
	GenerateComparable = false,
	GenerateComparisonOperators = false,
	GenerateEnumProperties = false,
	GenerateImplicitFromPrimitive = false,
	GenerateImplicitToPrimitive = false,
	GenerateEmpty = false
)]
readonly partial record struct StrictEmailAddress
{
	public string Value { get; }

	StrictEmailAddress(string value) => Value = value;

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase")]
	public static StrictEmailAddress Create(string value)
	{
		return value.Contains('@', StringComparison.Ordinal)
			? new(value.Trim().ToLowerInvariant())
			: throw new ArgumentException("Invalid email address.", nameof(value));
	}

	public static StrictEmailAddress Hydrate(string value) => new(value);
}

[Scalar(
	GenerateJsonConverter = false,
	GenerateComparable = false,
	GenerateComparisonOperators = false,
	GenerateEnumProperties = false,
	GenerateImplicitFromPrimitive = false,
	GenerateImplicitToPrimitive = false,
	GenerateEmpty = false
)]
readonly partial record struct CustomerId
{
	public Guid Value { get; }

	CustomerId(Guid value) => Value = value;

	public static CustomerId Create(Guid value)
	{
		return value == Guid.Empty ? throw new ArgumentException("Value cannot be empty.", nameof(value)) : new(value);
	}

	public static CustomerId Hydrate(Guid value) => new(value);
}
