using ZodSharp.SourceGenerators;

namespace Purview.ValueObjects.SourceGenerator.Common;

/// <summary>
/// Options for generator tests that exercise the ZodSharp integration end-to-end: the real
/// Purview.ZodSharp source generator runs alongside the value-object generator, so <c>[ZodSchema]</c>
/// and the <c>{Type}Schema</c> classes come from the ZodSharp generator rather than being mocked
/// in the test source.
/// </summary>
public sealed record ZodSchemaValidationGeneratorTestOptions : ValueObjectsGeneratorTestOptions
{
	public ZodSchemaValidationGeneratorTestOptions()
	{
		AdditionalGeneratorTypes = [.. AdditionalGeneratorTypes, typeof(ZodSchemaGenerator)];
		ExcludeGeneratedSourceHintNames = [.. ExcludeGeneratedSourceHintNames, "ZodSchemaAttribute.g.cs"];
	}

	public static new ZodSchemaValidationGeneratorTestOptions Default => new();
}
