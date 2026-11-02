using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ZodSharp;
using ZodSharp.Core;

namespace Purview.ValueObjects.Serialization;

/// <summary>
/// A strict value object's generated converter reports a validation failure as a <see cref="JsonException"/> whose
/// inner exception is the validator's, so System.Text.Json records where the value sat. Purview.ZodSharp joins the
/// two (<see cref="ZodJsonExceptions"/>), so a <see cref="ZodException"/>'s errors are located under the member that
/// held the value - exactly as validating the deserialized model would report them.
/// </summary>
public sealed class StrictDeserializationTests
{
	[Test]
	public async Task Deserialize_GivenInvalidStrictZodMember_ReportsErrorsUnderTheMember()
	{
		// Arrange
		const string json = """{"CustomerEmail":"not-an-email","Copies":[]}""";

		// Act
		var exception = CaptureJsonException<StrictOrderRequest>(json);

		// Assert
		await Assert.That(exception.Path).IsEqualTo("$.CustomerEmail");
		await Assert.That(exception.InnerException).IsTypeOf<ZodException>();

		var found = ZodJsonExceptions.TryGetErrors(exception, RequestTypeInfo(), out var errors);
		await Assert.That(found).IsTrue();
		await Assert
			.That(errors.Select(static e => e.FullPath.ToDottedString()))
			.IsEquivalentTo(["CustomerEmail.Value"]);
	}

	[Test]
	public async Task Deserialize_GivenInvalidStrictZodElement_ReportsErrorsUnderTheElement()
	{
		// Arrange
		const string json = """{"CustomerEmail":"a@example.com","Copies":["b@example.com","not-an-email"]}""";

		// Act
		var exception = CaptureJsonException<StrictOrderRequest>(json);

		// Assert
		ZodJsonExceptions.TryGetErrors(exception, RequestTypeInfo(), out var errors);
		await Assert.That(errors.Select(static e => e.FullPath.ToDottedString())).IsEquivalentTo(["Copies[1].Value"]);
	}

	[Test]
	public async Task Deserialize_GivenInvalidStrictComplexValueObject_WrapsTheValidatorException()
	{
		// Arrange
		const string json = """{"Price":{"Amount":-1,"Currency":"USD"}}""";

		// Act
		var exception = CaptureJsonException<StrictPriceHolder>(json);

		// Assert
		await Assert.That(exception.Path).IsEqualTo("$.Price");
		await Assert.That(exception.InnerException).IsAssignableTo<ArgumentException>();
		await Assert.That(exception.Message).Contains(nameof(StrictPrice));
	}

	[Test]
	public async Task Deserialize_GivenValidStrictValues_RoundTrips()
	{
		// Arrange
		const string json = """{"CustomerEmail":"a@example.com","Copies":["b@example.com"]}""";

		// Act
		var request = JsonSerializer.Deserialize<StrictOrderRequest>(json);

		// Assert
		await Assert.That(request!.CustomerEmail.Value).IsEqualTo("a@example.com");
		await Assert.That(request.Copies.Single().Value).IsEqualTo("b@example.com");
	}

	[Test]
	public async Task Deserialize_GivenHydrateMode_DoesNotValidate()
	{
		// Act - the default mode is the replay path: an invalid value is hydrated, never rejected.
		var email = JsonSerializer.Deserialize<ZodInsteadOfHooksEmail>("\"not-an-email\"");

		// Assert
		await Assert.That(email.Value).IsEqualTo("not-an-email");
	}

	static JsonException CaptureJsonException<T>(string json)
	{
		try
		{
			JsonSerializer.Deserialize<T>(json);
		}
		catch (JsonException ex)
		{
			return ex;
		}

		throw new InvalidOperationException("Expected a JsonException.");
	}

	// The root contract maps the JSON names in JsonException.Path back to member names - from a source-generated
	// context, as a trimmed or Native AOT application would supply it.
	static System.Text.Json.Serialization.Metadata.JsonTypeInfo<StrictOrderRequest> RequestTypeInfo() =>
		StrictDeserializationJsonContext.Default.StrictOrderRequest;
}

/// <summary>A strict scalar validated by its ZodSharp schema.</summary>
[Scalar(ZodSchemaMode = ZodSchemaMode.InsteadOfHooks, DeserializationMode = ValueObjectDeserializationMode.Strict)]
[ZodSchema]
public readonly partial record struct StrictZodEmail
{
	[Required, EmailAddress]
	public string Value { get; }
}

public sealed record StrictOrderRequest(StrictZodEmail CustomerEmail, IReadOnlyList<StrictZodEmail> Copies);

/// <summary>A strict complex value object validated by its <c>OnValidate</c> hook.</summary>
[ValueObject(DeserializationMode = ValueObjectDeserializationMode.Strict)]
public readonly partial record struct StrictPrice(decimal Amount, string Currency)
{
	partial void OnValidate(decimal amount, string currency)
	{
		if (amount < 0)
			throw new ArgumentOutOfRangeException(nameof(amount), "The amount must not be negative.");
	}
}

public sealed record StrictPriceHolder(StrictPrice Price);

[System.Text.Json.Serialization.JsonSerializable(typeof(StrictOrderRequest))]
sealed partial class StrictDeserializationJsonContext : System.Text.Json.Serialization.JsonSerializerContext;
