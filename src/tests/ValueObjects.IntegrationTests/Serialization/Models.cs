namespace Purview.ValueObjects.Serialization;

/// <summary>
/// A plain enum column: it is neither a scalar nor a complex value object, so Entity Framework Core must
/// keep mapping it with its own enum support.
/// </summary>
public enum CustomerKind
{
	Active,
	Suspended,
}

sealed class EFCustomer
{
	public CustomerId Id { get; set; }

	public EmailAddress Email { get; set; }

	public OrderStatus Status { get; set; }

	public CustomerKind Kind { get; set; }
}

sealed class EFOrder
{
	public OrderId Id { get; set; }

	public Money Total { get; set; }
}

sealed class EFDomainEvent
{
	public Guid Id { get; set; }

	public Audit Audit { get; set; }
}

[Scalar]
public readonly partial record struct OrderId
{
	public Guid Value { get; }

	static partial void OnValidate(Guid value)
	{
		if (value == Guid.Empty)
			throw new ArgumentException("Order id cannot be empty.", nameof(value));
	}
}

[ValueObject(EFMapping = EntityFrameworkMapping.Json)]
public readonly partial record struct Audit
{
	public DateTimeOffset OccurredAt { get; }

	partial void OnValidate(DateTimeOffset occurredAt)
	{
		if (occurredAt == default)
			throw new ArgumentException("OccurredAt cannot be default.", nameof(occurredAt));
	}
}
