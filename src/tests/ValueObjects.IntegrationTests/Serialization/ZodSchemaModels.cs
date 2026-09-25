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
/// <see cref="ZodSchemaMode.InsteadOfHooks"/> skips the hook entirely; the hook throws so a successful
/// <c>Create</c> proves it was not invoked.
/// </summary>
[Scalar(ZodSchemaMode = ZodSchemaMode.InsteadOfHooks)]
[ZodSchema]
public readonly partial record struct ZodInsteadOfHooksEmail
{
	public string Value { get; }

	static partial void OnValidate(string value) =>
		throw new ArgumentException("OnValidate must not run.", nameof(value));
}
