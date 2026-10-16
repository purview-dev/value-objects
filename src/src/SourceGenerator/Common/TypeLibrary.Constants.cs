namespace Purview.ValueObjects.SourceGenerator.Common;

public static partial class TypeLibrary
{
	// The *FullTypeName constants below are consumed as attribute arguments by the [Generate]
	// attribute-data-model declarations (see ValueObjectDataModels.cs). Attribute arguments must fold
	// to constant strings within the same compilation pass, so these cannot reference the generated
	// {Member}FullName constants (those are only available in a later pass) and are kept as literals.
	// The TypeLibrary also exposes generated *FullName constants (via [TypeRef(generateFullNameConst: true)])
	// which the generator's runtime logic uses instead of hard-coded type names.
	public const string ValueObjectsNamespace = "Purview.ValueObjects";

	public const string SerializationNamespace = "Purview.ValueObjects.Serialization";

	public const string ValueObjectAttributeFullTypeName = SerializationNamespace + ".ValueObjectAttribute";

	public const string ValueObjectDefaultsAttributeFullTypeName =
		SerializationNamespace + ".ValueObjectDefaultsAttribute";

	public const string ScalarAttributeFullTypeName = SerializationNamespace + ".ScalarAttribute";

	/// <summary>The metadata name of the automatic (generic) scalar attribute, <c>[Scalar&lt;T&gt;]</c>.</summary>
	public const string ScalarAttributeGenericMetadataName = ScalarAttributeFullTypeName + "`1";

	public const string ValueObjectDeserializationModeFullTypeName =
		SerializationNamespace + ".ValueObjectDeserializationMode";

	public const string EntityFrameworkMappingFullTypeName = SerializationNamespace + ".EntityFrameworkMapping";

	public const string ZodSchemaModeFullTypeName = SerializationNamespace + ".ZodSchemaMode";

	public const string StringCasingFullTypeName = SerializationNamespace + ".StringCasing";

	public const string StringNormalizeAttributeFullTypeName = SerializationNamespace + ".StringNormalizeAttribute";

	// ZodSharp's runtime and generator-emitted types. The ZodSharp attribute and schema types are produced
	// by the ZodSharp source generator, so these names are matched and emitted as qualified text rather than
	// resolved as symbols (the value object generator must keep working when ZodSharp is not referenced).
	public const string ZodSharpNamespace = "ZodSharp";

	public const string ZodSharpCoreNamespace = ZodSharpNamespace + ".Core";

	public const string ZodSharpSchemasNamespace = ZodSharpNamespace + ".Schemas";

	public const string ZodSchemaAttributeName = "ZodSchemaAttribute";

	public const string ZodRefineContextName = "RefineCtx";

	public const string ZodValidationErrorName = "ValidationError";

	/// <summary>The default ZodSharp synchronous refinement method name (<c>[ZodSchema]</c> default).</summary>
	public const string ZodDefaultRefinementMethodName = "Validate";

	/// <summary>The optional partial hook a value object implements to contribute Zod-compatible issues.</summary>
	public const string ZodRefinementHookName = "OnZodValidate";

	/// <summary>The ZodSharp exception thrown when the generated <c>Create</c> path rejects a value.</summary>
	public const string ZodExceptionTypeName = ZodSharpCoreNamespace + ".ZodException";

	/// <summary>The empty base path passed to a generated <c>RefineCtx&lt;T&gt;</c>.</summary>
	public const string ZodEmptyPathExpression = "global::System.Collections.Immutable.ImmutableArray<string>.Empty";

	/// <summary>The ZodSharp rule contract the scalar rule adapter forwards to.</summary>
	public const string ZodValidationRuleName = "IValidationRule";

	/// <summary>
	/// The metadata name used to detect a Purview.ZodSharp runtime reference: the adapter can only compile
	/// when the rule interface is available.
	/// </summary>
	public const string ZodValidationRuleMetadataName = ZodSharpCoreNamespace + "." + ZodValidationRuleName + "`1";

	/// <summary>The scalar rule adapter emitted into consumer compilations that reference ZodSharp.</summary>
	public const string ScalarRuleAdapterName = "ScalarRuleAdapter";

