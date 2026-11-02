using System.ComponentModel.DataAnnotations;
using Purview.ValueObjects.Serialization;
using ZodSharp;

namespace Purview.ValueObjects.ZodSharp.AspNetCoreSample;

/// <summary>
/// A scalar value object validated by the ZodSharp-generated schema. It deserializes with the default
/// <see cref="ValueObjectDeserializationMode.Hydrate"/>, so binding never throws; the endpoint filter validates the
/// bound request, and an invalid email is reported at <c>CustomerEmail.Value</c>.
/// </summary>
[Scalar(ZodSchemaMode = ZodSchemaMode.InsteadOfHooks)]
[ZodSchema]
readonly partial record struct EmailAddress
{
	[Required, EmailAddress, StringLength(254)]
	public string Value { get; }
}

/// <summary>
/// A scalar value object validated by the ZodSharp-generated schema.
/// </summary>
[Scalar(ZodSchemaMode = ZodSchemaMode.InsteadOfHooks)]
[ZodSchema]
readonly partial record struct OrderQuantity
{
	[Range(1, 100)]
	public int Value { get; }
}

/// <summary>
/// The request DTO bound by <c>POST /orders</c>. Its generated schema validates each value object member through
/// that value object's own schema, so every invalid member is reported, under its own path.
/// </summary>
[ZodSchema]
sealed partial record PlaceOrderRequest(EmailAddress CustomerEmail, OrderQuantity Quantity);
