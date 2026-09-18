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
}
