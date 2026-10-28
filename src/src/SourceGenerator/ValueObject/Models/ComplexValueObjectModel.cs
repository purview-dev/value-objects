namespace Purview.ValueObjects.SourceGenerator.ValueObject.Models;

sealed record class ComplexValueObjectModel(
	GeneratedTypeModel TypeModel,
	EquatableArray<ComplexPropertyModel> Properties,
	ValueObjectAttributeData Options,
	bool CtorExists,
	string HintName,
	string TypeName,
	bool IsReferenceType,
	bool IsStruct,
	bool IsRecord,
	bool IsReadOnly,
	TypeDeclarationAccessibility? Accessibility,
	bool ImplementsSelfEquatable,
	bool MirrorIEquatableSelf,
	bool HydrateExists,
	bool CreateExists,
	bool CompareToSelfExists,
	bool CompareToObjectExists,
	bool EqualsSelfExists,
	bool EqualsObjectExists,
	bool GetHashCodeExists,
	bool EqualityOperatorExists,
	bool InequalityOperatorExists,
	bool HasJsonConverterAttribute,
	bool DeclareOnNormalize,
	bool DeclareOnValidate,
	bool ValidateHookIsReadOnly,
	bool EmptyExists,
	EquatableArray<string> EmptyArguments,
	bool ParameterlessCtorExists,
	string? EFConstructorArguments,
	string HydrateFactoryName,
	EquatableArray<string> ExistingRelationalOperators,
	bool HasZodSchemaValidation,
	string? ZodSchemaClassName,
	bool IsEFReferenced,
	bool IsEF8Referenced
);

/// <summary>A member of a complex value object.</summary>
/// <param name="Name">The member's name.</param>
/// <param name="TypeName">The member's fully qualified type, with its nullable annotation.</param>
/// <param name="Type">The member's type.</param>
/// <param name="NormalizeTrim">True when the generated <c>Create</c> trims the member.</param>
/// <param name="NormalizeCasing">The casing the generated <c>Create</c> applies to the member.</param>
/// <param name="NormalizeExplicit">True when the normalization was set on the member rather than defaulted.</param>
/// <param name="JsonConverterExpression">
/// The expression that constructs the JSON converter a value-object-typed member serializes through, or
/// <see langword="null"/>. The generated converter passes it to <c>ValueObjectJson</c>, so a member that is itself
/// a value object still round-trips when a source-generated <c>JsonSerializerContext</c> never listed its type.
/// </param>
readonly record struct ComplexPropertyModel(
	string Name,
	string TypeName,
	TypeReference Type,
	bool NormalizeTrim,
	string NormalizeCasing,
	bool NormalizeExplicit,
	string? JsonConverterExpression
);
