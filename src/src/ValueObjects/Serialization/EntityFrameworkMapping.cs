namespace Purview.ValueObjects.Serialization;

/// <summary>
/// Determines how a complex <c>[ValueObject]</c> type is mapped when the Entity Framework Core
/// integration is active (when the consuming project references <c>Microsoft.EntityFrameworkCore</c>).
/// </summary>
/// <remarks>
/// The value is a code-generation preference only: no Entity Framework types are referenced by this
/// package. The source generator emits the corresponding Entity Framework members only when Entity
/// Framework Core is referenced by the consuming project.
/// </remarks>
public enum EntityFrameworkMapping
{
	/// <summary>
	/// Maps the value object as an Entity Framework Core complex type (EF Core 8+), producing separate
	/// columns for each member and enabling translation of nested member predicates in queries.
	/// </summary>
	ComplexType = 0,

	/// <summary>
	/// Maps the value object to a JSON column using the generated JSON converter.
	/// </summary>
	/// <remarks>
	/// Deep predicates require the provider's JSON translation; equality against the whole value
	/// object translates through the JSON serialization.
	/// </remarks>
	Json = 1,

	/// <summary>
	/// Generates no Entity Framework mapping members for the value object.
	/// </summary>
	None = 2,
}
