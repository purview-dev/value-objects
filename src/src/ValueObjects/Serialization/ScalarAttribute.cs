namespace Purview.ValueObjects.Serialization;

/// <summary>
/// Marks a struct or class as a scalar value object whose underlying value property is declared by the
/// author, and controls how the source generator produces conversion, comparison, and serialization
/// members for it.
/// </summary>
/// <remarks>
/// <para>
/// A scalar value object wraps a single underlying primitive value. When the generator processes a type
/// decorated with this attribute, it can emit a JSON converter, <see cref="IComparable"/> support,
/// comparison operators, implicit conversions to and from the primitive, and an <c>Empty</c> instance.
/// </para>
/// <para>
/// The <see cref="ScalarOptionsAttribute.PropertyName"/> identifies the member holding the underlying value.
/// Scalar value objects are serialized using only that member's value, which is what makes them
/// query-friendly for primitive inner values.
/// </para>
/// <para>
/// This is the <b>manual</b> form: the author declares the named property and the generator never emits
/// it. To have the generator declare the property instead, pass a value type to the constructor (for
/// example <c>[Scalar(typeof(string))]</c>) or use <see cref="ScalarAttribute{TValue}"/>. The forms are
/// mutually exclusive for a given type.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
public sealed class ScalarAttribute : ScalarOptionsAttribute
{
	/// <summary>
	/// Initializes the manual form, where the author declares the named property.
	/// </summary>
	/// <param name="propertyName">The name of the property that holds the underlying scalar value.</param>
	public ScalarAttribute(string propertyName = "Value")
		: base(propertyName) { }

	/// <summary>
	/// Initializes the automatic form, where the generator declares the named property of type
	/// <paramref name="valueType"/>.
	/// </summary>
	/// <param name="valueType">
	/// The underlying value type, for example <c>typeof(string)</c> or <c>typeof(int?)</c>. A nullable
	/// reference type cannot be a <c>typeof</c> operand (CS8639); use <see cref="ScalarOptionsAttribute.Nullable"/>
	/// for those, for example <c>[Scalar(typeof(string), Nullable = true)]</c>.
	/// </param>
	/// <param name="propertyName">The name of the property the generator declares.</param>
	public ScalarAttribute(Type valueType, string propertyName = "Value")
		: base(propertyName) => ValueType = valueType;

	/// <summary>
	/// Gets the underlying value type for the automatic form, or <see langword="null"/> for the manual form.
	/// </summary>
	public Type? ValueType { get; }
}
