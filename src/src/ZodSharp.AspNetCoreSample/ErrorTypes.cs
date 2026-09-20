using ZodSharp.AspNetCore;
using ZodSharp.Core;

namespace Purview.ValueObjects.ZodSharp.AspNetCoreSample;

/// <summary>
/// Error types registered in <see cref="ErrorTypeRegistry"/> that map validation error codes to
/// HTTP status codes and formatted messages. The bundled <c>ZODSASP001</c> analyzer verifies that
/// every <see cref="ErrorType.MessageFormat"/> placeholder is declared in <see cref="ErrorType.Parameters"/>.
/// </summary>
static partial class ErrorTypes
{
	/// <summary>
	/// Maps the <c>aggregate_save_failed</c> code to a <c>409 Conflict</c> response.
	/// </summary>
	[ErrorType]
	public static readonly ErrorType SaveFailed = new(
		Code: "aggregate_save_failed",
		Description: "The order could not be saved because it was modified concurrently.",
		HttpStatus: StatusCodes.Status409Conflict,
		MessageFormat: "Order '{OrderId}' (of type {AggregateType}) failed to save",
		Parameters: new List<ErrorTypeParameter>
		{
			ErrorType.Param<string>("OrderId"),
			ErrorType.Param<string>("AggregateType"),
		}
	);
}
