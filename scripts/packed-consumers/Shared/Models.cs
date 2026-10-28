// Shared by the Blazor consumers: scripts/test-packed-consumers.ps1 copies this folder into BlazorWasm and
// BlazorServer before building them, so both validate exactly the same value objects.
using System.ComponentModel.DataAnnotations;
using Purview.ValueObjects.Serialization;
using ZodSharp;
#if !EXCLUDE_TYPE_LEVEL_RULES
using ZodSharp.Rules;
#endif

namespace PackedValueObjectsBlazor;

/// <summary>A scalar whose generated <c>Create</c> normalizes, then validates through <c>EmailAddressSchema</c>.</summary>
[Scalar]
[ZodSchema]
public readonly partial record struct EmailAddress
{
	[Required]
	[EmailAddress]
	[StringLength(254, MinimumLength = 3)]
	public string Value { get; }

	static partial void OnNormalize(ref string value) => value = value?.Trim().ToLowerInvariant()!;
}

/// <summary>A scalar validated by a pattern on its underlying value.</summary>
[Scalar]
[ZodSchema]
public readonly partial record struct PostalCode
{
	[Required]
	[RegularExpression("^[0-9]{5}$")]
	public string Value { get; }
}

/// <summary>
/// A Guid scalar rejected by the built-in, type-level <c>[NonSentinel]</c> rule. The WebAssembly AOT publish
/// builds without the rule (EXCLUDE_TYPE_LEVEL_RULES): Purview.ZodSharp 3.0.0-prerelease.1 validates a type-level
/// rule through <c>ZodRuleErrors.Build(..., (object)rule as IZodRule)</c>, which crashes the Mono AOT compiler
/// (0xC0000005). The gate probes that publish separately and reports it as a known issue.
/// </summary>
[Scalar]
[ZodSchema]
#if !EXCLUDE_TYPE_LEVEL_RULES
[NonSentinel(Message = "Choose a plan.")]
#endif
public readonly partial record struct PlanId
{
	public Guid Value { get; }
}

/// <summary>
/// The form model. Its value object members are validated by their own generated schemas, so the rules live with
/// the value objects and the form only declares what is specific to it.
/// </summary>
[ZodSchema]
public sealed class Registration
{
	[Required]
	[StringLength(50, MinimumLength = 2)]
	public string? Name { get; set; }

	public EmailAddress Email { get; set; }

	public PostalCode Postcode { get; set; }

	public PlanId Plan { get; set; }
}

/// <summary>Whether this build carries the type-level rule (see <see cref="PlanId"/>).</summary>
public static class TypeLevelRules
{
#if EXCLUDE_TYPE_LEVEL_RULES
	public static bool Enabled => false;
#else
	public static bool Enabled => true;
#endif
}
