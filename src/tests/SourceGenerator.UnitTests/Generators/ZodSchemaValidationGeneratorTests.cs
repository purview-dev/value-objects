namespace Purview.ValueObjects.SourceGenerator.Generators;

/// <summary>
/// Tests the value-object generator's ZodSharp integration against the real Purview.ZodSharp source
/// generator: when a value object is also annotated with <c>[ZodSchema]</c> (the attribute emitted by
/// the ZodSharp generator), the generated <c>Create</c> validates the constructed instance through
/// the schema class the ZodSharp generator produces.
/// </summary>
public sealed class ZodSchemaValidationGeneratorTests
	: ValueObjectSourceGeneratorTestBase<ZodSchemaValidationGeneratorTestOptions>
{
	[Test]
	public async Task Scalar_GivenZodSchema_GeneratedCreateValidatesViaSchema(CancellationToken cancellationToken)
	{
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				[Scalar]
				[ZodSchema]
				public readonly partial record struct EmailAddress
				{
					[EmailAddress]
					[StringLength(254, MinimumLength = 3)]
					public string Value { get; }
				}

				public static class Harness
				{
					public static bool CreateSucceeds() =>
						EmailAddress.Create("demo@example.com").Value == "demo@example.com";

					public static bool CreateRejectsInvalid()
					{
						try
						{
							EmailAddress.Create("not-an-email");
							return false;
						}
						catch (global::ZodSharp.Core.ZodException)
						{
							return true;
						}
					}
				}
			}
			""";

		var result = await GenerateAsync(source, ZodSchemaValidationGeneratorTestOptions.Compile, cancellationToken);

		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harness = assembly!.GetType("Testing.Harness")!;

		var succeeds = (bool)harness.GetMethod("CreateSucceeds")!.Invoke(null, null)!;
		var rejects = (bool)harness.GetMethod("CreateRejectsInvalid")!.Invoke(null, null)!;

		await Assert.That(succeeds).IsTrue();
		await Assert.That(rejects).IsTrue();
	}

	[Test]
	public async Task Scalar_GivenZodSchema_InAdditionToHooks_RunsOnValidateToo(CancellationToken cancellationToken)
	{
		const string source = """
			using ZodSharp;

			namespace Testing
			{
				[Scalar]
				[ZodSchema]
				public readonly partial record struct EmailAddress
				{
					public string Value { get; }

					static partial void OnValidate(string value)
					{
						if (value != "allowed")
							throw new System.ArgumentException("Only 'allowed' is accepted.", nameof(value));
					}
				}

				public static class Harness
				{
					public static bool OnValidateStillRuns() =>
						EmailAddress.Create("allowed").Value == "allowed";

					public static bool OnValidateRejects()
					{
						try
						{
							EmailAddress.Create("denied");
							return false;
						}
						catch (System.ArgumentException)
						{
							return true;
						}
					}
				}
			}
			""";

		var result = await GenerateAsync(source, ZodSchemaValidationGeneratorTestOptions.Compile, cancellationToken);

		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harness = assembly!.GetType("Testing.Harness")!;

		var runs = (bool)harness.GetMethod("OnValidateStillRuns")!.Invoke(null, null)!;
		var rejects = (bool)harness.GetMethod("OnValidateRejects")!.Invoke(null, null)!;

		await Assert.That(runs).IsTrue();
		await Assert.That(rejects).IsTrue();
	}

	[Test]
	public async Task Scalar_GivenZodSchemaInsteadOfHooks_DoesNotRunOnValidate(CancellationToken cancellationToken)
	{
		const string source = """
			using ZodSharp;

			namespace Testing
			{
				[Scalar(ZodSchemaMode = Purview.ValueObjects.Serialization.ZodSchemaMode.InsteadOfHooks)]
				[ZodSchema]
				public readonly partial record struct EmailAddress
				{
					public string Value { get; }

					static partial void OnValidate(string value)
					{
						throw new System.ArgumentException("OnValidate must not run.", nameof(value));
					}
				}

				public static class Harness
				{
					public static bool CreateSkipsOnValidate() =>
						EmailAddress.Create("anything").Value == "anything";
				}
			}
			""";

		var result = await GenerateAsync(source, ZodSchemaValidationGeneratorTestOptions.Compile, cancellationToken);

		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harness = assembly!.GetType("Testing.Harness")!;

		var skipped = (bool)harness.GetMethod("CreateSkipsOnValidate")!.Invoke(null, null)!;

		await Assert.That(skipped).IsTrue();
	}

	[Test]
	public async Task Scalar_GivenZodSchema_GeneratedHookIsInvokedByCreate(CancellationToken cancellationToken)
	{
		const string source = """
			using ZodSharp;

			namespace Testing
			{
				[Scalar]
				[ZodSchema]
				public readonly partial record struct EmailAddress
				{
					public string Value { get; }

					static partial void OnNormalize(ref string value) => value = value?.Trim().ToLowerInvariant()!;

					partial void OnZodValidate(global::ZodSharp.Schemas.RefineCtx<EmailAddress> context)
					{
						if (context.Value.Value.EndsWith(".invalid", System.StringComparison.Ordinal))
							context.AddIssue("invalid_domain", "Domain is not allowed.", [nameof(Value)]);
					}
				}

				public static class Harness
				{
					public static bool CreateAcceptsValid() =>
						EmailAddress.Create(" Demo@Example.com ").Value == "demo@example.com";

					public static string? CreateReportsHookIssue()
					{
						try
						{
							EmailAddress.Create("demo@example.invalid");
							return null;
						}
						catch (global::ZodSharp.Core.ZodException exception)
						{
							return exception.Errors.Length == 1 ? exception.Errors[0].Code : null;
						}
					}

					public static bool HydrateSkipsHook() =>
						EmailAddress.Hydrate("demo@example.invalid").Value == "demo@example.invalid";
				}
			}
			""";

		var result = await GenerateAsync(source, ZodSchemaValidationGeneratorTestOptions.Compile, cancellationToken);
		var query = result.Generated();

		var emailAddress = query.GetRecord("EmailAddress", "Testing");

		// The ZodSharp generator declares and invokes the refinement hook; the value object generator only
		// validates through the generated schema, so nothing hook-shaped is emitted here.
		await Assert.That(emailAddress.HasMethod("OnZodValidate")).IsFalse();
		await Assert.That(emailAddress.HasMethod("Validate")).IsFalse();

		var createBody = emailAddress.GetMethod("Create").Node.Body?.ToString() ?? string.Empty;
		await Assert.That(createBody).Contains("EmailAddressSchema.Validate(instance)");

		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harness = assembly!.GetType("Testing.Harness")!;

		var acceptsValid = (bool)harness.GetMethod("CreateAcceptsValid")!.Invoke(null, null)!;
		var hookCode = (string?)harness.GetMethod("CreateReportsHookIssue")!.Invoke(null, null);
		var hydrateSkips = (bool)harness.GetMethod("HydrateSkipsHook")!.Invoke(null, null)!;

		await Assert.That(acceptsValid).IsTrue();
		await Assert.That(hookCode).IsEqualTo("invalid_domain");
		await Assert.That(hydrateSkips).IsTrue();
	}

	[Test]
	public async Task Complex_GivenZodSchema_GeneratedHookIsInvokedByCreate(CancellationToken cancellationToken)
	{
		const string source = """
			using ZodSharp;

			namespace Testing
			{
				[ValueObject]
				[ZodSchema]
				public readonly partial record struct Money(decimal Amount, string Currency)
				{
					partial void OnZodValidate(global::ZodSharp.Schemas.RefineCtx<Money> context)
					{
						if (context.Value.Amount <= 0)
							context.AddIssue("invalid_amount", "Amount must be positive.", [nameof(Amount)]);
					}
				}

				public static class Harness
				{
					public static bool CreateAcceptsValid() => Money.Create(10m, "EUR").Amount == 10m;

					public static string? CreateReportsHookIssue()
					{
						try
						{
							Money.Create(0m, "EUR");
							return null;
						}
						catch (global::ZodSharp.Core.ZodException exception)
						{
							return exception.Errors.Length == 1 ? exception.Errors[0].Code : null;
						}
					}
				}
			}
			""";

		var result = await GenerateAsync(source, ZodSchemaValidationGeneratorTestOptions.Compile, cancellationToken);
		var query = result.Generated();

		var money = query.GetRecord("Money", "Testing");
		// The ZodSharp generator owns the hook; the value object generator only validates through the schema.
		await Assert.That(money.HasMethod("OnZodValidate")).IsFalse();

		var createBody = money.GetMethod("Create").Node.Body?.ToString() ?? string.Empty;
		await Assert.That(createBody).Contains("MoneySchema.Validate(instance)");

		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harness = assembly!.GetType("Testing.Harness")!;

		var acceptsValid = (bool)harness.GetMethod("CreateAcceptsValid")!.Invoke(null, null)!;
		var hookCode = (string?)harness.GetMethod("CreateReportsHookIssue")!.Invoke(null, null);

		await Assert.That(acceptsValid).IsTrue();
		await Assert.That(hookCode).IsEqualTo("invalid_amount");
	}

	[Test]
	public async Task Scalar_GivenInsteadOfHooksWithOnValidate_ReportsOnValidateSkipped(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			using ZodSharp;

			namespace Testing
			{
				[Scalar(ZodSchemaMode = Purview.ValueObjects.Serialization.ZodSchemaMode.InsteadOfHooks)]
				[ZodSchema]
				public readonly partial record struct EmailAddress
				{
					public string Value { get; }

					static partial void OnValidate(string value)
					{
						throw new System.ArgumentException("OnValidate must not run.", nameof(value));
					}
				}
			}
			""";

		var result = await GenerateAsync(source, ZodSchemaValidationGeneratorTestOptions.Default, cancellationToken);

		await Assert.That(result).HasDiagnostic("VO1013");
	}

	[Test]
	public async Task Scalar_GivenZodSchemaName_GeneratedCreateValidatesThroughConfiguredSchema(
		CancellationToken cancellationToken
	)
	{
		// SchemaName is honoured by ZodSharp, so both generators must agree on the configured schema class
		// name: the generated Create validates through it and it is the class ZodSharp emits.
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				[Scalar]
				[ZodSchema(SchemaName = "CorporateEmailSchema")]
				public readonly partial record struct CorporateEmail
				{
					[EmailAddress]
					public string Value { get; }
				}

				public static class Harness
				{
					public static bool CreateAcceptsValid() =>
						CorporateEmail.Create("demo@example.com").Value == "demo@example.com";

					public static bool CreateRejectsInvalid()
					{
						try
						{
							CorporateEmail.Create("not-an-email");
							return false;
						}
						catch (global::ZodSharp.Core.ZodException)
						{
							return true;
						}
					}
				}
			}
			""";

		var result = await GenerateAsync(source, ZodSchemaValidationGeneratorTestOptions.Compile, cancellationToken);

		var query = result.Generated();
		await Assert.That(query.GetClass("CorporateEmailSchema", "Testing").Node).IsNotNull();

		var createBody =
			query.GetRecord("CorporateEmail", "Testing").GetMethod("Create").Node.Body?.ToString() ?? string.Empty;
		await Assert.That(createBody).Contains("CorporateEmailSchema.Validate(instance)");

		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		await Assert.That(assembly!.GetType("Testing.CorporateEmailSchema")).IsNotNull();

		var harness = assembly.GetType("Testing.Harness")!;
		var acceptsValid = (bool)harness.GetMethod("CreateAcceptsValid")!.Invoke(null, null)!;
		var rejectsInvalid = (bool)harness.GetMethod("CreateRejectsInvalid")!.Invoke(null, null)!;

		await Assert.That(acceptsValid).IsTrue();
		await Assert.That(rejectsInvalid).IsTrue();
	}

	[Test]
	public async Task Scalar_GivenNonSentinelRule_IsAdaptedThroughCreate(CancellationToken cancellationToken)
	{
		// The built-in [NonSentinel] attribute (shipped in ZodSharp.Rules) is written against the underlying
		// value, so the ZodSharp generator closes it with the scalar's value type and wraps it in the
		// ScalarRuleAdapter the value-object generator emits. The wrapped rule owns the error identity and
		// reports an empty path.
		const string source = """
			using System;
			using ZodSharp;
			using ZodSharp.Rules;

			namespace Testing
			{
				[Scalar]
				[ZodSchema]
				[NonSentinel(Message = "UserId must not be empty.")]
				public readonly partial record struct UserId
				{
					public Guid Value { get; }
				}

				public static class Harness
				{
					public static bool CreateSucceeds() => UserId.Create(Guid.NewGuid()).Value != Guid.Empty;

					public static string? CreateReportsCode()
					{
						try
						{
							UserId.Create(Guid.Empty);
							return null;
						}
						catch (global::ZodSharp.Core.ZodException exception)
						{
							return exception.Errors.Length == 1 ? exception.Errors[0].Code : "unexpected-count";
						}
					}

					public static int CreateReportsPathLength()
					{
						try
						{
							UserId.Create(Guid.Empty);
							return -1;
						}
						catch (global::ZodSharp.Core.ZodException exception)
						{
							return exception.Errors[0].Path.Length;
						}
					}

					public static bool HydrateIsReplaySafe() => UserId.Hydrate(Guid.Empty).Value == Guid.Empty;
				}
			}
			""";

		var result = await GenerateAsync(source, ZodSchemaValidationGeneratorTestOptions.Compile, cancellationToken);

		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harness = assembly!.GetType("Testing.Harness")!;

		var createSucceeds = (bool)harness.GetMethod("CreateSucceeds")!.Invoke(null, null)!;
		var code = (string?)harness.GetMethod("CreateReportsCode")!.Invoke(null, null);
		var pathLength = (int)harness.GetMethod("CreateReportsPathLength")!.Invoke(null, null)!;
		var hydrateSafe = (bool)harness.GetMethod("HydrateIsReplaySafe")!.Invoke(null, null)!;

		await Assert.That(createSucceeds).IsTrue();
		await Assert.That(code).IsEqualTo("invalid_value");
		await Assert.That(pathLength).IsEqualTo(0);
		await Assert.That(hydrateSafe).IsTrue();
	}

	[Test]
	public async Task Class_GivenShippedMemberAttributes_ValidatesMembers(CancellationToken cancellationToken)
	{
		// [Email], [E164], [UUID], and [MinLengthZod] all ship in ZodSharp.Rules, so a consumer validates a
		// DTO's members without hand-authoring a single rule attribute.
		const string source = """
			using System;
			using ZodSharp;
			using ZodSharp.Rules;

			namespace Testing
			{
				[ZodSchema]
				public sealed partial class ContactDto
				{
					[Email]
					public string Email { get; init; } = string.Empty;

					[E164]
					public string Phone { get; init; } = string.Empty;

					[UUID(UuidVersion.V4)]
					public string Id { get; init; } = string.Empty;

					[MinLengthZod(3)]
					public string Code { get; init; } = string.Empty;
				}

				public static class Harness
				{
					public static bool ValidPasses() =>
						ContactDtoSchema.Validate(
							new ContactDto
							{
								Email = "demo@example.com",
								Phone = "+14155552671",
								Id = "123e4567-e89b-42d3-a456-426614174000",
								Code = "abc",
							}
						).IsSuccess;

					public static bool InvalidFails() =>
						!ContactDtoSchema.Validate(
							new ContactDto
							{
								Email = "not-an-email",
								Phone = "not-a-phone",
								Id = "not-a-uuid",
								Code = "ab",
							}
						).IsSuccess;
				}
			}
			""";

		var result = await GenerateAsync(source, ZodSchemaValidationGeneratorTestOptions.Compile, cancellationToken);

		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harness = assembly!.GetType("Testing.Harness")!;

		var validPasses = (bool)harness.GetMethod("ValidPasses")!.Invoke(null, null)!;
		var invalidFails = (bool)harness.GetMethod("InvalidFails")!.Invoke(null, null)!;

		await Assert.That(validPasses).IsTrue();
		await Assert.That(invalidFails).IsTrue();
	}
}
