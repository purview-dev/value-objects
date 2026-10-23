namespace Purview.ValueObjects.Serialization;

/// <summary>
/// Marks a struct or class as a scalar value object whose underlying value type is given by
/// <typeparamref name="TValue"/>, and lets the source generator declare the underlying property.
/// </summary>
/// <typeparam name="TValue">The type of the underlying value the value object wraps.</typeparam>
/// <remarks>
/// <para>
/// This is the <b>automatic</b> form: the generator emits the property named by
/// <see cref="ScalarOptionsAttribute.PropertyName"/> (default <c>Value</c>) as
/// <c>public TValue Value { get; init; }</c>. Declaring a member with that name yourself is an error
/// (<c>VO1022</c>) — use <see cref="ScalarAttribute"/> instead when you want to own the property. The
/// two forms are mutually exclusive for a given type.
/// </para>
/// <para>
/// <b>Nullability.</b> A nullable reference type cannot be used as a generic attribute argument (the
/// compiler reports <c>CS8970</c> because nullable annotations are not representable in attribute
/// metadata), so <c>[Scalar&lt;string?&gt;]</c> does not compile. Set
/// <see cref="ScalarOptionsAttribute.Nullable"/> to have the generator declare the property as nullable:
/// <c>[Scalar&lt;string&gt;(Nullable = true)]</c> produces <c>public string? Value { get; init; }</c>, and
/// <c>[Scalar&lt;DateTimeOffset&gt;(Nullable = true)]</c> produces
/// <c>public DateTimeOffset? Value { get; init; }</c>. Nullable value types can also be written directly,
/// for example <c>[Scalar&lt;int?&gt;]</c>. A nullable scalar exposes an additional
/// <c>Null()</c> factory, equivalent to <c>Hydrate(null)</c>, and a <c>HasValue</c> property that
/// reports whether the underlying value is null without validating it.
/// </para>
/// <para>
/// <b>Readability.</b> The automatic form is concise, but the underlying type and nullability are
/// expressed in the attribute rather than in a property declaration. Some readers prefer the explicit
/// manual form:
/// <code>
/// // Automatic.
/// [Scalar&lt;string&gt;(Nullable = true)]
/// public readonly partial record struct Handle { }
///
/// // Manual.
/// [Scalar]
/// public readonly partial record struct Handle
/// {
///     public string? Value { get; init; }
/// }
/// </code>
/// Each form is supported on its own. Combining them is reported as an error: <c>VO1022</c> for a
/// member with the generator-owned name, and <c>VO1028</c>/<c>VO1029</c>/<c>VO1030</c> when the
/// declaration also asserts nullability or a mismatched type.
/// </para>
/// <para>
/// <b>Comparisons.</b> A nullable scalar can be compared with <c>null</c> and with the underlying value
/// in either order. Its conversion to the underlying value is explicit (<c>(string)value</c> or
/// <c>value.Value</c>), which keeps <c>value == null</c> unambiguous against the synthesized
/// <c>(TSelf, TSelf)</c> equality. A non-nullable scalar keeps the implicit conversion.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
public sealed class ScalarAttribute<TValue>(string propertyName = "Value") : ScalarOptionsAttribute(propertyName);
