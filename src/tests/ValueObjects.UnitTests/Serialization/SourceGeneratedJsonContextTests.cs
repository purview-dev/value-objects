using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Purview.ValueObjects.Serialization;

/// <summary>
/// The generated JSON converters must not depend on reflection, so they keep working with the source-generated
/// <see cref="JsonSerializerContext"/> a trimmed or Native AOT application serializes through.
/// </summary>
/// <remarks>
/// A context declared in the project that declares the value objects cannot see the <c>[JsonConverter]</c>
/// attribute the value object generator adds, so it describes <see cref="ContextCurrencyCode"/> as an object with a
/// <c>Value</c> property. The application registers the root value objects' generated converters on the options
/// (<see cref="ContextOptions"/>); <see cref="ContextCurrencyCode"/>, a member of <see cref="ContextMoney"/>, is
/// deliberately not registered, so these tests prove the converter <see cref="ValueObjectJson"/> is handed for a
/// value-object-typed member wins over the context's object metadata.
/// </remarks>
public sealed class SourceGeneratedJsonContextTests
{
	static readonly JsonSerializerOptions ContextOptions = new(ContextOnlyJsonContext.Default.Options)
	{
		Converters =
		{
			new ContextMoney.ContextMoneyJsonConverter(),
			new ContextReference.ContextReferenceJsonConverter(),
		},
	};

	static readonly JsonSerializerOptions IgnoreNullOptions = new(ContextOptions)
	{
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	};

	// A resolver that describes nothing, as a trimmed application's would for a type it never listed.
	static readonly JsonSerializerOptions EmptyResolverOptions = new()
	{
		TypeInfoResolver = JsonTypeInfoResolver.Combine(),
	};

	[Test]
	public async Task ComplexValueObject_GivenContextWithoutMemberConverters_RoundTrips()
	{
		// Arrange
		ContextOrder order = new()
		{
			Total = ContextMoney.Create(19.99m, ContextCurrencyCode.Create("usd")),
			Reference = ContextReference.Create("REF-1"),
		};

		// Act
		var json = JsonSerializer.Serialize(order, ContextOptions);
		var roundTripped = JsonSerializer.Deserialize<ContextOrder>(json, ContextOptions);

		// Assert
		await Assert
			.That(json)
			.IsEqualTo("""{"total":{"amount":19.99,"currency":"USD","fallback":null},"reference":"REF-1"}""");
		await Assert.That(roundTripped!.Total).IsEqualTo(order.Total);
		await Assert.That(roundTripped.Reference).IsEqualTo(order.Reference);
	}

	[Test]
	public async Task ComplexValueObject_GivenNullableValueObjectMember_RoundTripsValueAndNull()
	{
		// Arrange
		var withFallback = ContextMoney.Create(
			5m,
			ContextCurrencyCode.Create("GBP"),
			ContextCurrencyCode.Create("EUR")
		);

		// Act
		var json = JsonSerializer.Serialize(withFallback, ContextOptions);
		var roundTripped = JsonSerializer.Deserialize<ContextMoney>(json, ContextOptions);

		// Assert
		await Assert.That(json).IsEqualTo("""{"amount":5,"currency":"GBP","fallback":"EUR"}""");
		await Assert.That(roundTripped).IsEqualTo(withFallback);
	}

	[Test]
	public async Task ComplexValueObject_GivenUnknownAndMissingMembers_SkipsAndDefaults()
	{
		// Act - "extra" is skipped, including its nested object, and the missing "fallback" stays null.
		var money = JsonSerializer.Deserialize<ContextMoney>(
			"""{"extra":{"nested":[1,2]},"amount":1.5,"currency":"CHF"}""",
			ContextOptions
		);

		// Assert
		await Assert.That(money.Amount).IsEqualTo(1.5m);
		await Assert.That(money.Currency).IsEqualTo(ContextCurrencyCode.Create("CHF"));
		await Assert.That(money.Fallback).IsNull();
	}

	[Test]
	public async Task ComplexValueObject_GivenWhenWritingNull_OmitsNullMember()
	{
		// Act
		var json = JsonSerializer.Serialize(
			ContextMoney.Create(2m, ContextCurrencyCode.Create("USD")),
			IgnoreNullOptions
		);

		// Assert
		await Assert.That(json).IsEqualTo("""{"amount":2,"currency":"USD"}""");
	}

	[Test]
	public async Task ComplexValueObject_GivenNonObjectToken_ThrowsJsonException()
	{
		// Act & Assert
		await Assert.That(() => JsonSerializer.Deserialize<ContextMoney>("42", ContextOptions)).Throws<JsonException>();
	}

	[Test]
	public async Task Read_GivenResolverWithoutMetadataForPrimitive_UsesBuiltInConverter()
	{
		// Arrange
		var id = Guid.NewGuid();
		Utf8JsonReader reader = new(JsonSerializer.SerializeToUtf8Bytes(id.ToString()));
		reader.Read();

		// Act
		var read = ValueObjectJson.Read<Guid>(ref reader, EmptyResolverOptions);

		// Assert
		await Assert.That(read).IsEqualTo(id);
	}

	[Test]
	public async Task Read_GivenResolverWithoutMetadataForNullablePrimitive_ReadsNull()
	{
		// Arrange
		Utf8JsonReader reader = new("null"u8);
		reader.Read();

		// Act
		var read = ValueObjectJson.Read<int?>(ref reader, EmptyResolverOptions);

		// Assert
		await Assert.That(read).IsNull();
	}

	[Test]
	public async Task Read_GivenResolverWithoutMetadataForCustomType_ThrowsNotSupportedNamingTheType()
	{
		// Arrange
		Utf8JsonReader reader = new("""{"x":1}"""u8);
		reader.Read();

		// Act
		NotSupportedException? exception = null;
		try
		{
			ValueObjectJson.Read<ContextOrder>(ref reader, EmptyResolverOptions);
		}
		catch (NotSupportedException ex)
		{
			exception = ex;
		}

		// Assert
		await Assert.That(exception).IsNotNull();
		await Assert.That(exception!.Message).Contains(typeof(ContextOrder).FullName!);
	}
}

[Scalar]
public readonly partial record struct ContextCurrencyCode
{
	public string Value { get; }

	static partial void OnNormalize(ref string value) => value = value?.Trim().ToUpperInvariant()!;
}

[Scalar]
public readonly partial record struct ContextReference
{
	public string Value { get; }
}

[ValueObject]
public readonly partial record struct ContextMoney
{
	public decimal Amount { get; }

	public ContextCurrencyCode Currency { get; }

	public ContextCurrencyCode? Fallback { get; }

	public static ContextMoney Create(decimal amount, ContextCurrencyCode currency) => Create(amount, currency, null);
}

public sealed class ContextOrder
{
	public ContextMoney Total { get; set; }

	public ContextReference Reference { get; set; }
}

[JsonSerializable(typeof(ContextOrder))]
[JsonSerializable(typeof(ContextMoney))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
sealed partial class ContextOnlyJsonContext : JsonSerializerContext;
