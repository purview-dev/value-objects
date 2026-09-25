namespace Purview.ValueObjects.SourceGenerator.Common;

/// <summary>
/// Resolves the Purview.ZodSharp generator and analyzer types out of band.
/// <para>
/// The ZodSharp generator ships as a merged, self-contained analyzer inside the Purview.ZodSharp
/// package, so it is copied beside the test binaries (never referenced). A compile-time reference
/// would be ambiguous: a merged component carries framework types
/// (<c>Purview.SourceGeneratorFramework.*</c>) that collide with the real framework assembly the test
/// harness loads, producing CS0433 for every framework type used in this project.
/// </para>
/// <para>
/// <see cref="SourceGeneratorTestOptions.AdditionalGeneratorTypes"/>/<c>AnalyzerTypes</c> accept
/// <see cref="Type"/> values and the runner instantiates them with <c>Activator.CreateInstance</c>, so
/// a reflected type behaves exactly like <c>typeof(...)</c>.
/// </para>
/// </summary>
public static class ZodSharpSourceGenerators
{
	const string AssemblyFileName = "Purview.ZodSharp.SourceGenerators.dll";
	const string GeneratorTypeName = "ZodSharp.SourceGenerators.ZodSchemaGenerator";
	const string AnalyzerTypeName = "ZodSharp.SourceGenerators.ZodSchemaAnalyzer";

	static readonly Lazy<Type> s_generator = new(() => Resolve(GeneratorTypeName));

	static readonly Lazy<Type> s_analyzer = new(() => Resolve(AnalyzerTypeName));

	/// <summary>Gets the ZodSharp schema source generator type.</summary>
	public static Type Generator => s_generator.Value;

	/// <summary>Gets the ZodSharp schema diagnostic analyzer type.</summary>
	public static Type Analyzer => s_analyzer.Value;

	static Type Resolve(string typeName)
	{
		var assemblyPath = Path.Combine(AppContext.BaseDirectory, AssemblyFileName);
		if (!File.Exists(assemblyPath))
		{
			throw new FileNotFoundException(
				$"The Purview.ZodSharp generator assembly was not found at '{assemblyPath}'. It is copied from the Purview.ZodSharp package by SourceGenerator.UnitTests.csproj; run a restore first.",
				assemblyPath
			);
		}

		// The generator assembly is a merged, self-contained analyzer that carries its own framework copy.
		// It must not be referenced at compile time, so we load it out of band and reflect the generator and analyzer types.
		return System.Reflection.Assembly.LoadFrom(assemblyPath).GetType(typeName, throwOnError: true)!;
	}
}
