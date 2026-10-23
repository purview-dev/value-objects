using System.Reflection;

namespace Purview.ValueObjects.SourceGenerator.Generators;

/// <summary>
/// Tests the single nullability flag every scalar derives. The automatic form takes it from
/// <c>Nullable = true</c> (or an already-nullable type argument); the manual form takes it from the
/// declared property. Both feed <c>Null()</c>, the JSON contract and the Entity Framework mapping.
/// </summary>
public sealed class ScalarNullabilityTests : ValueObjectSourceGeneratorTestBase
{
	const string Source = """
		namespace Testing
		{
		[Purview.ValueObjects.Serialization.Scalar<string>(Nullable = true)]
		public readonly partial record struct AutoNullable { }

		[Purview.ValueObjects.Serialization.Scalar<string>]
		public readonly partial record struct AutoRequired { }

		[Purview.ValueObjects.Serialization.Scalar]
		public readonly partial record struct ManualNullable
		{
			public string? Value { get; init; }
		}

		[Purview.ValueObjects.Serialization.Scalar]
		public readonly partial record struct ManualRequired
		{
			public string Value { get; init; }
		}

		[Purview.ValueObjects.Serialization.Scalar<System.DateTimeOffset>(Nullable = true)]
		public readonly partial record struct AutoNullableValueType { }

		[Purview.ValueObjects.Serialization.Scalar]
		public readonly partial record struct ManualNullableValueType
		{
			public System.DateTimeOffset? Value { get; init; }
		}

		public static class Harness
		{
			public static bool AutoNullableIsNull() => AutoNullable.Null().Value is null;

			public static bool ManualNullableIsNull() => ManualNullable.Null().Value is null;

			public static bool AutoNullableValueTypeIsNull() =>
				AutoNullableValueType.Null().Value is null;

			public static bool ManualNullableValueTypeIsNull() =>
				ManualNullableValueType.Null().Value is null;

			public static string? DeserializeAutoNullable(string json) =>
				System.Text.Json.JsonSerializer.Deserialize<AutoNullable>(json)!.Value;

			public static string? DeserializeManualNullable(string json) =>
				System.Text.Json.JsonSerializer.Deserialize<ManualNullable>(json)!.Value;

			public static System.DateTimeOffset? DeserializeAutoNullableValueType(string json) =>
				System.Text.Json.JsonSerializer.Deserialize<AutoNullableValueType>(json)!.Value;

			public static System.DateTimeOffset? DeserializeManualNullableValueType(string json) =>
				System.Text.Json.JsonSerializer.Deserialize<ManualNullableValueType>(json)!.Value;

			public static bool AutoRequiredRejectsNull()
			{
				try
				{
					System.Text.Json.JsonSerializer.Deserialize<AutoRequired>("null");
					return false;
				}
				catch (System.Text.Json.JsonException)
				{
					return true;
				}
			}

			public static bool ManualRequiredRejectsNull()
			{
				try
				{
					System.Text.Json.JsonSerializer.Deserialize<ManualRequired>("null");
					return false;
				}
				catch (System.Text.Json.JsonException)
				{
					return true;
				}
			}
		}
		}
		""";

	async Task<Assembly> GenerateAssemblyAsync(CancellationToken cancellationToken)
	{
		using var result = await GenerateAsync(
			Source,
			ValueObjectsGeneratorTestOptions.Default.Compile(),
			cancellationToken
		);
		return await Assert.That(result.CompilationResult.Assembly).IsNotNull();
	}

	[Test]
	public async Task Nullability_GivenAutomaticNullable_InfersNullable(CancellationToken cancellationToken)
	{
		var assembly = await GenerateAssemblyAsync(cancellationToken);
		var harness = assembly.GetType("Testing.Harness")!;

		var isNull = (bool)harness.GetMethod("AutoNullableIsNull")!.Invoke(null, null)!;
		var deserialized = harness.GetMethod("DeserializeAutoNullable")!.Invoke(null, ["null"]);

		await Assert.That(isNull).IsTrue();
		await Assert.That(deserialized).IsNull();
	}

	[Test]
	public async Task Nullability_GivenManualNullable_InfersNullable(CancellationToken cancellationToken)
	{
		var assembly = await GenerateAssemblyAsync(cancellationToken);
		var harness = assembly.GetType("Testing.Harness")!;

		var isNull = (bool)harness.GetMethod("ManualNullableIsNull")!.Invoke(null, null)!;
		var deserialized = harness.GetMethod("DeserializeManualNullable")!.Invoke(null, ["null"]);

		await Assert.That(isNull).IsTrue();
		await Assert.That(deserialized).IsNull();
	}

