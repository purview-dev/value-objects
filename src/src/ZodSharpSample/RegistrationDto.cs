using System.ComponentModel.DataAnnotations;
using ZodSharp;
using ZodSharp.Core;

namespace Purview.ValueObjects.ZodSharpSample;

/// <summary>
/// A plain DTO validated by the source-generated <c>RegistrationDtoSchema</c> /
/// <c>RegistrationDtoSchemaValidator</c>. Values are mapped to value objects after validation.
/// </summary>
[ZodSchema(RefinementMethodName = nameof(ValidateRegistration))]
sealed class RegistrationDto
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
	/// A custom sync refinement method, wired up via <c>RefinementMethodName</c>. The generator
	/// discovers this instance method and runs the returned errors after the DataAnnotations rules.
	/// </summary>
	public IEnumerable<ValidationError> ValidateRegistration()
	{
		if (Name.StartsWith('x'))
			yield return new ValidationError("name", "Name cannot start with 'x'.", [nameof(Name)]);
	}
}
