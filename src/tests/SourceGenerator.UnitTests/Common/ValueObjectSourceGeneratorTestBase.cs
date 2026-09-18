using Purview.ValueObjects.SourceGenerator.Generators;

namespace Purview.ValueObjects.SourceGenerator.Common;

public abstract class ValueObjectSourceGeneratorTestBase
	: TUnitSourceGeneratorTestBase<ValueObjectSourceGenerator, ValueObjectsGeneratorTestOptions>
{
	protected const int HintNameHashHexLength = ValueObjectsGeneratorTestOptions.HintNameHashHexLength;

	protected const string GeneratedSourceFileSuffix = ValueObjectsGeneratorTestOptions.GeneratedSourceFileSuffix;

	protected static int ExpectedFileCount => ValueObjectsGeneratorTestOptions.ValueObjectExpectedFileCount;

	protected static int ExpectedFileCountPlusGen =>
		ValueObjectsGeneratorTestOptions.ValueObjectExpectedFileCountPlusGen;
}
