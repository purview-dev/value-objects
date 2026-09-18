using ZodSharp;
using ZodSharp.Core;

namespace Purview.ValueObjects.ZodSharpSample;

/// <summary>
/// Hand-built ZodSharp schemas that validate the underlying value of each scalar value object and
/// then construct the value object through its strict <c>Create</c> factory. This is the
/// schema-first pattern: ZodSharp owns the rule definitions, the value object owns the invariants.
/// </summary>
static class ScalarSchemas
{
	public static readonly IZodSchema<string, string> EmailSchema = Z.String().Email().Min(3).Max(254);

	public static readonly IZodSchema<string, string> CurrencySchema = Z.String().Regex("^[A-Z]{3}$");

	public static readonly IZodSchema<OrderStatusKind, OrderStatusKind> OrderStatusSchema = Z.Enum<OrderStatusKind>();

	public static readonly IZodSchema<double, double> MoneyAmountSchema = Z.Number().Positive();

	public static ValidationResult<EmailAddress> ValidateEmail(string value) =>
		Map(EmailSchema.Validate(value), EmailAddress.Create);

	public static ValidationResult<CurrencyCode> ValidateCurrency(string value) =>
		Map(CurrencySchema.Validate(value), CurrencyCode.Create);

	public static ValidationResult<OrderStatus> ValidateStatus(OrderStatusKind value) =>
		Map(OrderStatusSchema.Validate(value), OrderStatus.Create);

	/// <summary>
	/// Validates a money amount and currency together, merging the ZodSharp results before
	/// constructing the <see cref="Money"/> value object.
	/// </summary>
	public static ValidationResult<Money> ValidateMoney(decimal amount, string currency)
	{
		var amountResult = MoneyAmountSchema.Validate((double)amount);
		var currencyResult = CurrencySchema.Validate(currency);

		if (!amountResult.IsSuccess || !currencyResult.IsSuccess)
			return ValidationResult<Money>.Failure(amountResult.Errors.AddRange(currencyResult.Errors));

		// Both validations succeeded, so we can construct the Money value object.
		return ValidationResult<Money>.Success(Money.Create(amount, CurrencyCode.Create(currencyResult.Value)));
	}

	static ValidationResult<TTarget> Map<TSource, TTarget>(
		ValidationResult<TSource> result,
		Func<TSource, TTarget> construct
	) =>
		result.IsSuccess
			? ValidationResult<TTarget>.Success(construct(result.Value))
			: ValidationResult<TTarget>.Failure(result.Errors);
}
