namespace Purview.ValueObjects;

/// <summary>
/// Marks a scalar value object as participating in the Entity Framework Core integration.
/// </summary>
/// <typeparam name="TSelf">The concrete scalar value object type.</typeparam>
/// <typeparam name="TProvider">The underlying provider type the value object maps to.</typeparam>
/// <remarks>
/// This is a code-generation marker only: it references no Entity Framework types. The source generator
/// implements it on <c>[Scalar]</c> types when Entity Framework Core is referenced by the consuming
/// project and the type has not opted out, and emits the nested <c>Ef</c> class exposing the generated
/// <c>ValueConverter</c> and <c>ValueComparer</c>. The generated <c>ConfigureValueObjects</c> extension
/// uses this marker to discover and automatically apply value conversions.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Design",
	"CA1040:Avoid empty interfaces",
	Justification = "Marker identifying value objects that participate in Entity Framework mapping"
)]
public interface IEfScalarValueObject<TSelf, TProvider> : IValueObject
	where TSelf : IValueObject
{
	//
}
