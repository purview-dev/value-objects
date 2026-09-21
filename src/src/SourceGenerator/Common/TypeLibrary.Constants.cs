namespace Purview.ValueObjects.SourceGenerator.Common;

public static partial class TypeLibrary
{
	public const string ValueObjectGeneratorName = "Purview.ValueObjects.ValueObjectSourceGenerator";

	// The *FullTypeName constants below are consumed as attribute arguments by the [Generate]
	// attribute-data-model declarations (see ValueObjectDataModels.cs). Attribute arguments must fold
	// to constant strings within the same compilation pass, so these cannot reference the generated
	// {Member}FullName constants (those are only available in a later pass) and are kept as literals.
	// The TypeLibrary also exposes generated *FullName constants (via [TypeRef(generateFullNameConst: true)])
	// which the generator's runtime logic uses instead of hard-coded type names.
	public const string SerializationNamespace = "Purview.ValueObjects.Serialization";

	public const string ValueObjectAttributeFullTypeName = SerializationNamespace + ".ValueObjectAttribute";

	public const string ValueObjectDefaultsAttributeFullTypeName =
		SerializationNamespace + ".ValueObjectDefaultsAttribute";

	public const string ScalarAttributeFullTypeName = SerializationNamespace + ".ScalarAttribute";

	public const string ValueObjectDeserializationModeFullTypeName =
		SerializationNamespace + ".ValueObjectDeserializationMode";

	public const string EfMappingFullTypeName = SerializationNamespace + ".EfMapping";

	public const string ZodSchemaModeFullTypeName = SerializationNamespace + ".ZodSchemaMode";

	public const string EfValueConverterFullTypeName = Microsoft
		.EntityFrameworkCore
		.Storage
		.ValueConversion
		.ValueConverterFullName;

	public const string EfValueObjectEfNamespace = "Purview.ValueObjects.Ef";

	public const string EfValueObjectExtensionsClassName = "ValueObjectEfExtensions";
}
