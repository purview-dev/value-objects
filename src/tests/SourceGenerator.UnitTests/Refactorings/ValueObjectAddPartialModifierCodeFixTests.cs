using Microsoft.CodeAnalysis.CodeFixes;
using Purview.ValueObjects.SourceGenerator.Analyzers;

namespace Purview.ValueObjects.SourceGenerator.Refactorings;

public sealed class ValueObjectAddPartialModifierCodeFixTests
{
	const string NonPartialScalar = """
		namespace Testing
		{
			[Purview.ValueObjects.Serialization.Scalar]
			public readonly record struct EmailAddress
			{
				public string Value { get; }
			}
		}
		""";

	const string NonPartialComplexValueObject = """
		namespace Testing
		{
			[Purview.ValueObjects.Serialization.ValueObject]
			public readonly record struct Money
			{
				public decimal Amount { get; }
				public string Currency { get; }
			}
		}
		""";

	[Test]
	public async Task GivenNonPartialScalar_AddsPartialModifier(CancellationToken cancellationToken)
	{
		var result = await CodeFixTestHarness.ApplyAsync<
			ValueObjectDiagnosticAnalyzer,
			AddPartialModifierCodeFixProvider
		>(NonPartialScalar, cancellationToken);

		await Assert.That(result.FixedCode).Contains("public readonly partial record struct EmailAddress");
	}

	[Test]
	public async Task GivenNonPartialComplexValueObject_AddsPartialModifier(CancellationToken cancellationToken)
	{
		var result = await CodeFixTestHarness.ApplyAsync<
			ValueObjectDiagnosticAnalyzer,
			AddPartialModifierCodeFixProvider
		>(NonPartialComplexValueObject, cancellationToken);

		await Assert.That(result.FixedCode).Contains("public readonly partial record struct Money");
	}

	/// <summary>
	/// The analyzer artifact consumers receive is the merged, self-contained assembly the framework
	/// merge pass produces, and that pass strips every <c>InternalsVisibleTo</c> declaration. A code
	/// fix that shares the diagnostic identity through generator internals therefore compiles against
	/// the unmerged build output and throws <see cref="FieldAccessException"/> as soon as Visual Studio
	/// reads <see cref="CodeFixProvider.FixableDiagnosticIds"/> from the packaged artifact.
	/// </summary>
	[Test]
	public async Task GivenPackagedAnalyzerShape_FixableDiagnosticIds_ExposeTheValueObjectMustBePartialId()
	{
		using var components = PackagedAnalyzerComponents.Load();

		await Assert
			.That(components.CodeFix.FixableDiagnosticIds)
			.Contains(DiagnosticLibrary.ValueObjectMustBePartial.Id);
	}

	[Test]
	public async Task GivenPackagedAnalyzerShape_AddsPartialModifier(CancellationToken cancellationToken)
	{
		using var components = PackagedAnalyzerComponents.Load();

		var result = await CodeFixTestHarness.ApplyAsync(
			NonPartialScalar,
			components.Generator,
			components.Analyzer,
			components.CodeFix,
			cancellationToken
		);

		await Assert.That(result.FixedCode).Contains("public readonly partial record struct EmailAddress");
		await Assert.That(result.FixedDiagnosticIds).Contains(DiagnosticLibrary.ValueObjectMustBePartial.Id);
	}
}
