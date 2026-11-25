namespace Purview.ValueObjects.SourceGenerator.ValueObject.Models;

sealed record class ComplexValueObjectModel(
	GeneratedTypeModel TypeModel,
	// The type's own value members: the members its equality and comparison operate on. A derived record's
	// base members are handled by the base record's equality, not repeated here.
	EquatableArray<ComplexPropertyModel> Properties,
	// The members the generated factories, constructor, hooks and JSON converter operate on: the inherited
	// record members first (base to derived), then the type's own members.
	EquatableArray<ComplexPropertyModel> AllProperties,
	ValueObjectAttributeData Options,
	bool CtorExists,
	string HintName,
	string TypeName,
	bool IsReferenceType,
	bool IsStruct,
	bool IsRecord,
	bool IsReadOnly,
	// A non-sealed record class requires a generated Equals(TSelf) to be virtual (CS8872), so the emitter
	// needs the declared sealed modifier even though its own partial declaration is never sealed.
	bool IsSealed,
	// The record base type when the value object derives from a record, or null. Generated equality and
	// hashing delegate to it so a derived record keeps the base members in its identity.
	string? RecordBaseTypeName,
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
	bool IsEF8Referenced,
	// The JsonConverter<T> an Entity Framework Core JSON column of this value object serializes through, or null
	// when there is none to call without reflection.
	string? EFJsonConverterTypeName
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
/// <param name="CollectionKind">How the member's collection type compares, or <see cref="ComplexMemberCollectionKind.None"/>.</param>
/// <param name="CollectionElementTypeName">
/// The member's element type, used as the explicit type argument of the generated helper call for a sequence or set;
/// <see langword="null"/> for a dictionary and a non-collection member.
/// </param>
/// <param name="CollectionElementComparerExpression">
/// The expression that constructs the structural comparer for a collection whose element is itself a collection,
/// or <see langword="null"/>. The comparer is passed to the generated helper so a nested collection compares by
/// contents rather than by reference.
/// </param>
/// <param name="CollectionValueComparerExpression">
/// The expression that constructs the structural comparer for a dictionary whose value is itself a collection,
/// or <see langword="null"/> for every other member.
/// </param>
readonly record struct ComplexPropertyModel(
	string Name,
	string TypeName,
	TypeReference Type,
	bool NormalizeTrim,
	string NormalizeCasing,
	bool NormalizeExplicit,
	string? JsonConverterExpression,
	ComplexMemberCollectionKind CollectionKind,
	string? CollectionElementTypeName,
	string? CollectionElementComparerExpression,
	string? CollectionValueComparerExpression
);

/// <summary>How a complex value object's collection member compares and hashes.</summary>
enum ComplexMemberCollectionKind
{
	/// <summary>The member is not a collection, or is a <see cref="string"/>.</summary>
	None,

	/// <summary>An ordered sequence: elements compare in order.</summary>
	Sequence,

	/// <summary>A set: elements compare without regard to order.</summary>
	Set,

	/// <summary>A dictionary: key/value pairs compare without regard to order.</summary>
	Dictionary,
}
