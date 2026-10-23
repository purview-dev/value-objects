using Purview.ValueObjects.SourceGenerator.Analyzers;

namespace Purview.ValueObjects.SourceGenerator.Refactorings;

/// <summary>
/// The three repairs for <c>VO1028</c>: nullability is declared by both the automatic attribute and a
/// declared <c>Value</c> property, so either source (or the automatic form itself) has to go.
/// </summary>
public sealed class ScalarNullabilityCodeFixTests
{
	const string DuplicateNullability = """
		namespace Testing
		{
			[Purview.ValueObjects.Serialization.Scalar<string>(Nullable = true)]
			public readonly partial record struct Handle
			{
				public string? Value { get; init; }
			}
		}
		""";

	const string TypeofDuplicateNullability = """
		namespace Testing
		{
			[Purview.ValueObjects.Serialization.Scalar(typeof(string), Nullable = true)]
			public readonly partial record struct TypeofHandle
			{
				public string? Value { get; init; }
			}
		}
		""";

	[Test]
	public async Task GivenDuplicateNullability_RemovesTheDeclaredProperty(CancellationToken cancellationToken)
	{
		var result = await CodeFixTestHarness.ApplyAsync<
			ValueObjectDiagnosticAnalyzer,
			ScalarNullabilityCodeFixProvider
		>(DuplicateNullability, cancellationToken);

		await Assert.That(result.FixedCode).DoesNotContain("public string? Value");
		await Assert.That(result.FixedCode).Contains("Scalar<string>(Nullable = true)");
		await Assert.That(result.FixedDiagnosticIds).Contains(DiagnosticLibrary.ScalarNullabilityDeclaredTwice.Id);
	}

	[Test]
	public async Task GivenDuplicateNullability_ConvertsToTheManualScalarForm(CancellationToken cancellationToken)
	{
		var result = await CodeFixTestHarness.ApplyAsync<
			ValueObjectDiagnosticAnalyzer,
			ScalarNullabilityCodeFixProvider
		>(DuplicateNullability, actionIndex: 1, cancellationToken);

		await Assert.That(result.FixedCode).Contains("Scalar]");
		await Assert.That(result.FixedCode).DoesNotContain("Scalar<string>");
		await Assert.That(result.FixedCode).Contains("public string? Value");
	}

	[Test]
	public async Task GivenDuplicateNullability_RemovesOnlyTheNullableOption(CancellationToken cancellationToken)
	{
		var result = await CodeFixTestHarness.ApplyAsync<
			ValueObjectDiagnosticAnalyzer,
			ScalarNullabilityCodeFixProvider
		>(DuplicateNullability, actionIndex: 2, cancellationToken);

		await Assert.That(result.FixedCode).Contains("Scalar<string>");
		await Assert.That(result.FixedCode).DoesNotContain("Nullable = true");
		await Assert.That(result.FixedCode).Contains("public string? Value");
	}

	[Test]
	public async Task GivenTypeofDuplicateNullability_ConvertsToTheManualScalarForm(CancellationToken cancellationToken)
	{
		var result = await CodeFixTestHarness.ApplyAsync<
			ValueObjectDiagnosticAnalyzer,
			ScalarNullabilityCodeFixProvider
		>(TypeofDuplicateNullability, actionIndex: 1, cancellationToken);

		await Assert.That(result.FixedCode).Contains("Scalar]");
		await Assert.That(result.FixedCode).DoesNotContain("typeof");
		await Assert.That(result.FixedCode).Contains("public string? Value");
	}

	[Test]
	public async Task GivenPackagedAnalyzerShape_FixableDiagnosticIds_ExposeVO1028()
	{
		using var components = PackagedAnalyzerComponents.Load();

		await Assert
			.That(components.ScalarNullabilityCodeFix.FixableDiagnosticIds)
			.Contains(DiagnosticLibrary.ScalarNullabilityDeclaredTwice.Id);
	}

	[Test]
	public async Task GivenPackagedAnalyzerShape_RemovesTheDeclaredProperty(CancellationToken cancellationToken)
	{
		using var components = PackagedAnalyzerComponents.Load();

		var result = await CodeFixTestHarness.ApplyAsync(
			DuplicateNullability,
			components.Generator,
			components.Analyzer,
			components.ScalarNullabilityCodeFix,
			cancellationToken
		);

		await Assert.That(result.FixedCode).DoesNotContain("public string? Value");
		await Assert.That(result.FixedDiagnosticIds).Contains(DiagnosticLibrary.ScalarNullabilityDeclaredTwice.Id);
	}
}
