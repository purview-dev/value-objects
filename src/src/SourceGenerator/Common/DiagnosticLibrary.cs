namespace Purview.ValueObjects.SourceGenerator.Common;

/// <summary>
/// The diagnostics this component produces.
/// <para>
/// The type is public because the identities are shared with the code-fix component
/// (<c>Purview.ValueObjects.SourceGenerator.Refactorings</c>). Consumers receive the merged,
/// self-contained analyzer that the Purview.SourceGeneratorFramework merge pass produces, and that
/// pass strips every <c>InternalsVisibleTo</c> declaration from the artifact. Reaching these members
/// through internals therefore compiles against the unmerged build output and then fails with a
/// <see cref="FieldAccessException"/> in the IDE, as soon as Roslyn reads a fixable
/// diagnostic id from the shipped analyzer. Public members are the only cross-component contract a
/// merged artifact preserves.
/// </para>
/// </summary>
public static class DiagnosticLibrary
{
	const string ValueObjectCategory = "ValueObjects";

	/// <summary>VO1001: Value object must be partial </summary>
	public static readonly DiagnosticDescriptor ValueObjectMustBePartial = new(
		id: "VO1001",
		title: "Value object must be partial",
		messageFormat: "Value object '{0}' must be declared partial to use [{1}]",
		category: ValueObjectCategory,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	/// <summary>VO1002: Nested value objects are not supported </summary>
	public static readonly DiagnosticDescriptor NestedValueObjectsAreNotSupported = new(
		id: "VO1002",
		title: "Nested value objects are not supported",
		messageFormat: "Value object '{0}' cannot be nested when using [{1}]",
		category: ValueObjectCategory,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	/// <summary>VO1003: Generic value objects are not supported </summary>
	public static readonly DiagnosticDescriptor GenericValueObjectsAreNotSupported = new(
		id: "VO1003",
		title: "Generic value objects are not supported",
		messageFormat: "Value object '{0}' cannot be generic when using [{1}]",
		category: ValueObjectCategory,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	/// <summary>VO1004: Scalar property is missing </summary>
	public static readonly DiagnosticDescriptor ScalarPropertyMissing = new(
		id: "VO1004",
		title: "Scalar property is missing",
		messageFormat: "Scalar value object '{0}' must declare readable property '{1}'",
		category: ValueObjectCategory,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	/// <summary>VO1005: Scalar constructor is missing </summary>
	public static readonly DiagnosticDescriptor ScalarConstructorMissing = new(
		id: "VO1005",
		title: "Scalar constructor is missing",
		messageFormat: "Scalar value object '{0}' must declare a constructor '{0}({1})' to support generated Create/Hydrate",
		category: ValueObjectCategory,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	/// <summary>VO1006: Scalar value objects should be record structs </summary>
	public static readonly DiagnosticDescriptor ScalarShouldBeRecordStruct = new(
		id: "VO1006",
		title: "Scalar value objects should be record structs",
		messageFormat: "Scalar value object '{0}' should be declared as a readonly record struct so the compiler can synthesize equality members and avoid CA1815",
		category: ValueObjectCategory,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true
	);

	/// <summary>VO1007: Strict mode requires Create </summary>
	public static readonly DiagnosticDescriptor StrictDeserializationRequiresCreate = new(
		id: "VO1007",
		title: "Strict mode requires Create",
		messageFormat: "Value object '{0}' uses strict deserialization mode but does not declare a compatible static Create(...) overload",
		category: ValueObjectCategory,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true
	);

	/// <summary>VO1008: Conflicting value object attributes </summary>
	public static readonly DiagnosticDescriptor ConflictingValueObjectAttributes = new(
		id: "VO1008",
		title: "Conflicting value object attributes",
		messageFormat: "Type '{0}' cannot be annotated with both [Scalar] and [ValueObject]",
		category: ValueObjectCategory,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	/// <summary>VO1009: Entity Framework requested but not referenced </summary>
	public static readonly DiagnosticDescriptor EFMappingRequiresEntityFramework = new(
		id: "VO1009",
		title: "Entity Framework mapping requires Microsoft.EntityFrameworkCore",
		messageFormat: "Value object '{0}' requests Entity Framework mapping but the project does not reference Microsoft.EntityFrameworkCore; no Entity Framework members will be generated",
		category: ValueObjectCategory,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true
	);

	/// <summary>VO1010: Entity Framework auto-conversion skipped </summary>
	public static readonly DiagnosticDescriptor EFAutoConversionSkipped = new(
		id: "VO1010",
		title: "Entity Framework auto-conversion is not available for this value object",
		messageFormat: "Scalar value object '{0}' wraps underlying type '{1}', which Entity Framework Core cannot map natively; automatic conversion is skipped, so the property must be mapped manually",
		category: ValueObjectCategory,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true
	);

	/// <summary>VO1013: OnValidate is skipped by ZodSchemaMode.InsteadOfHooks </summary>
	public static readonly DiagnosticDescriptor OnValidateSkippedByInsteadOfHooks = new(
		id: "VO1013",
		title: "OnValidate is not invoked by ZodSchemaMode.InsteadOfHooks",
		messageFormat: "Value object '{0}' implements 'OnValidate' but ZodSchemaMode.InsteadOfHooks means the generated Create does not invoke it",
		category: ValueObjectCategory,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true
	);

	/// <summary>VO1016: Value object members must be immutable </summary>
	public static readonly DiagnosticDescriptor ValueObjectMemberIsMutable = new(
		id: "VO1016",
		title: "Value object member is mutable",
		messageFormat: "Value object '{0}' member '{1}' has a setter; value objects must be immutable, so declare the member get-only or init-only",
		category: ValueObjectCategory,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true
	);

	/// <summary>VO1017: A JSON column mapping needs the JSON converter </summary>
	public static readonly DiagnosticDescriptor EFJsonMappingRequiresJsonConverter = new(
		id: "VO1017",
		title: "Entity Framework JSON mapping requires the JSON converter",
		messageFormat: "Value object '{0}' maps to a JSON column but its JSON converter generation is disabled; the column content is produced by reflection serialization, which can differ from the value object's own JSON contract",
		category: ValueObjectCategory,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true
	);

	/// <summary>VO1018: A complex mapping cannot convert a member </summary>
	public static readonly DiagnosticDescriptor EFComplexMappingUnsupportedMember = new(
		id: "VO1018",
		title: "Entity Framework complex mapping cannot convert this member",
		messageFormat: "Value object '{0}' maps as an Entity Framework Core complex type but member '{1}' of type '{2}' cannot be converted{3}",
		category: ValueObjectCategory,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true
	);

	/// <summary>VO1019: Complex type mapping requires Entity Framework Core 8 </summary>
	public static readonly DiagnosticDescriptor EFComplexTypeRequiresEntityFramework8 = new(
		id: "VO1019",
		title: "Entity Framework complex type mapping requires Entity Framework Core 8 or later",
		messageFormat: "Value object '{0}' maps as an Entity Framework Core complex type, which requires Entity Framework Core 8 or later; no Entity Framework mapping will be generated for it",
		category: ValueObjectCategory,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true
	);

	/// <summary>VO1021: Entity Framework value generation is unavailable </summary>
	public static readonly DiagnosticDescriptor EFValueGenerationUnavailable = new(
		id: "VO1021",
		title: "Entity Framework value generation is not available for this value object",
		messageFormat: "Value object '{0}' requests Entity Framework key value generation but {1}; no value generator will be emitted",
		category: ValueObjectCategory,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true
	);

	/// <summary>VO1015: ZodSchema SchemaName is not a valid identifier </summary>
	public static readonly DiagnosticDescriptor ZodSchemaNameInvalid = new(
		id: "VO1015",
		title: "ZodSharp schema name is not a valid identifier",
		messageFormat: "Value object '{0}' sets [ZodSchema(SchemaName = \"{1}\")], which is not a valid C# identifier; the ZodSharp-generated schema class and the value object generator must agree on the same name",
		category: ValueObjectCategory,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);
}
