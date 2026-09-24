using Purview.ValueObjects.SourceGenerator.Generators;

namespace Purview.ValueObjects.SourceGenerator.Common;

public abstract class ValueObjectSourceGeneratorTestBase
	: ValueObjectSourceGeneratorTestBase<ValueObjectsGeneratorTestOptions>;

public abstract class ValueObjectSourceGeneratorTestBase<TTestOptions>
	: TUnitSourceGeneratorTestBase<ValueObjectSourceGenerator, TTestOptions>
	where TTestOptions : ValueObjectsGeneratorTestOptions, new()
{
	protected const int HintNameHashHexLength = ValueObjectsGeneratorTestOptions.HintNameHashHexLength;

	protected const string GeneratedSourceFileSuffix = ValueObjectsGeneratorTestOptions.GeneratedSourceFileSuffix;

	protected static int ExpectedFileCount => ValueObjectsGeneratorTestOptions.ValueObjectExpectedFileCount;

	protected static int ExpectedFileCountPlusGen =>
		ValueObjectsGeneratorTestOptions.ValueObjectExpectedFileCountPlusGen;
}
