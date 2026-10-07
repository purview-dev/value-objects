namespace Purview.ValueObjects.Serialization;

/// <summary>
/// Covers <c>TryCreate</c> on a ZodSharp-validated scalar.
/// </summary>
/// <remarks>
/// The generated <c>TryCreate</c> caught only <see cref="ArgumentException"/>, but a ZodSharp-validated
/// <c>Create</c> throws <c>ZodException</c>. So for every <c>[ZodSchema]</c> value object — a headline
/// feature with its own documentation page — <c>TryCreate</c> threw instead of returning <see langword="false"/>,
/// and the documented "try" contract did not hold.
/// </remarks>
public class ZodValidatedTryCreateTests
{
	[Test]
	public async Task TryCreate_GivenValidValue_ReturnsTrueAndTheValue()
	{
		// Act
		var created = ZodValidatedEmail.TryCreate("someone@example.com", out var result);

		// Assert
		await Assert.That(created).IsTrue();
		await Assert.That(result.Value).IsEqualTo("someone@example.com");
	}

	[Test]
	public async Task TryCreate_GivenValueFailingZodValidation_ReturnsFalseInsteadOfThrowing()
	{
		// Act — "x" fails both the email rule and the minimum length, so Create throws ZodException.
		var created = ZodValidatedEmail.TryCreate("x", out var result);

		// Assert
		await Assert.That(created).IsFalse();
		await Assert.That(result).IsEqualTo(default(ZodValidatedEmail));
	}

	[Test]
	public async Task TryCreate_GivenValueFailingZodValidation_DoesNotThrow()
	{
		// The regression this guards: the call itself used to throw rather than report failure.
		await Assert.That(() => ZodValidatedEmail.TryCreate("not-an-email", out _)).ThrowsNothing();
	}

	[Test]
	public async Task Create_GivenInvalidValue_StillThrows()
	{
		// TryCreate reporting failure must not change the strict factory's contract.
		await Assert.That(() => ZodValidatedEmail.Create("x")).ThrowsException();
	}
}
