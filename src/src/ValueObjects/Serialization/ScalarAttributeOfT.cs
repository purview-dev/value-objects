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
/// <see cref="ScalarOptionsAttribute.Nullable"/> to have the generator declare the property as a nullable
/// reference type: <c>[Scalar&lt;string&gt;(Nullable = true)]</c> produces
/// <c>public string? Value { get; init; }</c>. This applies to reference types only; nullable value types
/// are written directly, for example <c>[Scalar&lt;int?&gt;]</c>.
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
/// Both are supported; neither is reported as a diagnostic.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
public sealed class ScalarAttribute<TValue>(string propertyName = "Value") : ScalarOptionsAttribute(propertyName);
