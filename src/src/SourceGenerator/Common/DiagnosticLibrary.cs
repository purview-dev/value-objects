namespace Purview.ValueObjects.SourceGenerator.Common;

static class DiagnosticLibrary
{
	const string ValueObjectCategory = "ValueObjects";

	/// <summary> VO1001: Value object must be partial </summary>
	public static readonly DiagnosticDescriptor ValueObjectMustBePartial = new(
		id: "VO1001",
		title: "Value object must be partial",
		messageFormat: "Value object '{0}' must be declared partial to use [{1}]",
		category: ValueObjectCategory,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	/// <summary> VO1002: Nested value objects are not supported </summary>
	public static readonly DiagnosticDescriptor NestedValueObjectsAreNotSupported = new(
		id: "VO1002",
		title: "Nested value objects are not supported",
		messageFormat: "Value object '{0}' cannot be nested when using [{1}]",
		category: ValueObjectCategory,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	/// <summary> VO1003: Generic value objects are not supported </summary>
	public static readonly DiagnosticDescriptor GenericValueObjectsAreNotSupported = new(
		id: "VO1003",
		title: "Generic value objects are not supported",
		messageFormat: "Value object '{0}' cannot be generic when using [{1}]",
		category: ValueObjectCategory,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	/// <summary> VO1004: Scalar property is missing </summary>
	public static readonly DiagnosticDescriptor ScalarPropertyMissing = new(
		id: "VO1004",
		title: "Scalar property is missing",
		messageFormat: "Scalar value object '{0}' must declare readable property '{1}'",
		category: ValueObjectCategory,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	/// <summary> VO1005: Scalar constructor is missing </summary>
	public static readonly DiagnosticDescriptor ScalarConstructorMissing = new(
		id: "VO1005",
		title: "Scalar constructor is missing",
		messageFormat: "Scalar value object '{0}' must declare a constructor '{0}({1})' to support generated Create/Hydrate",
		category: ValueObjectCategory,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	/// <summary> VO1006: Scalar value objects should be record structs </summary>
	public static readonly DiagnosticDescriptor ScalarShouldBeRecordStruct = new(
		id: "VO1006",
		title: "Scalar value objects should be record structs",
		messageFormat: "Scalar value object '{0}' should be declared as a readonly record struct so the compiler can synthesize equality members and avoid CA1815",
		category: ValueObjectCategory,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true
	);

	/// <summary> VO1007: Strict mode requires Create </summary>
	public static readonly DiagnosticDescriptor StrictDeserializationRequiresCreate = new(
		id: "VO1007",
		title: "Strict mode requires Create",
		messageFormat: "Value object '{0}' uses strict deserialization mode but does not declare a compatible static Create(...) overload",
		category: ValueObjectCategory,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true
	);

	/// <summary> VO1008: Conflicting value object attributes </summary>
	public static readonly DiagnosticDescriptor ConflictingValueObjectAttributes = new(
		id: "VO1008",
		title: "Conflicting value object attributes",
		messageFormat: "Type '{0}' cannot be annotated with both [Scalar] and [ValueObject]",
		category: ValueObjectCategory,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);
}
