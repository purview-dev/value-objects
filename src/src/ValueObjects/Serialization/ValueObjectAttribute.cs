namespace Purview.ValueObjects.Serialization;

/// <summary>
/// Marks a struct or class as a complex (non-scalar) value object and controls how the source generator
/// produces conversion, comparison, and serialization members for it.
/// </summary>
/// <remarks>
/// A complex value object wraps multiple members. When the generator processes a type decorated with this
/// attribute, it can emit a JSON converter, <see cref="IComparable"/> support, comparison operators, an
/// <c>Empty</c> instance, and a constructor. See also <see cref="ScalarAttribute"/> for single-value wrappers
/// and <see cref="ValueObjectDefaultsAttribute"/> for assembly-level defaults.
/// </remarks>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
public sealed class ValueObjectAttribute : Attribute
{
	/// <summary>
	/// Gets or sets whether a JSON converter should be generated for the value object.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool GenerateJsonConverter { get; init; } = true;

	/// <summary>
	/// Gets or sets whether the value object should implement <see cref="IComparable{T}"/>.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool GenerateComparable { get; init; } = true;

	/// <summary>
	/// Gets or sets whether comparison operators should be generated for the value object.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool GenerateComparisonOperators { get; init; } = true;

	/// <summary>
	/// Gets or sets whether a static <c>Empty</c> instance should be generated.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool GenerateEmpty { get; init; } = true;

	/// <summary>
	/// Gets or sets whether a constructor should be generated for the value object.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool GenerateConstructor { get; init; } = true;

	/// <summary>
	/// Gets or sets how the value object is mapped when the Entity Framework Core integration is active.
	/// </summary>
	/// <value>Defaults to <see cref="EntityFrameworkMapping.ComplexType"/>.</value>
	/// <remarks>
	/// Entity Framework members are emitted only when the consuming project references
	/// <c>Microsoft.EntityFrameworkCore</c>; otherwise this option is ignored. Set to
	/// <see cref="EntityFrameworkMapping.None"/> to opt out per type (see <see cref="ValueObjectDefaultsAttribute"/>
	/// for assembly-level opt-out).
	/// </remarks>
	public EntityFrameworkMapping EFMapping { get; init; } = EntityFrameworkMapping.ComplexType;

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
