namespace Purview.ValueObjects.SourceGenerator.Common;

public static partial class TypeLibrary
{
	public const string SerializationNamespace = "Purview.ValueObjects.Serialization";

	public const string ValueObjectGeneratorName = "Purview.ValueObjects.ValueObjectSourceGenerator";

	public const string ValueObjectAttributeFullTypeName = SerializationNamespace + ".ValueObjectAttribute";

	public const string ValueObjectDefaultsAttributeFullTypeName =
		SerializationNamespace + ".ValueObjectDefaultsAttribute";

	public const string ScalarAttributeFullTypeName = SerializationNamespace + ".ScalarAttribute";

	public const string ValueObjectDeserializationModeFullTypeName =
		SerializationNamespace + ".ValueObjectDeserializationMode";
}