	[Test]
	public async Task Nullability_GivenAutomaticNullableValueType_InfersNullable(CancellationToken cancellationToken)
	{
		var assembly = await GenerateAssemblyAsync(cancellationToken);
		var harness = assembly.GetType("Testing.Harness")!;

		var isNull = (bool)harness.GetMethod("AutoNullableValueTypeIsNull")!.Invoke(null, null)!;
		var deserialized = harness.GetMethod("DeserializeAutoNullableValueType")!.Invoke(null, ["null"]);

		await Assert.That(isNull).IsTrue();
		await Assert.That(deserialized).IsNull();
	}

	[Test]
	public async Task Nullability_GivenManualNullableValueType_InfersNullable(CancellationToken cancellationToken)
	{
		var assembly = await GenerateAssemblyAsync(cancellationToken);
		var harness = assembly.GetType("Testing.Harness")!;

		var isNull = (bool)harness.GetMethod("ManualNullableValueTypeIsNull")!.Invoke(null, null)!;
		var deserialized = harness.GetMethod("DeserializeManualNullableValueType")!.Invoke(null, ["null"]);

		await Assert.That(isNull).IsTrue();
		await Assert.That(deserialized).IsNull();
	}

	[Test]
	public async Task Nullability_GivenNonNullableReference_RejectsNull(CancellationToken cancellationToken)
	{
		var assembly = await GenerateAssemblyAsync(cancellationToken);
		var harness = assembly.GetType("Testing.Harness")!;

		var automatic = (bool)harness.GetMethod("AutoRequiredRejectsNull")!.Invoke(null, null)!;
		var manual = (bool)harness.GetMethod("ManualRequiredRejectsNull")!.Invoke(null, null)!;

		await Assert.That(automatic).IsTrue();
		await Assert.That(manual).IsTrue();
	}

	[Test]
	public async Task Nullability_GivenNonNullableScalar_DoesNotEmitNullFactory(CancellationToken cancellationToken)
	{
		using var result = await GenerateAsync(Source, ValueObjectsGeneratorTestOptions.Default, cancellationToken);

		await Assert.That(result.Generated().GetRecord("AutoRequired", "Testing").HasMethod("Null")).IsFalse();
		await Assert.That(result.Generated().GetRecord("ManualRequired", "Testing").HasMethod("Null")).IsFalse();
	}

	[Test]
	public async Task Nullability_GivenDuplicateDeclaration_DoesNotEmitTheType(CancellationToken cancellationToken)
	{
		// The analyzer reports VO1028, but the generator itself must also refuse to emit: a partial with
		// a second Value property would be CS0102 in code the consumer cannot edit. This run disables
		// the analyzer, so only the generator's blocking decision is observable.
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar<string>(Nullable = true)]
				public readonly partial record struct Handle
				{
					public string? Value { get; init; }
				}
			}
			""";

		using var result = await GenerateAsync(
			source,
			ValueObjectsGeneratorTestOptions.Default.Compile() with
			{
				AnalyzerTypes = [],
			},
			cancellationToken
		);
		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();

		var handle = assembly.GetType("Testing.Handle")!;
		await Assert.That(handle.GetMethod("Create", BindingFlags.Public | BindingFlags.Static)).IsNull();

		var duplicateErrors = result
			.CompilationResult.Compilation.GetDiagnostics(cancellationToken)
			.Where(diagnostic =>
				diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error
				&& diagnostic.Id is "CS0102" or "CS0111"
			)
			.ToArray();
		await Assert.That(duplicateErrors).IsEmpty();
	}

	[Test]
	public async Task Nullability_GivenNullableScalar_DeclaresNullableUnderlyingProperty(
		CancellationToken cancellationToken
	)
	{
		var assembly = await GenerateAssemblyAsync(cancellationToken);

		var automatic = assembly.GetType("Testing.AutoNullable")!.GetProperty("Value")!;
		var manual = assembly.GetType("Testing.ManualNullable")!.GetProperty("Value")!;
		var automaticValueType = assembly.GetType("Testing.AutoNullableValueType")!.GetProperty("Value")!;
		var manualValueType = assembly.GetType("Testing.ManualNullableValueType")!.GetProperty("Value")!;

		NullabilityInfoContext context = new();
		;

		await Assert.That(context.Create(automatic).ReadState).IsEqualTo(NullabilityState.Nullable);
		await Assert.That(context.Create(manual).ReadState).IsEqualTo(NullabilityState.Nullable);
		await Assert.That(automaticValueType.PropertyType).IsEqualTo(typeof(DateTimeOffset?));
		await Assert.That(manualValueType.PropertyType).IsEqualTo(typeof(DateTimeOffset?));
	}
}
