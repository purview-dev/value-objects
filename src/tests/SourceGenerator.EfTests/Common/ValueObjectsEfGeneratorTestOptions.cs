namespace Purview.ValueObjects.SourceGenerator.Common;

/// <summary>
/// Source generator test options for the Entity Framework Core integration. This project references
/// <c>Microsoft.EntityFrameworkCore</c>, so the framework's trusted references include the Entity
/// Framework assemblies and every in-memory test compilation has the Entity Framework integration
/// active.
/// </summary>
public record ValueObjectsEfGeneratorTestOptions : SourceGeneratorTestOptions
{
	public const string PreCompilationMarkerHintName = "PreCompilationMarker.g.cs";

	public static readonly string[] ValueObjectGeneratedAttributes =
	[
		"EmbeddedAttribute.g.cs",
		"ValueObjectDefaultsAttribute.g.cs",
	];

	public ValueObjectsEfGeneratorTestOptions()
	{
		DisableSourceGeneratorPropertyName = PropertyLibrary.DisableSourceGenerator;
		ValidateCodeWriterScopes = true;
		AdditionalNamespaces = [typeof(ScalarJsonConverterFactory).Namespace!, typeof(IValueObject).Namespace!];
		AdditionalAssemblyTypes = [typeof(IValueObject)];
		ExcludeGeneratedSourceHintNames = [.. ValueObjectGeneratedAttributes, PreCompilationMarkerHintName];
		AnalyzerTypes = [typeof(Analyzers.ValueObjectDiagnosticAnalyzer)];
	}

	public static new ValueObjectsEfGeneratorTestOptions Default => new();

	public static ValueObjectsEfGeneratorTestOptions NoValidation => new() { ThrowOnGenerationException = false };
}
