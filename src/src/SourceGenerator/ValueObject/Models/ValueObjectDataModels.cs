namespace Purview.ValueObjects.SourceGenerator.ValueObject.Models;

[Generate(TypeLibrary.ScalarAttributeFullTypeName)]
readonly partial record struct ScalarAttributeData(
	[Argument("propertyName", DefaultValue = "Value")] string PropertyName,
	[Property(DefaultValue = true)] bool GenerateJsonConverter,
	[Property(DefaultValue = true)] bool GenerateComparable,
	[Property(DefaultValue = true)] bool GenerateComparisonOperators,
	[Property(DefaultValue = true)] bool GenerateEnumProperties,
	[Property(DefaultValue = true)] bool GenerateImplicitFromPrimitive,
	[Property(DefaultValue = true)] bool GenerateImplicitToPrimitive,
	[Property(DefaultValue = true)] bool GenerateEmpty,
	[Property(DefaultValue = true)] bool GenerateEFConverter,
	[Property(DefaultValue = true)] bool GenerateEFComparer,
	[Property(DefaultValue = TypeLibrary.ValueObjectDeserializationModeFullTypeName + ".Hydrate", IsEnum = true)]
		string DeserializationMode,
	[Property(DefaultValue = TypeLibrary.ZodSchemaModeFullTypeName + ".InAdditionToHooks", IsEnum = true)]
		string ZodSchemaMode
);

[Generate(TypeLibrary.ValueObjectAttributeFullTypeName)]
readonly partial record struct ValueObjectAttributeData(
	[Property(DefaultValue = true)] bool GenerateJsonConverter,
	[Property(DefaultValue = true)] bool GenerateComparable,
	[Property(DefaultValue = true)] bool GenerateComparisonOperators,
	[Property(DefaultValue = true)] bool GenerateEmpty,
	[Property(DefaultValue = true)] bool GenerateConstructor,
	[Property(DefaultValue = TypeLibrary.EntityFrameworkMappingFullTypeName + ".ComplexType", IsEnum = true)]
		string EFMapping,
	[Property(DefaultValue = true)] bool GenerateEFComparer,
	[Property(DefaultValue = TypeLibrary.ValueObjectDeserializationModeFullTypeName + ".Hydrate", IsEnum = true)]
		string DeserializationMode,
	[Property(DefaultValue = TypeLibrary.ZodSchemaModeFullTypeName + ".InAdditionToHooks", IsEnum = true)]
		string ZodSchemaMode
);

[Generate(TypeLibrary.ValueObjectDefaultsAttributeFullTypeName)]
readonly partial record struct ValueObjectDefaultsAttributeData(
	[Property(DefaultValue = true)] bool GenerateJsonConverter,
	[Property(DefaultValue = true)] bool GenerateComparable,
	[Property(DefaultValue = true)] bool GenerateComparisonOperators,
	[Property(DefaultValue = true)] bool GenerateEnumProperties,
	[Property(DefaultValue = true)] bool GenerateImplicitFromPrimitive,
	[Property(DefaultValue = true)] bool GenerateImplicitToPrimitive,
	[Property(DefaultValue = true)] bool GenerateEmpty,
	[Property(DefaultValue = true)] bool GenerateConstructor,
	[Property(DefaultValue = TypeLibrary.EntityFrameworkMappingFullTypeName + ".ComplexType", IsEnum = true)]
		string EFMapping,
	[Property(DefaultValue = true)] bool GenerateEFConverter,
	[Property(DefaultValue = true)] bool GenerateEFComparer,
	[Property(DefaultValue = TypeLibrary.ValueObjectDeserializationModeFullTypeName + ".Hydrate", IsEnum = true)]
		string DeserializationMode,
	[Property(DefaultValue = TypeLibrary.ZodSchemaModeFullTypeName + ".InAdditionToHooks", IsEnum = true)]
		string ZodSchemaMode
);
