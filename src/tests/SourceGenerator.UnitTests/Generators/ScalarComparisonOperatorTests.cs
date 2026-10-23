namespace Purview.ValueObjects.SourceGenerator.Generators;

/// <summary>
/// Regression tests for null comparison operators. Before the extraction conversion became explicit,
/// <c>value == null</c> was ambiguous (CS9342) between the operator against the underlying value and the
/// record's synthesized <c>(T, T)</c> equality. The compile itself is the regression test; the runtime
/// assertions pin the results.
/// </summary>
public sealed class ScalarComparisonOperatorTests : ValueObjectSourceGeneratorTestBase
{
	const string Source = """
		namespace Testing
		{
		[Purview.ValueObjects.Serialization.Scalar<string>(Nullable = true)]
		public readonly partial record struct NullableString { }

		[Purview.ValueObjects.Serialization.Scalar<System.DateTimeOffset>(Nullable = true)]
		public readonly partial record struct OptionalDateTimeOffset { }

		[Purview.ValueObjects.Serialization.Scalar<string>(Trim = true, Casing = Purview.ValueObjects.Serialization.StringCasing.LowerInvariant)]
		public readonly partial record struct NormalizedText { }

		[Purview.ValueObjects.Serialization.Scalar<string>]
		public readonly partial record struct NonNullableText { }

		public static class Harness
		{
			public static bool[] NullComparisons()
			{
				NullableString DataJson = NullableString.Null();
				return
				[
					DataJson == null,
					DataJson != null,
					null == DataJson,
					DataJson == "value",
					"value" == DataJson,
				];
			}

			public static bool[] ValueComparisons()
			{
				NullableString name = NullableString.Create("value");
				return
				[
					name == null,
					name != null,
					name == "value",
					"value" == name,
					name != "other",
					"other" != name,
				];
			}

			public static bool[] NullishValues()
			{
				return
				[
					default(NullableString) == null,
					NullableString.Empty == null,
					NullableString.Null() == null,
					NullableString.Create(null) == null,
					null == NullableString.Create(null),
				];
			}

			public static bool[] ValueTypeComparisons()
			{
				OptionalDateTimeOffset observed = OptionalDateTimeOffset.Null();
				var epoch = System.DateTimeOffset.UnixEpoch;
				return
				[
					observed == null,
					observed != null,
					null == observed,
					observed == epoch,
					epoch == observed,
					observed != epoch,
					OptionalDateTimeOffset.Create(epoch) == epoch,
					epoch == OptionalDateTimeOffset.Create(epoch),
					default(OptionalDateTimeOffset) == null,
					OptionalDateTimeOffset.Empty == null,
					OptionalDateTimeOffset.Create(null) == null,
				];
			}

			public static bool ExtractionIsExplicit()
			{
				string value = (string)NullableString.Create("value");
				return value == "value";
			}

			public static bool CreationConversionRemainsImplicit()
			{
				NullableString value = "value";
				return value.Value == "value";
			}

			public static bool ExtractionIsImplicitForNonNullable()
			{
				string value = NonNullableText.Create("value");
				return value == "value";
			}

			public static bool OperatorsUseTheUnderlyingValueWithoutNormalizing()
			{
				NormalizedText normalized = NormalizedText.Create("  VALUE  ");
				return normalized.Value == "value"
					&& normalized == "value"
					&& normalized != "  VALUE  ";
			}

			public static bool ExtractionOperatorIsExplicit()
			{
				var methods = typeof(NullableString).GetMethods();
				return System.Linq.Enumerable.Any(
						methods,
						method => method.Name == "op_Explicit"
							&& method.ReturnType == typeof(string)
							&& method.GetParameters()[0].ParameterType == typeof(NullableString))
					&& !System.Linq.Enumerable.Any(
						methods,
						method => method.Name == "op_Implicit"
							&& method.ReturnType == typeof(string));
			}
			public static bool NonNullableExtractionIsImplicit()
			{
				var methods = typeof(NonNullableText).GetMethods();
				return System.Linq.Enumerable.Any(
						methods,
						method => method.Name == "op_Implicit"
							&& method.ReturnType == typeof(string)
							&& method.GetParameters()[0].ParameterType == typeof(NonNullableText))
					&& !System.Linq.Enumerable.Any(
						methods,
						method => method.Name == "op_Explicit"
							&& method.ReturnType == typeof(string));
			}
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

		await Assert.That(actual.SequenceEqual(expected)).IsTrue();
	}

	[Test]
	public async Task Comparison_GivenNullableReferenceScalar_CompilesAndReturnsExpected(
		CancellationToken cancellationToken
	)
	{
		var assembly = await GenerateAssemblyAsync(cancellationToken);

		await AssertResultsAsync(assembly, "NullComparisons", [true, false, true, false, false]);
		await AssertResultsAsync(assembly, "ValueComparisons", [false, true, true, true, true, true]);
		await AssertResultsAsync(assembly, "NullishValues", [true, true, true, true, true]);
	}

	[Test]
	public async Task Comparison_GivenNullableValueTypeScalar_CompilesAndReturnsExpected(
		CancellationToken cancellationToken
	)
	{
		var assembly = await GenerateAssemblyAsync(cancellationToken);

		await AssertResultsAsync(
			assembly,
			"ValueTypeComparisons",
			[true, false, true, false, false, true, true, true, true, true, true]
		);
	}

	[Test]
	public async Task Comparison_GivenNullableScalar_UsesExplicitExtractionAndImplicitCreation(
		CancellationToken cancellationToken
	)
	{
		var assembly = await GenerateAssemblyAsync(cancellationToken);
		var harness = assembly.GetType("Testing.Harness")!;

		var extraction = (bool)harness.GetMethod("ExtractionIsExplicit")!.Invoke(null, null)!;
		var creation = (bool)harness.GetMethod("CreationConversionRemainsImplicit")!.Invoke(null, null)!;
		var operatorShape = (bool)harness.GetMethod("ExtractionOperatorIsExplicit")!.Invoke(null, null)!;

		await Assert.That(extraction).IsTrue();
		await Assert.That(creation).IsTrue();
		await Assert.That(operatorShape).IsTrue();
	}

	[Test]
	public async Task Comparison_GivenNonNullableScalar_KeepsImplicitExtraction(CancellationToken cancellationToken)
	{
		var assembly = await GenerateAssemblyAsync(cancellationToken);
		var harness = assembly.GetType("Testing.Harness")!;

		var extraction = (bool)harness.GetMethod("ExtractionIsImplicitForNonNullable")!.Invoke(null, null)!;
		var operatorShape = (bool)harness.GetMethod("NonNullableExtractionIsImplicit")!.Invoke(null, null)!;

		await Assert.That(extraction).IsTrue();
		await Assert.That(operatorShape).IsTrue();
	}

	[Test]
	public async Task Comparison_GivenNormalizingScalar_ComparesWithoutNormalizing(CancellationToken cancellationToken)
	{
		var assembly = await GenerateAssemblyAsync(cancellationToken);
		var harness = assembly.GetType("Testing.Harness")!;

		var raw = (bool)harness.GetMethod("OperatorsUseTheUnderlyingValueWithoutNormalizing")!.Invoke(null, null)!;

		await Assert.That(raw).IsTrue();
	}
}
