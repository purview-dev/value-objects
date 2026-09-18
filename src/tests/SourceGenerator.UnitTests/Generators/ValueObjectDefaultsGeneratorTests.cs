using System.Reflection;

namespace Purview.ValueObjects.SourceGenerator.Generators;

/// <summary>
/// Tests the assembly-level <c>[ValueObjectDefaults]</c> attribute: defaults set on the assembly apply
/// to every value object unless the type explicitly overrides them.
/// </summary>
public sealed class ValueObjectDefaultsGeneratorTests : ValueObjectSourceGeneratorTestBase
{
	[Test]
	public async Task Scalar_GivenAssemblyDefaultGenerateJsonConverterFalse_DoesNotGenerateConverter(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			[assembly: Purview.ValueObjects.Serialization.ValueObjectDefaults(GenerateJsonConverter = false)]

			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar]
				public readonly partial record struct EmailAddress
				{
					public string Value { get; }

					private EmailAddress(string value) => Value = value;
				}
			}
			""";

		var result = await GenerateAsync(source, cancellationToken);

		await Assert.That(result.Generated().HasClass("EmailAddressJsonConverter", "Testing")).IsFalse();
	}

	[Test]
	public async Task Scalar_GivenAssemblyDefaultGenerateJsonConverterFalse_TypeOverrideWins(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			[assembly: Purview.ValueObjects.Serialization.ValueObjectDefaults(GenerateJsonConverter = false)]

			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar(GenerateJsonConverter = true)]
				public readonly partial record struct EmailAddress
				{
					public string Value { get; }

					private EmailAddress(string value) => Value = value;
				}
			}
			""";

		var result = await GenerateAsync(source, cancellationToken);

		await Assert.That(result.Generated().HasClass("EmailAddressJsonConverter", "Testing")).IsTrue();
	}

	[Test]
	public async Task Complex_GivenAssemblyDefaultGenerateConstructorFalse_DoesNotGenerateParameterlessCtor(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			[assembly: Purview.ValueObjects.Serialization.ValueObjectDefaults(GenerateConstructor = false)]

			namespace Testing
			{
				[Purview.ValueObjects.Serialization.ValueObject]
				public readonly partial record struct Money
				{
					public decimal Amount { get; }

					public string Currency { get; }

					private Money(decimal amount, string currency)
					{
						Amount = amount;
						Currency = currency;
					}
				}
			}
			""";

		var result = await GenerateAsync(source, cancellationToken);

		var money = result.Generated().GetRecord("Money", "Testing");
		await Assert.That(money.TryGetConstructor(out _, [])).IsFalse();
	}

	[Test]
	public async Task Complex_GivenAssemblyDefaultGenerateConstructorFalse_TypeOverrideWins(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			[assembly: Purview.ValueObjects.Serialization.ValueObjectDefaults(GenerateConstructor = false)]

			namespace Testing
			{
				[Purview.ValueObjects.Serialization.ValueObject(GenerateConstructor = true)]
				public readonly partial record struct Money
				{
					public decimal Amount { get; }

					public string Currency { get; }

					private Money(decimal amount, string currency)
					{
						Amount = amount;
						Currency = currency;
					}
				}
			}
			""";

		var result = await GenerateAsync(source, cancellationToken);

		var money = result.Generated().GetRecord("Money", "Testing");
		await Assert.That(money.TryGetConstructor(out _, [])).IsTrue();
	}

	[Test]
	public async Task Scalar_GivenAssemblyDefaultZodSchemaModeInsteadOfHooks_SkipsOnValidate(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			using ZodSharp;
			using ZodSharp.Core;

			[assembly: Purview.ValueObjects.Serialization.ValueObjectDefaults(ZodSchemaMode = Purview.ValueObjects.Serialization.ZodSchemaMode.InsteadOfHooks)]

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
}
