namespace Purview.ValueObjects.SourceGenerator.Common;

public record ValueObjectsGeneratorTestOptions : SourceGeneratorTestOptions
{
	public static readonly string[] ValueObjectGeneratedTypes =
	[
		"EmbeddedAttribute.g.cs",
		$"{TypeLibrary.EFValueObjectExtensionsClassName}.g.cs",
	];

	public static readonly int ValueObjectExpectedFileCount = ValueObjectGeneratedTypes.Length;

	public static readonly int ValueObjectExpectedFileCountPlusGen = ValueObjectExpectedFileCount;

	public const int HintNameHashHexLength = 16;

	public const string GeneratedSourceFileSuffix = ".g.cs";

	public ValueObjectsGeneratorTestOptions()
	{
		DisableSourceGeneratorPropertyName = PropertyLibrary.DisableSourceGenerator;
		ValidateCodeWriterScopes = true;
		AdditionalNamespaces =
		[
			typeof(ScalarJsonConverterFactory).Namespace!,
			typeof(IValueObject).Namespace!,
			typeof(ZodSharp.Core.ValidationResult<>).Namespace!,
		];
		AdditionalAssemblyTypes = [typeof(IValueObject), typeof(ZodSharp.Core.ValidationResult<>)];
		AdditionalReferences = [.. TestMetadataReferences.GetAdditionalReferences()];
		ExcludeGeneratedSourceHintNames = [.. ValueObjectGeneratedTypes];
		AnalyzerTypes = [typeof(Analyzers.ValueObjectDiagnosticAnalyzer)];
	}

	public static new ValueObjectsGeneratorTestOptions Default => new();

	public static readonly ValueObjectsGeneratorTestOptions NoValidation = new() { ThrowOnGenerationException = false };
}
