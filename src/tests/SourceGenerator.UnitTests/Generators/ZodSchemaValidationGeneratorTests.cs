namespace Purview.ValueObjects.SourceGenerator.Generators;

/// <summary>
/// Tests the value-object generator's ZodSharp integration: when a value object is also annotated
/// with <c>[ZodSchema]</c>, the generated <c>Create</c> validates the constructed instance through
/// the source-generated schema class.
/// </summary>
public sealed class ZodSchemaValidationGeneratorTests : ValueObjectSourceGeneratorTestBase
{
	[Test]
	public async Task Scalar_GivenZodSchema_GeneratedCreateValidatesViaSchema(CancellationToken cancellationToken)
	{
		const string source = """
			using ZodSharp;
			using ZodSharp.Core;

			namespace ZodSharp
			{
				[System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
				public sealed class ZodSchemaAttribute : System.Attribute
				{
					public string? SchemaName { get; init; }
				}
			}

			namespace Testing
			{
				[ZodSchema]
				public static class EmailAddressSchema
				{
					public static ValidationResult<EmailAddress> Validate(EmailAddress value) =>
						value.Value.Contains('@', System.StringComparison.Ordinal)
							? ValidationResult<EmailAddress>.Success(value)
							: ValidationResult<EmailAddress>.Failure(
								new ValidationError("invalid", "Invalid email.", [nameof(value)])
							);
				}

				[Purview.ValueObjects.Serialization.Scalar]
				[ZodSchema]
				public readonly partial record struct EmailAddress
				{
					public string Value { get; }

					private EmailAddress(string value) => Value = value;
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

		var result = await GenerateAsync(source, ValueObjectsGeneratorTestOptions.Default.Compile(), cancellationToken);

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
			using ZodSharp.Core;

			namespace ZodSharp
			{
				[System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
				public sealed class ZodSchemaAttribute : System.Attribute
				{
					public string? SchemaName { get; init; }
				}
			}

			namespace Testing
			{
				[ZodSchema]
				public static class EmailAddressSchema
				{
					public static ValidationResult<EmailAddress> Validate(EmailAddress value) =>
						ValidationResult<EmailAddress>.Success(value);
				}

				[Purview.ValueObjects.Serialization.Scalar]
				[ZodSchema]
				public readonly partial record struct EmailAddress
				{
					public string Value { get; }

					private EmailAddress(string value) => Value = value;

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

		var result = await GenerateAsync(source, ValueObjectsGeneratorTestOptions.Default.Compile(), cancellationToken);

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
			using ZodSharp.Core;

			namespace ZodSharp
			{
				[System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
				public sealed class ZodSchemaAttribute : System.Attribute
				{
					public string? SchemaName { get; init; }
				}
			}

			namespace Testing
			{
				[ZodSchema]
				public static class EmailAddressSchema
				{
					public static ValidationResult<EmailAddress> Validate(EmailAddress value) =>
						ValidationResult<EmailAddress>.Success(value);
				}

				[Purview.ValueObjects.Serialization.Scalar(ZodSchemaMode = Purview.ValueObjects.Serialization.ZodSchemaMode.InsteadOfHooks)]
				[ZodSchema]
				public readonly partial record struct EmailAddress
				{
					public string Value { get; }

					private EmailAddress(string value) => Value = value;

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

		var result = await GenerateAsync(source, ValueObjectsGeneratorTestOptions.Default.Compile(), cancellationToken);

		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harness = assembly!.GetType("Testing.Harness")!;

		var skipped = (bool)harness.GetMethod("CreateSkipsOnValidate")!.Invoke(null, null)!;

		await Assert.That(skipped).IsTrue();
	}

	[Test]
	public async Task Scalar_GivenZodSchemaWithCustomSchemaName_UsesThatSchemaClass(CancellationToken cancellationToken)
	{
		const string source = """
			using ZodSharp;
			using ZodSharp.Core;

			namespace ZodSharp
			{
				[System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
				public sealed class ZodSchemaAttribute : System.Attribute
				{
					public string? SchemaName { get; init; }
				}
			}

			namespace Testing
			{
				[ZodSchema(SchemaName = "EmailRules")]
				public static class EmailRules
				{
					public static ValidationResult<EmailAddress> Validate(EmailAddress value) =>
						value.Value.Contains('@', System.StringComparison.Ordinal)
							? ValidationResult<EmailAddress>.Success(value)
							: ValidationResult<EmailAddress>.Failure(
								new ValidationError("invalid", "Invalid email.", [nameof(value)])
							);
				}

				[Purview.ValueObjects.Serialization.Scalar]
				[ZodSchema(SchemaName = "EmailRules")]
				public readonly partial record struct EmailAddress
				{
					public string Value { get; }

					private EmailAddress(string value) => Value = value;
				}

				public static class Harness
				{
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

		var result = await GenerateAsync(source, ValueObjectsGeneratorTestOptions.Default.Compile(), cancellationToken);

		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harness = assembly!.GetType("Testing.Harness")!;

		var rejects = (bool)harness.GetMethod("CreateRejectsInvalid")!.Invoke(null, null)!;

		await Assert.That(rejects).IsTrue();
	}
}
