using System.ComponentModel.DataAnnotations;
using Purview.ValueObjects.Serialization;
using ZodSharp;
using ZodSharp.Core;

namespace Purview.ValueObjects;

/// <summary>
/// Verifies the generator-integrated ZodSharp validation: a value object annotated with both
/// <c>[Scalar]</c> and <c>[ZodSchema]</c> validates the constructed instance through the ZodSharp
/// source-generated <c>ValidatedEmailAddressSchema</c> in its <c>Create</c> path.
/// </summary>
public sealed class ZodSchemaValidationTests
{
	[Test]
	public async Task Create_GivenInvalidEmail_ThrowsZodExceptionFromGeneratedSchema()
	{
		await Assert.That(() => ValidatedEmailAddress.Create("not-an-email")).Throws<ZodException>();
	}

	[Test]
	public async Task Create_GivenValidEmail_Succeeds()
	{
		var email = ValidatedEmailAddress.Create("demo@example.com");

		await Assert.That(email.Value).IsEqualTo("demo@example.com");
	}

	[Test]
	public async Task Hydrate_GivenInvalidEmail_DoesNotRevalidate()
	{
		// Hydrate is replay-safe: the generated schema is only invoked by Create.
		var email = ValidatedEmailAddress.Hydrate("not-an-email");

		await Assert.That(email.Value).IsEqualTo("not-an-email");
	}
}

[Scalar]
[ZodSchema]
public readonly partial record struct ValidatedEmailAddress
{
	[EmailAddress]
	[StringLength(254, MinimumLength = 3)]
	public string Value { get; }
}
