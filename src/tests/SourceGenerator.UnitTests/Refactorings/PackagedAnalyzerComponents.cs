using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Purview.ValueObjects.SourceGenerator.Refactorings;

/// <summary>
/// Loads the value-object Roslyn components in the shape a package consumer receives: the merged,
/// self-contained analyzer artifact (the assembly Visual Studio loads from
/// <c>analyzers/dotnet/cs</c>) together with the code-fix component.
/// <para>
/// The merge pass strips every <c>InternalsVisibleTo</c> declaration from the artifact, so a
/// component that reaches into another component's internals compiles against the unmerged build
/// output and then fails with <see cref="FieldAccessException"/> at runtime. Loading the packaged
/// shape here reproduces that environment and keeps the failure detectable without Visual Studio.
/// </para>
/// <para>
/// A merged artifact must never be referenced at compile time, so it is loaded out of band into an
/// isolated <see cref="AssemblyLoadContext"/>; host assemblies (Roslyn, immutable collections, and
/// composition) are shared so the reflected components keep one type identity with the test host.
/// </para>
/// </summary>
public sealed class PackagedAnalyzerComponents : IDisposable
{
	const string GeneratorAssemblyName = "Purview.ValueObjects.SourceGenerator";

	const string CodeFixAssemblyName = "Purview.ValueObjects.SourceGenerator.Refactorings";

	const string GeneratorTypeName = "Purview.ValueObjects.SourceGenerator.Generators.ValueObjectSourceGenerator";

	const string AnalyzerTypeName = "Purview.ValueObjects.SourceGenerator.Analyzers.ValueObjectDiagnosticAnalyzer";

	const string CodeFixTypeName =
		"Purview.ValueObjects.SourceGenerator.Refactorings.AddPartialModifierCodeFixProvider";

	const string MergedArtifactDirectoryName = "purview-merged";

	readonly PackagedAnalyzerLoadContext _loadContext;

	PackagedAnalyzerComponents(
		PackagedAnalyzerLoadContext loadContext,
		IIncrementalGenerator generator,
		DiagnosticAnalyzer analyzer,
		CodeFixProvider codeFix
	)
	{
		_loadContext = loadContext;
		Generator = generator;
		Analyzer = analyzer;
		CodeFix = codeFix;
	}

	/// <summary>Gets the value-object source generator loaded from the packaged analyzer artifact.</summary>
	public IIncrementalGenerator Generator { get; }

	/// <summary>Gets the value-object diagnostic analyzer loaded from the packaged analyzer artifact.</summary>
	public DiagnosticAnalyzer Analyzer { get; }

	/// <summary>Gets the code-fix provider loaded from the packaged code-fix component.</summary>
	public CodeFixProvider CodeFix { get; }

	/// <summary>Loads the packaged analyzer and code-fix components for the current build configuration.</summary>
	public static PackagedAnalyzerComponents Load()
	{
		var packageAnalyzerPath = FindPackagedAnalyzerPath();
		var codeFixPath = typeof(AddPartialModifierCodeFixProvider).Assembly.Location;

		PackagedAnalyzerLoadContext loadContext = new(packageAnalyzerPath, codeFixPath, AppContext.BaseDirectory);
		try
		{
			var analyzerAssembly = loadContext.LoadFromAssemblyPath(packageAnalyzerPath);
			var codeFixAssembly = loadContext.LoadFromAssemblyPath(codeFixPath);

			return new PackagedAnalyzerComponents(
				loadContext,
				(IIncrementalGenerator)CreateComponent(analyzerAssembly, GeneratorTypeName),
				(DiagnosticAnalyzer)CreateComponent(analyzerAssembly, AnalyzerTypeName),
				(CodeFixProvider)CreateComponent(codeFixAssembly, CodeFixTypeName)
			);
		}
		catch
		{
			loadContext.Unload();
			throw;
		}
	}

	/// <inheritdoc/>
	public void Dispose() => _loadContext.Unload();

	static object CreateComponent(Assembly assembly, string typeName) =>
		Activator.CreateInstance(assembly.GetType(typeName, throwOnError: true)!)!;

