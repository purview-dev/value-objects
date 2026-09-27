using System.ComponentModel.DataAnnotations;
using ZodSharp;
using ZodSharp.Schemas;

namespace Purview.ValueObjects.ZodSharpSample;

/// <summary>
/// A plain DTO validated by the source-generated <c>RegistrationDtoSchema</c> /
/// <c>RegistrationDtoSchemaValidator</c>. Values are mapped to value objects after validation.
/// </summary>
/// <remarks>
/// <c>partial</c> is required so the ZodSharp generator can declare the <c>OnZodValidate</c> refinement hook
/// on this type.
/// </remarks>
[ZodSchema]
sealed partial class RegistrationDto
{
	[Required]
	[StringLength(100, MinimumLength = 2)]
	public string Name { get; init; } = string.Empty;

	[Range(13, 120)]
	public int Age { get; init; }

	[Required]
	[EmailAddress]
	public string Email { get; init; } = string.Empty;

	/// <summary>
	/// A custom refinement, declared by the ZodSharp generator and implemented here. The issues it adds are
	/// merged with the DataAnnotations issues by every schema entry point.
	/// </summary>
	partial void OnZodValidate(RefineCtx<RegistrationDto> context)
	{
		if (context.Value.Name.StartsWith('x'))
			context.AddIssue("name", "Name cannot start with 'x'.", [nameof(Name)]);
	}
}
