using ZodSharp;
using ZodSharp.Rules;

namespace Purview.ValueObjects.Serialization;

/// <summary>
/// Dual-generator fixtures for the automatic scalar forms. The value-object generator declares the
/// underlying property, so the ZodSharp generator cannot see the member and resolves the underlying type
/// and property name from the attribute. Type-level rules written against the underlying value are adapted
/// through the <see cref="ScalarRuleAdapter{TSelf, TValue, TRule}"/> the value-object generator emits.
/// </summary>
/// <remarks>
/// Both automatic spellings are covered: the generic <c>[Scalar&lt;TValue&gt;]</c> and the non-generic
/// <c>[Scalar(typeof(TValue))]</c>, including their nullable variants.
/// </remarks>
[Scalar<Guid>]
[ZodSchema]
[NonSentinel(Message = "InstallationId must not be empty.")]
public readonly partial record struct InstallationId { }

/// <summary>The non-generic automatic form of the same shape.</summary>
[Scalar(typeof(Guid))]
[ZodSchema]
[NonSentinel(Message = "TypeofInstallationId must not be empty.")]
public readonly partial record struct TypeofInstallationId { }

/// <summary>The automatic form with a custom property name (<c>Id</c>).</summary>
[Scalar<Guid>("Id")]
[ZodSchema]
[NonSentinel(Message = "TenantId must not be empty.")]
public readonly partial record struct AutoTenantId { }

/// <summary>The non-generic automatic form with a custom property name (<c>Id</c>).</summary>
[Scalar(typeof(Guid), "Id")]
[ZodSchema]
[NonSentinel(Message = "TypeofTenantId must not be empty.")]
public readonly partial record struct TypeofAutoTenantId { }

/// <summary>A nullable reference scalar; it round-trips JSON <c>null</c> and rejects whitespace.</summary>
[Scalar<string>(Nullable = true)]
[ZodSchema]
[NullOrNonWhiteSpace(Message = "Nickname must be null or non-whitespace.")]
public readonly partial record struct Nickname { }

/// <summary>The non-generic automatic form of a nullable reference scalar.</summary>
[Scalar(typeof(string), Nullable = true)]
[ZodSchema]
[NullOrNonWhiteSpace(Message = "TypeofNickname must be null or non-whitespace.")]
public readonly partial record struct TypeofNickname { }

/// <summary>A nullable value type scalar.</summary>
[Scalar<int?>]
[ZodSchema]
public readonly partial record struct Score { }

/// <summary>The non-generic automatic form of a nullable value type scalar.</summary>
[Scalar(typeof(int?))]
[ZodSchema]
public readonly partial record struct TypeofScore { }

/// <summary>A nullable scalar that requires a value (<c>[RequiredZod]</c> rejects null).</summary>
[Scalar<string>(Nullable = true)]
[ZodSchema]
[RequiredZod]
public readonly partial record struct RequiredName { }

/// <summary>
/// The automatic form with the default <see cref="ZodSchemaMode.InAdditionToHooks"/>: the schema runs and
/// the <c>OnValidate</c> hook still runs.
/// </summary>
[Scalar<string>]
[ZodSchema]
public readonly partial record struct AutoHookEmail
{
	static partial void OnValidate(string value)
	{
		if (value != "allowed")
			throw new ArgumentException("Only 'allowed' is accepted.", nameof(value));
	}
}

/// <summary>
/// The automatic form with <see cref="ZodSchemaMode.InsteadOfHooks"/>: the hook throws, so a successful
/// <c>Create</c> proves it was skipped.
/// </summary>
/// <remarks>
/// The <c>OnValidate</c> body is deliberately unreachable, which is what <c>VO1013</c> reports; the test
/// project opts out of that rule.
/// </remarks>
[Scalar<string>(ZodSchemaMode = ZodSchemaMode.InsteadOfHooks)]
[ZodSchema]
public readonly partial record struct AutoInsteadOfHooksEmail
{
	static partial void OnValidate(string value) =>
		throw new ArgumentException("OnValidate must not run.", nameof(value));
}

/// <summary>
/// The automatic form with a custom generated schema class name; the value-object generator's
/// <c>Create</c> must resolve the same name.
/// </summary>
[Scalar<Guid>]
[ZodSchema(SchemaName = "AutoNamedSchemaIdSchema")]
[NonSentinel(Message = "AutoNamedSchemaId must not be empty.")]
public readonly partial record struct AutoNamedSchemaId { }
