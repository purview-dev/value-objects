using ZodSharp.Core;
using ZodSharp.Rules;

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

	[Test]
	public async Task AssetId_GivenNonSentinelRule_ReportsInvalidValueThroughCreate()
	{
		// Act — the shipped [NonSentinel] attribute maps to the built-in NonSentinelRule<Guid>, which the
		// ZodSharp generator adapts to the scalar automatically.
		var created = AssetId.Create(Guid.NewGuid());

		// Assert
		await Assert.That(created.Value).IsNotEqualTo(Guid.Empty);

		var exception = await Assert.That(() => AssetId.Create(Guid.Empty)).Throws<ZodException>();
		var error = exception!.Errors.Single();
		await Assert.That(error.Code).IsEqualTo(NonSentinelRule<Guid>.ErrorCode);
		await Assert.That(error.Message).IsEqualTo("AssetId must not be empty.");
		// Built-in rules own their code but report no structured origin.
		await Assert.That(error.Origin).IsNull();
		// A scalar is validated as a unit, so the adapted rule reports an empty path.
		await Assert.That(error.Path).IsEmpty();

		// Hydrate stays replay-safe: the rule is not re-run.
		await Assert.That(AssetId.Hydrate(Guid.Empty).Value).IsEqualTo(Guid.Empty);
	}

	[Test]
	public async Task ScalarRuleAdapter_GivenUnderlyingRule_ValidatesScalarAsUnit()
	{
		// Arrange: the adapter is the seam that turns a normal underlying-value rule into a scalar rule.
		ScalarRuleAdapter<UserId, Guid, NonSentinelRule<Guid>> adapter = new(new NonSentinelRule<Guid>());

		// Act & Assert
		await Assert.That(adapter.IsValid(UserId.Hydrate(Guid.NewGuid()))).IsTrue();
		await Assert.That(adapter.IsValid(UserId.Hydrate(Guid.Empty))).IsFalse();
	}

	[Test]
	public async Task UserId_GivenNonSentinelRule_IsAdaptedAndValidatesScalarAsUnit()
	{
		// Act — the same built-in attribute serves every scalar backed by the same primitive.
		var created = UserId.Create(Guid.NewGuid());

		// Assert
		await Assert.That(created.Value).IsNotEqualTo(Guid.Empty);

		var exception = await Assert.That(() => UserId.Create(Guid.Empty)).Throws<ZodException>();
		var error = exception!.Errors.Single();
		await Assert.That(error.Code).IsEqualTo(NonSentinelRule<Guid>.ErrorCode);
		await Assert.That(error.Message).IsEqualTo("UserId must not be empty.");
		await Assert.That(error.Path).IsEmpty();

		// Hydrate stays replay-safe: the rule is not re-run.
		await Assert.That(UserId.Hydrate(Guid.Empty).Value).IsEqualTo(Guid.Empty);
	}

	[Test]
	public async Task ExternalId_GivenNonSentinelRule_IsAdaptedAndRejectsSentinel()
	{
		// Act
		var created = ExternalId.Create(Guid.NewGuid());

		// Assert
		await Assert.That(created.Value).IsNotEqualTo(Guid.Empty);

		var exception = await Assert.That(() => ExternalId.Create(Guid.Empty)).Throws<ZodException>();
		var error = exception!.Errors.Single();
		await Assert.That(error.Code).IsEqualTo(NonSentinelRule<Guid>.ErrorCode);
		await Assert.That(error.Message).IsEqualTo("ExternalId must not be empty.");
		await Assert.That(error.Path).IsEmpty();

		// Hydrate stays replay-safe: the rule is not re-run.
		await Assert.That(ExternalId.Hydrate(Guid.Empty).Value).IsEqualTo(Guid.Empty);
	}

	[Test]
	public async Task CorrelationId_GivenNonSentinelCodeOverride_ReportsTheOverride()
	{
		// Act — the built-in rule's code parameter is set through the attribute, so the reported code is the
		// override rather than the rule's ErrorCode default.
		var exception = await Assert.That(() => CorrelationId.Create(Guid.Empty)).Throws<ZodException>();

		// Assert
		var error = exception!.Errors.Single();
		await Assert.That(error.Code).IsEqualTo("invalid_correlation_id");
		await Assert.That(error.Message).IsEqualTo("CorrelationId must not be empty.");
		await Assert.That(error.Path).IsEmpty();
	}

	[Test]
	public async Task ContactDto_GivenShippedMemberAttributes_ValidatesMembers()
	{
		// Arrange — [Email], [E164], [UUID], and [MinLengthZod] all ship in ZodSharp.Rules.
		ContactDto valid = new()
		{
			Email = "demo@example.com",
			Phone = "+14155552671",
			Id = "123e4567-e89b-42d3-a456-426614174000",
			Code = "abc",
		};

		// Act
		var validResult = ContactDtoSchema.Validate(valid);

		// Assert
		await Assert.That(validResult.IsSuccess).IsTrue();

		ContactDto invalid = new()
		{
			Email = "not-an-email",
			Phone = "not-a-phone",
			Id = "not-a-uuid",
			Code = "ab",
		};

		var invalidResult = ContactDtoSchema.Validate(invalid);
		await Assert.That(invalidResult.IsSuccess).IsFalse();

		var codes = invalidResult.Errors.Select(error => error.Code).ToHashSet(StringComparer.Ordinal);
		await Assert.That(codes.Contains(EmailRule.ErrorCode)).IsTrue();
		await Assert.That(codes.Contains(MinLengthRule.ErrorCode)).IsTrue();
	}
}
