namespace Purview.ValueObjects.SourceGenerator.Generators;

/// <summary>
/// Tests the generated <c>HasValue</c> member. It is emitted for a scalar whose underlying value can be
/// null: a nullable scalar, or any reference-backed scalar (whose <c>Empty</c>/<c>Hydrate(null!)</c>
/// value is null even when the domain contract says non-null). It never validates or normalizes.
/// </summary>
public sealed class ScalarHasValueTests : ValueObjectSourceGeneratorTestBase
{
	const string Source = """
		namespace Testing
		{
		[Purview.ValueObjects.Serialization.Scalar<string>(Nullable = true)]
		public readonly partial record struct NullableText { }

		[Purview.ValueObjects.Serialization.Scalar<System.DateTimeOffset>(Nullable = true)]
		public readonly partial record struct OptionalTimestamp { }

		[Purview.ValueObjects.Serialization.Scalar<string>]
		public readonly partial record struct RequiredText { }

		[Purview.ValueObjects.Serialization.Scalar<System.Guid>]
		public readonly partial record struct RequiredId { }

		[Purview.ValueObjects.Serialization.Scalar]
		public readonly partial record struct ManualNullable
		{
			public string? Value { get; init; }
		}

		[Purview.ValueObjects.Serialization.Scalar<string>(Nullable = true)]
		public readonly partial record struct DeclaredHasValue
		{
			public bool HasValue => true;
		}

		public static class Harness
		{
			public static bool[] NullableShapes()
			{
				return
				[
					NullableText.Null().HasValue == false,
					NullableText.Create("value").HasValue,
					default(NullableText).HasValue == false,
					NullableText.Empty.HasValue == false,
					OptionalTimestamp.Null().HasValue == false,
					OptionalTimestamp.Create(System.DateTimeOffset.UnixEpoch).HasValue,
					ManualNullable.Null().HasValue == false,
					ManualNullable.Create("value").HasValue,
				];
			}

			public static bool[] ReferenceBackedShapes()
			{
				return
				[
					RequiredText.Empty.HasValue == false,
					RequiredText.Create("value").HasValue,
					RequiredText.Hydrate(null!).HasValue == false,
				];
			}

			public static bool DeclaredHasValueIsNotDuplicated() =>
				typeof(DeclaredHasValue)
					.GetProperties()
					.Count(property => property.Name == nameof(DeclaredHasValue.HasValue)) == 1
				&& DeclaredHasValue.Null().HasValue;
		}
		}
		""";

	async Task<System.Reflection.Assembly> GenerateAssemblyAsync(CancellationToken cancellationToken)
	{
		using var result = await GenerateAsync(
			Source,
			ValueObjectsGeneratorTestOptions.Default.Compile(),
			cancellationToken
		);
		return await Assert.That(result.CompilationResult.Assembly).IsNotNull();
	}

	static async Task AssertResultsAsync(System.Reflection.Assembly assembly, string methodName, bool[] expected)
	{
		var harness = assembly.GetType("Testing.Harness")!;
		var actual = (bool[])harness.GetMethod(methodName)!.Invoke(null, null)!;

		await Assert.That(string.Join(",", actual)).IsEqualTo(string.Join(",", expected));
	}

	[Test]
	public async Task HasValue_GivenNullableScalars_ReportsWhetherTheValueIsNull(CancellationToken cancellationToken)
	{
		var assembly = await GenerateAssemblyAsync(cancellationToken);

		await AssertResultsAsync(assembly, "NullableShapes", [true, true, true, true, true, true, true, true]);
	}

	[Test]
	public async Task HasValue_GivenReferenceBackedScalar_ReportsWhetherTheValueIsNull(
		CancellationToken cancellationToken
	)
	{
		var assembly = await GenerateAssemblyAsync(cancellationToken);

		await AssertResultsAsync(assembly, "ReferenceBackedShapes", [true, true, true]);
	}

	[Test]
	public async Task HasValue_GivenNonNullableValueTypeScalar_IsNotGenerated(CancellationToken cancellationToken)
	{
		var assembly = await GenerateAssemblyAsync(cancellationToken);

		var requiredId = assembly.GetType("Testing.RequiredId")!;
		await Assert.That(requiredId.GetProperty("HasValue")).IsNull();
	}

	[Test]
	public async Task HasValue_GivenAuthorDeclaredMember_IsNotDuplicated(CancellationToken cancellationToken)
	{
		var assembly = await GenerateAssemblyAsync(cancellationToken);
		var harness = assembly.GetType("Testing.Harness")!;

		var notDuplicated = (bool)harness.GetMethod("DeclaredHasValueIsNotDuplicated")!.Invoke(null, null)!;

		await Assert.That(notDuplicated).IsTrue();
	}
}
