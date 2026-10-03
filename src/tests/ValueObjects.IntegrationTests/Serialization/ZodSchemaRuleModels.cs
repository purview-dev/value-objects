using System.ComponentModel.DataAnnotations;
using ZodSharp;
using ZodSharp.Core;
using ZodSharp.Rules;

namespace Purview.ValueObjects.Serialization;

/// <summary>
/// A custom rule that validates a scalar value object <em>as a unit</em>. It reads the value object through
/// its <see cref="IScalarValueObject{TSelf, TValue}"/> contract, so the same rule serves every Guid-backed
/// scalar. Implementing <see cref="IZodRule"/> lets the rule own the reported error code and origin, which is
/// how one attribute can produce a different code per annotated scalar.
/// </summary>
/// <remarks>
/// This is the supported shape for a rule that must observe the value object rather than its
/// <c>Value</c>. A generic rule must have exactly one type parameter, so the underlying value type is pinned
/// by the constraint: define one rule per primitive
/// (<c>NonEmptyRule&lt;TSelf&gt; where TSelf : IScalarValueObject&lt;TSelf, Guid&gt;</c>, a <c>long</c>
/// variant, and so on).
/// </remarks>
public readonly record struct NonEmptyRule<TSelf>(string? Code = null, string? Message = null)
	: IValidationRule<TSelf>,
		IZodRule
	where TSelf : IScalarValueObject<TSelf, Guid>
{
	public const string ErrorCode = "invalid_value";
	public const string MessageFormat = "Value must not be empty.";

	public bool IsValid(in TSelf value) => value.Value != Guid.Empty;

	public string GetErrorMessage(in TSelf value) => Message ?? MessageFormat;

	// IValidationRule<T>.Code is the rule's own default; IZodRule.Code is the optional per-use override, so
	// the generator prefers a code supplied where the attribute is applied and falls back to ErrorCode.
	string IValidationRule<TSelf>.Code => Code ?? ErrorCode;

	string? IZodRule.Code => Code;

	string? IZodRule.Origin => "value_object";
}

/// <summary>
/// Maps <see cref="NonEmptyRule{TSelf}"/> to a type-level DataAnnotations attribute. The ZodSharp generator
/// closes the open generic with the annotated scalar type, so the rule validates the value object as a unit
/// and reports an empty path.
/// </summary>
[ZodRule(typeof(NonEmptyRule<>))]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class NonEmptyAttribute : ValidationAttribute
{
	public string? Code { get; set; }

	public string? Message { get; set; }
}

/// <summary>
/// The scalar-aware member of the non-sentinel rule family: it adapts <see cref="NonSentinelRule{T}"/> for a
/// Guid-backed scalar value object and owns the reported error identity.
/// </summary>
/// <remarks>
/// The <see cref="Purview.ValueObjects.ScalarRuleAdapter{TSelf, TValue, TRule}"/> it composes is emitted into
/// this project by the value-object generator, which runs because the project references both
/// <c>Purview.ValueObjects</c> and <c>Purview.ZodSharp</c>.
/// </remarks>
public readonly record struct NonSentinelScalarRule<TSelf>(string? Message = null) : IValidationRule<TSelf>, IZodRule
	where TSelf : IScalarValueObject<TSelf, Guid>
{
	public const string ErrorCode = NonSentinelRule<Guid>.ErrorCode;
	public const string MessageFormat = NonSentinelRule<Guid>.MessageFormat;

	public bool IsValid(in TSelf value) =>
		new ScalarRuleAdapter<TSelf, Guid, NonSentinelRule<Guid>>(new NonSentinelRule<Guid>(Message)).IsValid(value);

	public string GetErrorMessage(in TSelf value) =>
		new ScalarRuleAdapter<TSelf, Guid, NonSentinelRule<Guid>>(new NonSentinelRule<Guid>(Message)).GetErrorMessage(
			value
		);

	string? IZodRule.Code => NonSentinelRule<Guid>.ErrorCode;

	string? IZodRule.Origin => "value_object";
}

/// <summary>
/// Maps <see cref="NonSentinelScalarRule{TSelf}"/> to a type-level attribute. The attribute name encodes the
/// rule name (<c>NonSentinelScalarAttribute</c> → <c>NonSentinelScalarRule</c>) so the mapping addresses the
/// whole family.
/// </summary>
[ZodRule(typeof(NonSentinelScalarRule<>))]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class NonSentinelScalarAttribute : ValidationAttribute
{
	public string? Message { get; set; }
}

/// <summary>
/// A Guid-backed scalar whose validation is driven entirely by a type-level custom rule
/// (<see cref="NonEmptyAttribute"/>). The generated <c>Create</c> validates the value object through the
/// ZodSharp-generated schema, which reports the rule's own <c>Code</c> and <c>Origin</c>.
/// </summary>
[Scalar]
[ZodSchema]
[NonEmpty(Code = "invalid_asset_id", Message = "AssetId must not be empty.")]
public readonly partial record struct AssetId
{
	public Guid Value { get; }
}

/// <summary>
/// A Guid-backed scalar whose validation reuses the normal <see cref="NonSentinelRule{T}"/> through
/// <see cref="NonSentinelScalarRule{TSelf}"/>, showing a rule written for an underlying value applied to a
/// scalar value object as a unit.
/// </summary>
[Scalar]
[ZodSchema]
[NonSentinelScalar]
public readonly partial record struct TenantId
{
	public Guid Value { get; }
}
