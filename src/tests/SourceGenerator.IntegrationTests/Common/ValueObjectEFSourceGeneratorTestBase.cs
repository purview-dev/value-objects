using Purview.ValueObjects.SourceGenerator.Generators;

namespace Purview.ValueObjects.SourceGenerator.Common;

public abstract class ValueObjectEFSourceGeneratorTestBase
	: TUnitSourceGeneratorTestBase<ValueObjectSourceGenerator, ValueObjectsEFGeneratorTestOptions>;
