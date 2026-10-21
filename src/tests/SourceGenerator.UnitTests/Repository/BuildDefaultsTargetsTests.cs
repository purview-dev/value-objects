namespace Purview.ValueObjects.SourceGenerator.Repository;

/// <summary>
/// Guards the contract between the runtime <c>[ValueObjectDefaults]</c> options, the MSBuild properties
/// the packaged targets expose, and the file name the generator uses to recognise the build-generated
/// attribute. Adding an option without wiring it through the targets would silently do nothing.
/// </summary>
public sealed class BuildDefaultsTargetsTests
{
	[Test]
	public async Task Props_DeclareOneBuildPropertyPerValueObjectDefaultsOption()
	{
		// The props is what gives the properties IDE completion and a value before the project body runs.
		var props = ReadSdkFile("Purview.ValueObjects.props");
		var missing = typeof(ValueObjectDefaultsAttribute)
			.GetProperties(
				System.Reflection.BindingFlags.Public
					| System.Reflection.BindingFlags.Instance
					| System.Reflection.BindingFlags.DeclaredOnly
			)
			.Select(static property => property.Name)
			.Where(name => !props.Contains($"<ValueObjectsDefault{name}", StringComparison.Ordinal))
			.ToArray();

		await Assert
			.That(missing)
			.IsEmpty()
			.Because(
				$"every [ValueObjectDefaults] option needs a ValueObjectsDefault<Name> property; missing: {string.Join(", ", missing)}"
			);
	}

	[Test]
	public async Task Targets_ExposeOneBuildPropertyPerValueObjectDefaultsOption()
	{
		var targets = ReadSdkFile("Purview.ValueObjects.targets");
		var missing = typeof(ValueObjectDefaultsAttribute)
			.GetProperties(
				System.Reflection.BindingFlags.Public
					| System.Reflection.BindingFlags.Instance
					| System.Reflection.BindingFlags.DeclaredOnly
			)
			.Select(static property => property.Name)
			.Where(name => !targets.Contains($"ValueObjectsDefault{name}", StringComparison.Ordinal))
			.ToArray();

		await Assert
			.That(missing)
			.IsEmpty()
			.Because(
				$"every [ValueObjectDefaults] option needs a ValueObjectsDefault<Name> property and a validation rule; missing: {string.Join(", ", missing)}"
			);
	}

	[Test]
	public async Task Targets_EmitTheBuildDefaultsFileNameTheGeneratorRecognises()
	{
		var targets = ReadSdkFile("Purview.ValueObjects.targets");

		await Assert
			.That(targets)
			.Contains(TypeLibrary.BuildDefaultsFileName)
			.Because("the generator identifies the build-generated attribute by this file name");
	}

	static string ReadSdkFile(string fileName) =>
		File.ReadAllText(
			Path.Combine(FindRepositoryRoot(), "src", "src", "ValueObjects", "Sdk", "buildTransitive", fileName)
		);

	static string FindRepositoryRoot()
	{
		DirectoryInfo? directory = new(AppContext.BaseDirectory);
		while (directory is not null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
				return directory.FullName;

			directory = directory.Parent;
		}

		throw new InvalidOperationException(
			$"Could not find the repository root (an AGENTS.md marker) above '{AppContext.BaseDirectory}'."
		);
	}
}