	/// <summary>
	/// Resolves the merged analyzer artifact produced by the Purview.SourceGeneratorFramework merge
	/// pass into the generator's intermediate output.
	/// </summary>
	static string FindPackagedAnalyzerPath()
	{
		var mergedRoot = Path.Combine(FindRepositoryRoot(), "src", "src", "SourceGenerator", "obj");
		if (!Directory.Exists(mergedRoot))
			throw new DirectoryNotFoundException(
				$"The generator intermediate output '{mergedRoot}' was not found. Build the solution before running the packaged-shape tests."
			);

		var configuration = FindBuildConfiguration();
		var candidates = Directory
			.EnumerateFiles(mergedRoot, GeneratorAssemblyName + ".dll", SearchOption.AllDirectories)
			.Where(path =>
				path.Contains(
					$"{Path.DirectorySeparatorChar}{MergedArtifactDirectoryName}{Path.DirectorySeparatorChar}",
					StringComparison.OrdinalIgnoreCase
				)
			)
			// A failed merge can leave a partially written staging directory behind; never load that.
			.Where(path => !path.Contains(".staging-", StringComparison.OrdinalIgnoreCase))
			.Where(path =>
				configuration is null
				|| path.Contains(
					$"{Path.DirectorySeparatorChar}{configuration}{Path.DirectorySeparatorChar}",
					StringComparison.OrdinalIgnoreCase
				)
			)
			.OrderByDescending(File.GetLastWriteTimeUtc)
			.ToArray();

		if (candidates.Length == 0)
		{
			throw new FileNotFoundException(
				"The merged, self-contained analyzer artifact was not found under "
					+ $"'{mergedRoot}'. Building the solution runs the Purview.SourceGeneratorFramework merge pass that produces it.",
				mergedRoot
			);
		}

		// The merge pass produces a single artifact, but the intermediate output can contain multiple
		return candidates[0];
	}

	static string FindRepositoryRoot()
	{
		DirectoryInfo? directory = new(AppContext.BaseDirectory);
		while (directory is not null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "src", "ValueObjects.slnx")))
				return directory.FullName;

			directory = directory.Parent;
		}

		throw new DirectoryNotFoundException(
			$"The repository root could not be located above '{AppContext.BaseDirectory}'."
		);
	}

	static string? FindBuildConfiguration()
	{
		DirectoryInfo? directory = new(AppContext.BaseDirectory);
		while (directory is not null)
		{
			if (directory.Name is "Debug" or "Release")
				return directory.Name;

			directory = directory.Parent;
		}

		return null;
	}

	/// <summary>
	/// Isolates the packaged components from the in-repo build output. The generator/analyzer assembly
	/// and the code-fix assembly resolve from the packaged shape; every other assembly is shared with
	/// the test host when it is already loaded, and otherwise probed beside the test binaries.
	/// </summary>
	sealed class PackagedAnalyzerLoadContext : AssemblyLoadContext
	{
		readonly Dictionary<string, string> _componentPaths;

		readonly string _probeDirectory;

		public PackagedAnalyzerLoadContext(string packageAnalyzerPath, string codeFixPath, string probeDirectory)
			: base(nameof(PackagedAnalyzerLoadContext), isCollectible: true)
		{
			_componentPaths = new Dictionary<string, string>(StringComparer.Ordinal)
			{
				[GeneratorAssemblyName] = packageAnalyzerPath,
				[CodeFixAssemblyName] = codeFixPath,
			};
			_probeDirectory = probeDirectory;
		}

		protected override Assembly? Load(AssemblyName assemblyName)
		{
			var name = assemblyName.Name;
			if (name is null)
				return null;

			if (_componentPaths.TryGetValue(name, out var componentPath))
				return LoadFromAssemblyPath(componentPath);

			var host = Default.Assemblies.FirstOrDefault(assembly =>
				AssemblyName.ReferenceMatchesDefinition(assembly.GetName(), assemblyName)
			);
			if (host is not null)
				return host;

			var probePath = Path.Combine(_probeDirectory, name + ".dll");
			return File.Exists(probePath) ? LoadFromAssemblyPath(probePath) : null;
		}
	}
}
