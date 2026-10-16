namespace Purview.ValueObjects.Serialization;

/// <summary>
/// Configures built-in string normalization for a string member of a <see cref="ValueObjectAttribute"/>
/// (complex) value object, so the member does not need a hand-written <c>OnNormalize</c> hook.
/// </summary>
/// <remarks>
/// <para>
/// The generated <c>Create</c> path applies <see cref="Trim"/> and <see cref="Casing"/> to the member
/// before validation. Applying the attribute to a member overrides the assembly-level
/// <see cref="ValueObjectDefaultsAttribute.Trim"/> and <see cref="ValueObjectDefaultsAttribute.Casing"/>
/// for that member; a member without the attribute inherits the assembly default.
/// </para>
/// <para>
/// The attribute only has an effect on <see cref="string"/> members. Applying it to a member of any
/// other type is reported as a diagnostic, and the normalization is skipped.
/// </para>
/// <para>
/// Normalization is only applied by the generated <c>Create</c>. When the value object implements its own
/// <c>OnNormalize</c> hook or declares its own <c>Create</c>, that takes precedence and the configured
/// normalization is reported as ignored.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed class StringNormalizeAttribute : Attribute
{
	/// <summary>
	/// Gets or sets whether the member's leading and trailing whitespace should be trimmed.
	/// </summary>
	/// <value>Defaults to <see langword="false"/>.</value>
	public bool Trim { get; init; }

	/// <summary>
	/// Gets or sets the casing canonicalization applied to the member.
	/// </summary>
	/// <value>Defaults to <see cref="StringCasing.None"/>.</value>
	public StringCasing Casing { get; init; } = StringCasing.None;
}
