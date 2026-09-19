using System.ComponentModel.DataAnnotations;
using Purview.ValueObjects.Serialization;
using ZodSharp;

namespace Purview.ValueObjects.ZodSharp.AspNetCoreSample;

/// <summary>
/// A scalar value object validated by the ZodSharp-generated schema and deserialized in
/// <see cref="ValueObjectDeserializationMode.Strict"/> mode, so invalid JSON input throws a
/// <c>ZodException</c> that the registered exception handler converts to Problem Details.
/// </summary>
[Scalar(ZodSchemaMode = ZodSchemaMode.InsteadOfHooks, DeserializationMode = ValueObjectDeserializationMode.Strict)]
[ZodSchema]
readonly partial record struct EmailAddress
{
	[Required, EmailAddress, StringLength(254)]
	public string Value { get; }
}

/// <summary>
/// A scalar value object validated by the ZodSharp-generated schema and deserialized strictly.
/// </summary>
[Scalar(ZodSchemaMode = ZodSchemaMode.InsteadOfHooks, DeserializationMode = ValueObjectDeserializationMode.Strict)]
[ZodSchema]
readonly partial record struct OrderQuantity
{
	[Range(1, 100)]
	public int Value { get; }
}

/// <summary>
/// The request DTO bound by <c>POST /orders</c>. Its value object members are deserialized strictly.
/// </summary>
sealed record PlaceOrderRequest(EmailAddress CustomerEmail, OrderQuantity Quantity);
