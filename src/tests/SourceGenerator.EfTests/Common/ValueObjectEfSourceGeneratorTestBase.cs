using Purview.ValueObjects.SourceGenerator.Generators;

namespace Purview.ValueObjects.SourceGenerator.Common;

public abstract class ValueObjectEfSourceGeneratorTestBase
	: TUnitSourceGeneratorTestBase<ValueObjectSourceGenerator, ValueObjectsEfGeneratorTestOptions>;
