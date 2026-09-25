using System.ComponentModel.DataAnnotations;

namespace Purview.ValueObjects.SourceGenerator.Common;

/// <summary>
/// Options for generator tests that exercise the ZodSharp integration end-to-end: the real
/// Purview.ZodSharp source generator runs alongside the value-object generator, so <c>[ZodSchema]</c>
/// and the <c>{Type}Schema</c> classes come from the ZodSharp generator rather than being mocked
/// in the test source.
/// <para>
/// The ZodSharp types are resolved out of band through <see cref="ZodSharpSourceGenerators"/> because
/// the packaged generator is a merged, self-contained analyzer that must not be referenced at compile
/// time.
/// </para>
/// </summary>
public sealed record ZodSchemaValidationGeneratorTestOptions : ValueObjectsGeneratorTestOptions
{
	public ZodSchemaValidationGeneratorTestOptions()
	{
		AdditionalGeneratorTypes = [.. AdditionalGeneratorTypes, ZodSharpSourceGenerators.Generator];
		ExcludeGeneratedSourceHintNames = [.. ExcludeGeneratedSourceHintNames, "ZodSchemaAttribute.g.cs"];
		AnalyzerTypes = [ZodSharpSourceGenerators.Analyzer];
		AdditionalAssemblyTypes = [.. AdditionalAssemblyTypes, typeof(RequiredAttribute)];
		// The loaded generator carries its own framework copy, so it validates its own CodeWriter
		// scopes and never writes to this harness's log sink.
		ValidateCodeWriterScopes = false;
	}

	public static new ZodSchemaValidationGeneratorTestOptions Default => new();

	public static ZodSchemaValidationGeneratorTestOptions Compile => new() { CompileToAssembly = true };
}
