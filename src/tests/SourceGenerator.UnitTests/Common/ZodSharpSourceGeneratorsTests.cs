using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Purview.ValueObjects.SourceGenerator.Common;

/// <summary>
/// Guards the out-of-band ZodSharp generator acquisition: the packaged analyzer must be loaded by path
/// rather than referenced, so framework types never become ambiguous with the harness's framework
/// assembly.
/// </summary>
public sealed class ZodSharpSourceGeneratorsTests
{
	[Test]
	public async Task SourceGenerators_GivenUnitTestAssembly_DoesNotReferenceMergedAnalyzer()
	{
		// Act
		var references = typeof(ZodSharpSourceGeneratorsTests).Assembly.GetReferencedAssemblies();

		// Assert
		await Assert
			.That(references.Any(static reference => reference.Name == "Purview.ZodSharp.SourceGenerators"))
			.IsFalse();
	}

	[Test]
	public async Task Generator_GivenPackagedAnalyzer_ResolvesComponentTypes()
	{
		// Act
		var generator = ZodSharpSourceGenerators.Generator;
		var analyzer = ZodSharpSourceGenerators.Analyzer;

		// Assert
		await Assert.That(typeof(IIncrementalGenerator).IsAssignableFrom(generator)).IsTrue();
		await Assert.That(generator.FullName).IsEqualTo("ZodSharp.SourceGenerators.ZodSchemaGenerator");
		await Assert.That(typeof(DiagnosticAnalyzer).IsAssignableFrom(analyzer)).IsTrue();
		await Assert.That(analyzer.FullName).IsEqualTo("ZodSharp.SourceGenerators.ZodSchemaAnalyzer");
	}
}
