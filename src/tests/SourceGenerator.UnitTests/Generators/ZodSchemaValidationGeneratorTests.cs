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
}
