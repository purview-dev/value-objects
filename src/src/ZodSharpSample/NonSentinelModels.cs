using Purview.ValueObjects.Serialization;
using ZodSharp;
using ZodSharp.Rules;

namespace Purview.ValueObjects.ZodSharpSample;

/// <summary>
/// A Guid-backed scalar whose non-default check is driven entirely by the built-in
/// <see cref="NonSentinelRule{T}"/> attribute that ships with <c>Purview.ZodSharp</c>. The rule is written
/// against the underlying value, so the ZodSharp generator adapts it automatically through the value-object
/// generator's <see cref="ScalarRuleAdapter{TSelf, TValue, TRule}"/> — no hand-authored rule or attribute is
/// required.
/// </summary>
[Scalar]
[ZodSchema]
[NonSentinel(Message = "AssetId must not be empty.")]
readonly partial record struct AssetId
{
	public Guid Value { get; }
}

/// <summary>
/// A second Guid-backed scalar using the same built-in attribute, showing one attribute serving every scalar
/// backed by the same primitive.
/// </summary>
[Scalar]
[ZodSchema]
[NonSentinel(Message = "UserId must not be empty.")]
readonly partial record struct UserId
{
	public Guid Value { get; }
}
