using ZodSharp.Core;

namespace Purview.ValueObjects.Serialization;

/// <summary>
/// End-to-end ZodSharp integration: both generators run in the real compiler for this project, so
/// these tests exercise the shipped behaviour (no in-memory harness, no reflected generator types).
/// </summary>
public sealed class ZodSchemaIntegrationTests
{
	[Test]
	public async Task ZodValidatedEmail_GivenValidValue_CreatesAndGivenInvalidValue_ThrowsZodException()
	{
		// Act
		var created = ZodValidatedEmail.Create("demo@example.com");

		// Assert
		await Assert.That(created.Value).IsEqualTo("demo@example.com");
		await Assert.That(() => ZodValidatedEmail.Create("not-an-email")).Throws<ZodException>();
	}

	[Test]
	public async Task ZodHookEmail_GivenZodSchemaInAdditionToHooks_StillRunsOnValidate()
	{
		// Act
		var created = ZodHookEmail.Create("allowed");

		// Assert
		await Assert.That(created.Value).IsEqualTo("allowed");
		await Assert.That(() => ZodHookEmail.Create("denied")).Throws<ArgumentException>();
	}

	[Test]
	public async Task ZodRefinementEmail_GivenHookIssue_CreateThrowsWithHookCode()
	{
		// Act
		var created = ZodRefinementEmail.Create("demo@example.com");

		// Assert
		await Assert.That(created.Value).IsEqualTo("demo@example.com");

		var exception = await Assert
			.That(() => ZodRefinementEmail.Create("demo@example.invalid"))
			.Throws<ZodException>();
		await Assert.That(exception!.Errors.Any(error => error.Code == "invalid_domain")).IsTrue();

		// Hydrate stays replay-safe: the refinement hook is not re-run.
		await Assert.That(ZodRefinementEmail.Hydrate("demo@example.invalid").Value).IsEqualTo("demo@example.invalid");
	}

	[Test]
	public async Task ZodRefinementMoney_GivenHookIssue_CreateThrowsWithHookCode()
	{
		// Act
		var created = ZodRefinementMoney.Create(10m, "EUR");

		// Assert
		await Assert.That(created.Amount).IsEqualTo(10m);

		var exception = await Assert.That(() => ZodRefinementMoney.Create(0m, "EUR")).Throws<ZodException>();
		await Assert.That(exception!.Errors.Any(error => error.Code == "invalid_amount")).IsTrue();

		// Hydrate stays replay-safe: the refinement hook is not re-run.
		await Assert.That(ZodRefinementMoney.Hydrate(0m, "EUR").Amount).IsEqualTo(0m);
	}

	[Test]
	public async Task ZodInsteadOfHooksEmail_GivenInsteadOfHooksMode_DoesNotRunOnValidate()
	{
		// Act
		var created = ZodInsteadOfHooksEmail.Create("anything");

		// Assert
		await Assert.That(created.Value).IsEqualTo("anything");
	}
}