	/// <summary>The adapter's metadata name, used to skip emission when a consumer declares its own.</summary>
	public const string ScalarRuleAdapterMetadataName = ValueObjectsNamespace + "." + ScalarRuleAdapterName + "`3";

	/// <summary>The hint name the adapter source is registered under.</summary>
	public const string ScalarRuleAdapterHintName = ScalarRuleAdapterName + ".g.cs";

	public const string EFValueObjectEFNamespace = "Microsoft.EntityFrameworkCore";

	public const string EFValueGeneratorFactoryFullTypeName =
		EFValueObjectEFNamespace + ".ValueGeneration.ValueGeneratorFactory";

	public const string EFValueGeneratorFullTypeName = EFValueObjectEFNamespace + ".ValueGeneration.ValueGenerator";

	public const string EFIPropertyFullTypeName = EFValueObjectEFNamespace + ".Metadata.IProperty";

	public const string EFITypeBaseFullTypeName = EFValueObjectEFNamespace + ".Metadata.ITypeBase";

	public const string EFEntityEntryFullTypeName = EFValueObjectEFNamespace + ".ChangeTracking.EntityEntry";

	public const string EFValueGeneratedFullTypeName = EFValueObjectEFNamespace + ".Metadata.ValueGenerated";

	public const string EFModelConfigurationBuilderFullTypeName =
		EFValueObjectEFNamespace + ".ModelConfigurationBuilder";

	public const string EFConventionModelBuilderFullTypeName =
		EFValueObjectEFNamespace + ".Metadata.Builders.IConventionModelBuilder";

	public const string EFConventionContextFullTypeName =
		EFValueObjectEFNamespace + ".Metadata.Conventions.IConventionContext";

	public const string EFModelFinalizingConventionFullTypeName =
		EFValueObjectEFNamespace + ".Metadata.Conventions.IModelFinalizingConvention";

	/// <summary>The Entity Framework Core entity type, the <c>ValueGeneratorFactory.Create</c> parameter before EF Core 8.</summary>
	public const string EFIEntityTypeFullTypeName = EFValueObjectEFNamespace + ".Metadata.IEntityType";

	/// <summary>The nested generator types emitted on a value object's <c>EF</c> class.</summary>
	public const string EFValueGeneratorFactoryMemberName = "ValueGeneratorFactory";

	/// <summary>The nested generator type emitted on a value object's <c>EF</c> class.</summary>
	public const string EFValueGeneratorMemberName = "ValueGenerator";

	/// <summary>The nested factory type for SQL Server-ordered identifiers.</summary>
	public const string EFValueGeneratorSqlServerFactoryMemberName = "SqlServerValueGeneratorFactory";

	/// <summary>The nested generator type for SQL Server-ordered identifiers.</summary>
	public const string EFValueGeneratorSqlServerMemberName = "SqlServerValueGenerator";

	/// <summary>The emitted enum selecting the ordering generated key values ascend in.</summary>
	public const string EFKeyOrderingEnumName = "ValueObjectKeyOrdering";

	/// <summary>The fully qualified name of the emitted key ordering enum.</summary>
	public const string EFKeyOrderingEnumFullTypeName =
		"global::" + EFValueObjectEFNamespace + "." + EFKeyOrderingEnumName;

	/// <summary>The UUIDv7 strategy call: ascending in plain big-endian byte order.</summary>
	public const string EFSequentialGuidUuidV7Expression = ".NewGuid()";

	/// <summary>The SQL Server strategy call: ascending in <c>uniqueidentifier</c> ordering.</summary>
	public const string EFSequentialGuidSqlServerExpression = ".NewSqlServerGuid()";

	/// <summary>The assembly-level convention that attaches generated key value generators.</summary>
	public const string EFKeyValueGeneratorConventionClassName = "ValueObjectKeyValueGeneratorConvention";

	/// <summary>The assembly-level time-ordered identifier helper.</summary>
	public const string EFSequentialGuidClassName = "ValueObjectSequentialGuid";

	/// <summary>The complete name of the assembly-level time-ordered identifier helper.</summary>
	public const string EFSequentialGuidFullTypeName =
		"global::" + EFValueObjectEFNamespace + "." + EFSequentialGuidClassName;

	public const string EFValueObjectExtensionsClassName = "ValueObjectEFExtensions";
}
