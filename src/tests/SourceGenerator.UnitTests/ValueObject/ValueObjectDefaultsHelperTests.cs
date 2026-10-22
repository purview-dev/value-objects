using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Purview.ValueObjects.SourceGenerator.ValueObject;

/// <summary>
/// White-box tests for the assembly-default layering: the build-generated attribute the packaged targets
/// emit from <c>ValueObjectsDefault*</c> MSBuild properties, and the author-declared attribute that can
/// override it.
/// </summary>
public sealed class ValueObjectDefaultsHelperTests
{
	const string BuildDefaultsPath = "obj/Debug/net10.0/" + TypeLibrary.BuildDefaultsFileName;

	[Test]
	public async Task ResolveAssemblyDefaults_GivenBuildGeneratedAttribute_AppliesIt()
	{
		var source = """
			[assembly: Purview.ValueObjects.Serialization.ValueObjectDefaults(Trim = true, Casing = Purview.ValueObjects.Serialization.StringCasing.LowerInvariant)]
			""";

		var defaults = Resolve(source, BuildDefaultsPath);

		await Assert.That(defaults.Exists).IsTrue();
		await Assert.That(defaults.Trim).IsTrue();
		await Assert
			.That(defaults.Casing)
			.IsEqualTo("global::" + TypeLibrary.StringCasingFullTypeName + ".LowerInvariant");
	}

	[Test]
	public async Task ResolveAssemblyDefaults_GivenNoAttributes_ReturnsEmpty()
	{
		var defaults = Resolve("", "Models.cs");

		await Assert.That(defaults.Exists).IsFalse();
	}

	[Test]
	public async Task ResolveAssemblyDefaults_GivenAuthorAttribute_OverridesExplicitBuildOption()
	{
		// The build says trim; the author explicitly turns it off. Only the author-declared option wins,
		// while the build's casing still applies because the author attribute does not name it.
		var buildSource = """
			[assembly: Purview.ValueObjects.Serialization.ValueObjectDefaults(Trim = true, Casing = Purview.ValueObjects.Serialization.StringCasing.UpperInvariant)]
			""";
		var authorSource = """
			[assembly: Purview.ValueObjects.Serialization.ValueObjectDefaults(Trim = false)]
			""";

		var defaults = Resolve([buildSource, authorSource], [BuildDefaultsPath, "Models.cs"]);

		await Assert.That(defaults.Trim).IsFalse();
		await Assert
			.That(defaults.Casing)
			.IsEqualTo("global::" + TypeLibrary.StringCasingFullTypeName + ".UpperInvariant");
	}

	[Test]
	public async Task ResolveAssemblyDefaults_GivenBuildAttribute_LeavesUnsetOptionsAtBuiltInDefaults()
	{
		var source = """
			[assembly: Purview.ValueObjects.Serialization.ValueObjectDefaults(Trim = true)]
			""";

		var defaults = Resolve(source, BuildDefaultsPath);

		await Assert.That(defaults.Trim).IsTrue();
		await Assert.That(defaults.GenerateJsonConverter).IsTrue();
		await Assert.That(defaults.GenerateEFValueGenerator).IsFalse();
		await Assert
			.That(defaults.EFMapping)
			.IsEqualTo("global::" + TypeLibrary.EntityFrameworkMappingFullTypeName + ".ComplexType");
		await Assert
			.That(defaults.DeserializationMode)
			.IsEqualTo("global::" + TypeLibrary.ValueObjectDeserializationModeFullTypeName + ".Hydrate");
	}

	static ValueObjectDefaultsAttributeData Resolve(string source, string path) => Resolve([source], [path]);

	static ValueObjectDefaultsAttributeData Resolve(string[] sources, string[] paths) =>
		ValueObjectDefaultsHelper.ResolveAssemblyDefaults(CreateCompilation(sources, paths).Assembly.GetAttributes());

	static CSharpCompilation CreateCompilation(string[] sources, string[] paths)
	{
		var trees = sources.Select((source, index) => CSharpSyntaxTree.ParseText(source, path: paths[index])).ToArray();

		return CSharpCompilation.Create(
			"Purview.ValueObjects.DefaultsTests",
			trees,
			GetReferences(),
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
		);
	}

	static ImmutableArray<MetadataReference> GetReferences()
	{
		var platform = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty;
		return
		[
			.. platform
				.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
				.Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path)),
			MetadataReference.CreateFromFile(typeof(ValueObjectDefaultsAttribute).Assembly.Location),
		];
	}
}
