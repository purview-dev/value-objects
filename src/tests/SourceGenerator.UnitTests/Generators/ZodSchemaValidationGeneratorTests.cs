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
}
