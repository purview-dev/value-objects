namespace Purview.ValueObjects;

/// <summary>
/// A value object whose creation requires additional owner context in order to be validated or constructed.
/// </summary>
/// <typeparam name="TSelf">The concrete value object type.</typeparam>
/// <typeparam name="TValue">The underlying value type wrapped by the value object.</typeparam>
/// <typeparam name="TOwner">The owning type providing the creation context.</typeparam>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1005:Avoid excessive parameters on generic types")]
public interface IContextualValueObject<TSelf, TValue, TOwner>
	where TSelf : IValueObject
{
	/// <summary>
	/// Creates a new instance using the supplied value and owner context.
	/// </summary>
	/// <param name="value">The value to wrap.</param>
	/// <param name="context">The owner and member context used for validation.</param>
	/// <returns>A new contextual value object instance.</returns>
	static abstract TSelf Create(TValue value, in ValueObjectContext<TOwner> context);
}
