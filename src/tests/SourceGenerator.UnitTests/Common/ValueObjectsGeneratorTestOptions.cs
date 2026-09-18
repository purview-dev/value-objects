namespace Purview.ValueObjects.SourceGenerator.Common;

public record ValueObjectsGeneratorTestOptions : SourceGeneratorTestOptions
{
	public const string PreCompilationMarkerHintName = "PreCompilationMarker.g.cs";

	public static readonly string[] ValueObjectGeneratedAttributes =
	[
		"EmbeddedAttribute.g.cs",
		"ValueObjectDefaultsAttribute.g.cs",
	];

	public static readonly int ValueObjectExpectedFileCount = ValueObjectGeneratedAttributes.Length + 1;

	public static readonly int ValueObjectExpectedFileCountPlusGen = ValueObjectExpectedFileCount + 1;

	public const int HintNameHashHexLength = 16;

	public const string GeneratedSourceFileSuffix = ".g.cs";

	public ValueObjectsGeneratorTestOptions()
	{
		DisableSourceGeneratorPropertyName = PropertyLibrary.DisableSourceGenerator;
		ValidateCodeWriterScopes = true;
		AdditionalNamespaces =
		[
			typeof(ValueObjects.Serialization.ScalarJsonConverterFactory).Namespace!,
			typeof(ValueObjects.IValueObject).Namespace!,
		];
		AdditionalAssemblyTypes = [typeof(ValueObjects.IValueObject)];
		AdditionalReferences = [.. TestMetadataReferences.GetAdditionalReferences()];
		ExcludeGeneratedSourceHintNames = [.. ValueObjectGeneratedAttributes, PreCompilationMarkerHintName];
		AnalyzerTypes = [typeof(Analyzers.ValueObjectDiagnosticAnalyzer)];
	}

	public static new ValueObjectsGeneratorTestOptions Default => new();

	public static readonly ValueObjectsGeneratorTestOptions NoValidation = new() { ThrowOnGenerationException = false };
}
