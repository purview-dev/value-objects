using ZodSharp;
using ZodSharp.Rules;

namespace Purview.ValueObjects.Serialization;

/// <summary>
/// Dual-generator fixtures for type-level rules on scalars: the value-object generator and the
/// Purview.ZodSharp generator both run over this project. The built-in <c>[NonSentinel]</c> attribute (shipped
/// in <c>ZodSharp.Rules</c>) is written against the underlying value, so the ZodSharp generator adapts it
/// automatically through the <see cref="ScalarRuleAdapter{TSelf, TValue, TRule}"/> the
/// value-object generator emits — no hand-authored rule or attribute is required.
/// </summary>
[Scalar]
[ZodSchema]
[NonSentinel(Message = "AssetId must not be empty.")]
public readonly partial record struct AssetId
{
	public Guid Value { get; }
}

/// <summary>
/// A second Guid-backed scalar using the same built-in <see cref="NonSentinelRule{T}"/> attribute, showing one
/// attribute serving every scalar backed by the same primitive.
/// </summary>
[Scalar]
[ZodSchema]
[NonSentinel(Message = "UserId must not be empty.")]
public readonly partial record struct UserId
{
	public Guid Value { get; }
}

/// <summary>
/// A third Guid-backed scalar using the same built-in attribute.
/// </summary>
[Scalar]
[ZodSchema]
[NonSentinel(Message = "ExternalId must not be empty.")]
public readonly partial record struct ExternalId
{
	public Guid Value { get; }
}

/// <summary>
/// A Guid-backed scalar that overrides the rule's error code through the attribute, showing the built-in
/// <see cref="NonSentinelRule{T}"/> <c>code</c> parameter flowing into the reported error.
/// </summary>
[Scalar]
[ZodSchema]
[NonSentinel(Code = "invalid_correlation_id", Message = "CorrelationId must not be empty.")]
public readonly partial record struct CorrelationId
{
	public Guid Value { get; }
}

/// <summary>
/// A <c>[ZodSchema]</c> DTO whose members use the built-in validation attributes that ship in
/// <c>ZodSharp.Rules</c>: <c>[Email]</c>, <c>[E164]</c>, <c>[UUID]</c>, and <c>[MinLengthZod]</c> (the
/// DataAnnotations name-collision suffix). The generated <c>ContactDtoSchema</c> validates them.
/// </summary>
[ZodSchema]
public sealed partial class ContactDto
{
	[Email]
	public string Email { get; init; } = string.Empty;

	[E164]
	public string Phone { get; init; } = string.Empty;

	[UUID(UuidVersion.V4)]
	public string Id { get; init; } = string.Empty;

	[MinLengthZod(3)]
	public string Code { get; init; } = string.Empty;
}
