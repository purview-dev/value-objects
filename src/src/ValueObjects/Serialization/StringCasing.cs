namespace Purview.ValueObjects.Serialization;

/// <summary>
/// Selects an optional casing canonicalization applied to a string-backed value object (or a string
/// member of a complex value object) during normalization.
/// </summary>
/// <remarks>
/// Casing is applied by the generated <c>Create</c> path, after any configured trimming. Only the
/// invariant-culture operations are exposed, because a value object's canonical form must not depend on
/// the current thread culture.
/// </remarks>
public enum StringCasing
{
	/// <summary>
	/// No casing canonicalization. The value is left exactly as supplied (after any configured trim).
	/// </summary>
	None = 0,

	/// <summary>
	/// Lowercases the value using <see cref="string.ToLowerInvariant"/>.
	/// </summary>
	LowerInvariant = 1,

	/// <summary>
	/// Uppercases the value using <see cref="string.ToUpperInvariant"/>.
	/// </summary>
	UpperInvariant = 2,
}
