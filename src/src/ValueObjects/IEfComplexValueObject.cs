namespace Purview.ValueObjects;

/// <summary>
/// Marks a complex value object as participating in the Entity Framework Core integration.
/// </summary>
/// <typeparam name="TSelf">The concrete complex value object type.</typeparam>
/// <remarks>
/// This is a code-generation marker only: it references no Entity Framework types. The source generator
/// implements it on <c>[ValueObject]</c> types when Entity Framework Core is referenced by the consuming
/// project and the type has not opted out, and emits the nested <c>Ef</c> class exposing the generated
/// <c>ValueComparer</c> (and, for <see cref="Serialization.EfMapping.Json"/>, a JSON column converter).
/// The generated <c>ConfigureValueObjects</c> extension uses this marker to discover and automatically
/// apply complex type or JSON column mapping.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Design",
	"CA1040:Avoid empty interfaces",
	Justification = "Marker identifying value objects that participate in Entity Framework mapping"
)]
public interface IEfComplexValueObject<TSelf> : IValueObject
	where TSelf : IValueObject
{
	//
}
