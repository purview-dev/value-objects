namespace Purview.ValueObjects.Serialization;

/// <summary>
/// Determines how a source-generated ZodSharp schema validator (from the <c>[ZodSchema]</c> attribute)
/// participates in the value object's generated <c>Create</c> path.
/// </summary>
public enum ZodSchemaMode
{
	/// <summary>
	/// The ZodSharp schema validator runs <em>in addition to</em> the value object's
	/// <c>OnValidate</c> hook.
	/// </summary>
	InAdditionToHooks = 0,

	/// <summary>
	/// The ZodSharp schema validator runs <em>instead of</em> the value object's
	/// <c>OnValidate</c> hook. <c>OnNormalize</c> still runs so input is canonicalized first.
	/// </summary>
	InsteadOfHooks = 1,
}
