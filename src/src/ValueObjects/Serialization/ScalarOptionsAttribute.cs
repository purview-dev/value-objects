namespace Purview.ValueObjects.Serialization;

/// <summary>
/// Carries the options shared by <see cref="ScalarAttribute"/> and <see cref="ScalarAttribute{TValue}"/>.
/// </summary>
/// <remarks>
/// <para>
/// This type exists so the automatic (<see cref="ScalarAttribute{TValue}"/>) and manual
/// (<see cref="ScalarAttribute"/>) scalar declarations configure the generator identically without
/// duplicating the option set. It is abstract and is never applied directly.
/// </para>
/// </remarks>
/// <remarks>
/// Initializes the attribute.
/// </remarks>
/// <param name="propertyName">The name of the property that holds the underlying scalar value.</param>
public abstract class ScalarOptionsAttribute(string propertyName) : Attribute
{
	/// <summary>
	/// Gets the name of the property that holds the underlying scalar value.
	/// </summary>
	/// <value>Defaults to <c>Value</c> when not specified.</value>
	public string PropertyName { get; } = propertyName;

	/// <summary>
	/// Gets or sets whether the generator declares the underlying property as a nullable reference type.
	/// </summary>
	/// <value>Defaults to <see langword="false"/>.</value>
	/// <remarks>
	/// Applies to the automatic forms (<see cref="ScalarAttribute{TValue}"/> or
	/// <see cref="ScalarAttribute(Type, string)"/>) and to reference types only. Nullable value types are
	/// expressed directly, for example <c>[Scalar&lt;int?&gt;]</c> or <c>[Scalar(typeof(int?))]</c>. A
	/// nullable reference type cannot be used as a generic attribute argument or a <c>typeof</c> operand
	/// (CS8970/CS8639), so <c>Nullable = true</c> is the way to declare one.
	/// </remarks>
	public bool Nullable { get; init; }

	/// <summary>
	/// Gets or sets whether a JSON converter should be generated for the value object.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool GenerateJsonConverter { get; init; } = true;

	/// <summary>
	/// Gets or sets whether the value object advertises comparison against its underlying value
	/// (<see cref="IComparable{T}"/> of the primitive) and whether the comparison operators are generated.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	/// <remarks>
	/// The value object always implements <see cref="IComparable{T}"/> of itself, <see cref="IComparable"/>,
	/// and <c>CompareTo(TValue)</c>, because the value-object contract requires them. Setting this to
	/// <see langword="false"/> drops the extra <c>IComparable&lt;TValue&gt;</c> interface and the comparison
	/// operators; the operators additionally require <see cref="GenerateComparisonOperators"/>.
	/// </remarks>
	public bool GenerateComparable { get; init; } = true;

	/// <summary>
	/// Gets or sets whether comparison operators should be generated for the value object.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool GenerateComparisonOperators { get; init; } = true;

	/// <summary>
	/// Gets or sets whether enum properties should be generated from the underlying value.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool GenerateEnumProperties { get; init; } = true;

	/// <summary>
	/// Gets or sets whether an implicit conversion from the primitive value should be generated.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool GenerateImplicitFromPrimitive { get; init; } = true;

	/// <summary>
	/// Gets or sets whether an implicit conversion to the primitive value should be generated.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool GenerateImplicitToPrimitive { get; init; } = true;

	/// <summary>
	/// Gets or sets whether a static <c>Empty</c> instance should be generated.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool GenerateEmpty { get; init; } = true;

	/// <summary>
	/// Gets or sets whether an Entity Framework Core <c>ValueConverter</c> and <c>ValueComparer</c> should
	/// be generated for the value object.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	/// <remarks>
	/// Entity Framework members are emitted only when the consuming project references
	/// <c>Microsoft.EntityFrameworkCore</c>; otherwise this option is ignored. Set to
	/// <see langword="false"/> to opt out per type (see <see cref="ValueObjectDefaultsAttribute"/> for
	/// assembly-level opt-out).
	/// </remarks>
	public bool GenerateEFConverter { get; init; } = true;

	/// <summary>
	/// Gets or sets whether an Entity Framework Core <c>ValueComparer</c> should be generated for the
	/// value object.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	/// <remarks>
	/// Entity Framework members are emitted only when the consuming project references
	/// <c>Microsoft.EntityFrameworkCore</c>; otherwise this option is ignored.
	/// </remarks>
	public bool GenerateEFComparer { get; init; } = true;

	/// <summary>
	/// Gets or sets whether an Entity Framework Core value generator should be generated for this
	/// Guid-backed scalar value object.
	/// </summary>
	/// <value>Defaults to <see langword="false"/>.</value>
	/// <remarks>
	/// <para>
	/// When enabled, the generator emits a <c>ValueGenerator</c> that assigns a time-ordered
	/// (UUIDv7) identifier when an entity's key is left at <see cref="Guid.Empty"/>, and publishes
	/// it through the generated <c>ValueObjectKeyValueGeneratorConvention</c> so it applies to key
	/// properties without per-entity configuration. A value supplied by domain code is never
	/// overwritten, and an explicit <c>ValueGeneratedNever()</c> in an entity configuration wins.
	/// </para>
	/// <para>
	/// Entity Framework members are emitted only when the consuming project references
	/// <c>Microsoft.EntityFrameworkCore</c>; otherwise this option is ignored. Value generation is
	/// supported for Guid-backed scalars only.
	/// </para>
	/// </remarks>
	public bool GenerateEFValueGenerator { get; init; }

	/// <summary>
	/// Gets or sets whether the string-backed scalar's leading and trailing whitespace should be trimmed
	/// during normalization, so the scalar does not need a hand-written <c>OnNormalize</c> hook.
	/// </summary>
	/// <value>Defaults to <see langword="false"/>.</value>
	/// <remarks>
	/// The generated <c>Create</c> path applies the trim before validation. This option only has an
	/// effect on a <see cref="string"/>-backed scalar; setting it on any other scalar is reported as a
	/// diagnostic. It is ignored when the scalar implements its own <c>OnNormalize</c> hook or declares
	/// its own <c>Create</c>.
	/// </remarks>
	public bool Trim { get; init; }

	/// <summary>
	/// Gets or sets the casing canonicalization applied to a string-backed scalar during normalization.
	/// </summary>
	/// <value>Defaults to <see cref="StringCasing.None"/>.</value>
	/// <remarks>
	/// The generated <c>Create</c> path applies the casing after any configured trim and before
	/// validation. This option only has an effect on a <see cref="string"/>-backed scalar; setting it on
	/// any other scalar is reported as a diagnostic. It is ignored when the scalar implements its own
	/// <c>OnNormalize</c> hook or declares its own <c>Create</c>.
	/// </remarks>
	public StringCasing Casing { get; init; } = StringCasing.None;

	/// <summary>
	/// Gets or sets the deserialization mode used by the generated JSON converter.
	/// </summary>
	/// <value>Defaults to <see cref="ValueObjectDeserializationMode.Hydrate"/>.</value>
	public ValueObjectDeserializationMode DeserializationMode { get; init; } = ValueObjectDeserializationMode.Hydrate;

	/// <summary>
	/// Gets or sets how a source-generated ZodSharp schema validator (from the <c>[ZodSchema]</c>
	/// attribute on this type) participates in the generated <c>Create</c> path.
	/// </summary>
	/// <value>Defaults to <see cref="ZodSchemaMode.InAdditionToHooks"/>.</value>
	public ZodSchemaMode ZodSchemaMode { get; init; } = ZodSchemaMode.InAdditionToHooks;
}
