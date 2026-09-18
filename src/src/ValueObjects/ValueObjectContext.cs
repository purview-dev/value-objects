namespace Purview.ValueObjects;

/// <summary>
/// Provides the contextual information available when creating a contextual value object.
/// </summary>
/// <typeparam name="TOwner">The owner type providing the context.</typeparam>
/// <param name="Owner">The owner instance the value object is being created for.</param>
/// <param name="MemberName">The name of the owner member the value object is being assigned to.</param>
/// <param name="Reason">The optional reason that triggered the creation.</param>
public readonly record struct ValueObjectContext<TOwner>(TOwner Owner, string MemberName, string? Reason = null);
