namespace Purview.ValueObjects.SourceGenerator.ValueObject;

/// <summary>
/// Applies assembly-level <c>[ValueObjectDefaults]</c> to per-type <c>[Scalar]</c>/<c>[ValueObject]</c>
/// options. Precedence is: an option explicitly set on the type wins, then the assembly default, then
/// the built-in default carried by the type data model.
/// </summary>
static class ValueObjectDefaultsHelper
{
	public static ScalarAttributeData Apply(
		ScalarAttributeData typeOptions,
		ValueObjectDefaultsAttributeData assemblyDefaults,
		ImmutableArray<AttributeData> attributes
	)
	{
		if (!assemblyDefaults.Exists)
			return typeOptions;

		// Merge assembly defaults into the type options, but only for properties that are not explicitly set on the type.
		return typeOptions with
		{
			GenerateJsonConverter = MergeBool(
				typeOptions.GenerateJsonConverter,
				assemblyDefaults.GenerateJsonConverter,
				"GenerateJsonConverter"
			),
			GenerateComparable = MergeBool(
				typeOptions.GenerateComparable,
				assemblyDefaults.GenerateComparable,
				"GenerateComparable"
			),
			GenerateComparisonOperators = MergeBool(
				typeOptions.GenerateComparisonOperators,
				assemblyDefaults.GenerateComparisonOperators,
				"GenerateComparisonOperators"
			),
			GenerateEnumProperties = MergeBool(
				typeOptions.GenerateEnumProperties,
				assemblyDefaults.GenerateEnumProperties,
				"GenerateEnumProperties"
			),
			GenerateImplicitFromPrimitive = MergeBool(
				typeOptions.GenerateImplicitFromPrimitive,
				assemblyDefaults.GenerateImplicitFromPrimitive,
				"GenerateImplicitFromPrimitive"
			),
			GenerateImplicitToPrimitive = MergeBool(
				typeOptions.GenerateImplicitToPrimitive,
				assemblyDefaults.GenerateImplicitToPrimitive,
				"GenerateImplicitToPrimitive"
			),
			GenerateEmpty = MergeBool(typeOptions.GenerateEmpty, assemblyDefaults.GenerateEmpty, "GenerateEmpty"),
			GenerateEFConverter = MergeBool(
				typeOptions.GenerateEFConverter,
				assemblyDefaults.GenerateEFConverter,
				"GenerateEFConverter"
			),
			GenerateEFComparer = MergeBool(
				typeOptions.GenerateEFComparer,
				assemblyDefaults.GenerateEFComparer,
				"GenerateEFComparer"
			),
			DeserializationMode = MergeString(
				typeOptions.DeserializationMode,
				assemblyDefaults.DeserializationMode,
				"DeserializationMode"
			),
			ZodSchemaMode = MergeString(typeOptions.ZodSchemaMode, assemblyDefaults.ZodSchemaMode, "ZodSchemaMode"),
		};

		bool MergeBool(bool typeValue, bool assemblyValue, string propertyName) =>
			IsPropertyExplicitlySet(
				attributes,
				TypeLibrary.Purview.ValueObjects.Serialization.ScalarAttribute,
				propertyName
			)
				? typeValue
				: assemblyValue;

		string MergeString(string typeValue, string assemblyValue, string propertyName) =>
			IsPropertyExplicitlySet(
				attributes,
				TypeLibrary.Purview.ValueObjects.Serialization.ScalarAttribute,
				propertyName
			)
				? typeValue
				: assemblyValue;
	}

	public static ValueObjectAttributeData Apply(
		ValueObjectAttributeData typeOptions,
		ValueObjectDefaultsAttributeData assemblyDefaults,
		ImmutableArray<AttributeData> attributes
	)
	{
		if (!assemblyDefaults.Exists)
			return typeOptions;

		// Merge assembly defaults into the type options, but only for properties that are not explicitly set on the type.
		return typeOptions with
		{
			GenerateJsonConverter = MergeBool(
				typeOptions.GenerateJsonConverter,
				assemblyDefaults.GenerateJsonConverter,
				"GenerateJsonConverter"
			),
			GenerateComparable = MergeBool(
				typeOptions.GenerateComparable,
				assemblyDefaults.GenerateComparable,
				"GenerateComparable"
			),
			GenerateComparisonOperators = MergeBool(
				typeOptions.GenerateComparisonOperators,
				assemblyDefaults.GenerateComparisonOperators,
				"GenerateComparisonOperators"
			),
			GenerateEmpty = MergeBool(typeOptions.GenerateEmpty, assemblyDefaults.GenerateEmpty, "GenerateEmpty"),
			GenerateConstructor = MergeBool(
				typeOptions.GenerateConstructor,
				assemblyDefaults.GenerateConstructor,
				"GenerateConstructor"
			),
			EFMapping = MergeString(typeOptions.EFMapping, assemblyDefaults.EFMapping, "EFMapping"),
			GenerateEFComparer = MergeBool(
				typeOptions.GenerateEFComparer,
				assemblyDefaults.GenerateEFComparer,
				"GenerateEFComparer"
			),
			DeserializationMode = MergeString(
				typeOptions.DeserializationMode,
				assemblyDefaults.DeserializationMode,
				"DeserializationMode"
			),
			ZodSchemaMode = MergeString(typeOptions.ZodSchemaMode, assemblyDefaults.ZodSchemaMode, "ZodSchemaMode"),
		};

		bool MergeBool(bool typeValue, bool assemblyValue, string propertyName) =>
			IsPropertyExplicitlySet(
				attributes,
				TypeLibrary.Purview.ValueObjects.Serialization.ValueObjectAttribute,
				propertyName
			)
				? typeValue
				: assemblyValue;

		string MergeString(string typeValue, string assemblyValue, string propertyName) =>
			IsPropertyExplicitlySet(
				attributes,
				TypeLibrary.Purview.ValueObjects.Serialization.ValueObjectAttribute,
				propertyName
			)
				? typeValue
				: assemblyValue;
	}

	public static bool IsPropertyExplicitlySet(
		ImmutableArray<AttributeData> attributes,
		TypeIdentity attributeType,
		string propertyName
	)
	{
		var attribute = attributes.FirstOrDefault(a => attributeType.Equals(a.AttributeClass));
		return attribute?.NamedArguments.Any(kvp => kvp.Key == propertyName) ?? false;
	}

	public static bool IsPropertyExplicitlySet(
		ImmutableArray<AttributeData> attributes,
		string attributeName,
		string propertyName
	)
	{
		var attribute = attributes.FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == attributeName);
		return attribute?.NamedArguments.Any(kvp => kvp.Key == propertyName) ?? false;
	}
}
