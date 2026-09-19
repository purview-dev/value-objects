using System.ComponentModel.DataAnnotations;
using ZodSharp;
using ZodSharp.Core;

namespace Purview.ValueObjects.ZodSharpSample;

/// <summary>
/// A DTO validated by the source-generated <c>PromoCodeSchema</c>. The
/// <c>CustomValidationMethodName</c> option names a static async validation method that the generator
/// wires into the generated <c>ValidateAsync</c>, which awaits it after the synchronous DataAnnotations
/// rules pass.
/// </summary>
[ZodSchema(CustomValidationMethodName = nameof(ValidatePromoCodeAsync))]
sealed class PromoCode
{
	[Required]
	[RegularExpression(@"^[A-Z0-9]{4,10}$")]
	public string Code { get; init; } = string.Empty;

	internal static ValueTask<ValidationResult<PromoCode>> ValidatePromoCodeAsync(
		PromoCode value,
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var isKnownPromo = value.Code is "SAVE10" or "WELCOME20";
		return ValueTask.FromResult(
			isKnownPromo
				? ValidationResult<PromoCode>.Success(value)
				: ValidationResult<PromoCode>.Failure(
					new ValidationError("code", "Unknown promotional code.", [nameof(Code)])
				)
		);
	}
}
