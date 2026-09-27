using System.ComponentModel.DataAnnotations;
using ZodSharp;

namespace Purview.ValueObjects.Serialization;

/// <summary>
/// Dual-generator fixtures: the value-object generator and the Purview.ZodSharp generator both run
/// over this project, so <c>[Scalar]</c> + <c>[ZodSchema]</c> types exercise the real end-to-end
/// integration (the generated <c>Create</c> validates the constructed instance through the
/// ZodSharp-generated schema).
/// </summary>
[Scalar]
[ZodSchema]
public readonly partial record struct ZodValidatedEmail
{
	[EmailAddress]
	[StringLength(254, MinimumLength = 3)]
	public string Value { get; }
}

/// <summary>
/// ZodSharp validation combined with a hook: the hook still runs because the default mode is
/// <see cref="ZodSchemaMode.InAdditionToHooks"/>.
/// </summary>
[Scalar]
[ZodSchema]
public readonly partial record struct ZodHookEmail
{
	public string Value { get; }

	static partial void OnValidate(string value)
	{
		if (value != "allowed")
			throw new ArgumentException("Only 'allowed' is accepted.", nameof(value));
	}
}

/// <summary>
/// ZodSharp refinement hook: the value object owns rules ZodSharp's DataAnnotations cannot express. The
/// generated Zod-compatible refinement surfaces the issues the hook adds through the ZodSharp-generated
/// schema, and therefore through the strict <c>Create</c> path.
/// </summary>
[Scalar]
[ZodSchema]
public readonly partial record struct ZodRefinementEmail
{
	public string Value { get; }

	partial void OnZodValidate(ZodSharp.Schemas.RefineCtx<ZodRefinementEmail> context)
	{
		if (context.Value.Value.EndsWith(".invalid", StringComparison.Ordinal))
			context.AddIssue("invalid_domain", "Domain is not allowed.", [nameof(Value)]);
	}
}

/// <summary>
/// A complex value object whose own Zod refinement supplies a cross-member rule.
/// </summary>
[ValueObject]
[ZodSchema]
public readonly partial record struct ZodRefinementMoney(decimal Amount, string Currency)
{
	partial void OnZodValidate(ZodSharp.Schemas.RefineCtx<ZodRefinementMoney> context)
	{
		if (context.Value.Amount <= 0)
			context.AddIssue("invalid_amount", "Amount must be positive.", [nameof(Amount)]);
	}
}

/// <summary>
/// <see cref="ZodSchemaMode.InsteadOfHooks"/> skips the hook entirely; the hook throws so a successful
/// <c>Create</c> proves it was not invoked.
/// </summary>
/// <remarks>
/// The <c>OnValidate</c> body is deliberately unreachable, which is what <c>VO1013</c> reports; the test
/// project opts out of that rule (see <c>ValueObjects.IntegrationTests.csproj</c>) because every fixture
/// here exists to exercise a mode the analyzer warns about.
/// </remarks>
[Scalar(ZodSchemaMode = ZodSchemaMode.InsteadOfHooks)]
[ZodSchema]
public readonly partial record struct ZodInsteadOfHooksEmail
{
	public string Value { get; }

	static partial void OnValidate(string value) =>
		throw new ArgumentException("OnValidate must not run.", nameof(value));
}
