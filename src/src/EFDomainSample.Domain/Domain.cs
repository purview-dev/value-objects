using Purview.ValueObjects.Serialization;

namespace Purview.ValueObjects.EFDomainSample.Domain;

/// <summary>
/// Identifies a customer. It opts into Entity Framework Core key value generation, so a new customer's
/// identifier is assigned when the record is added.
/// </summary>
[Scalar(GenerateEFValueGenerator = true)]
public readonly partial record struct CustomerId
{
	public Guid Value { get; }
}

/// <summary>Identifies the tenant boundary that owns the data.</summary>
[Scalar]
public readonly partial record struct TenantId
{
	public Guid Value { get; }
}

/// <summary>A stable, human-meaningful tenant key, stored in a bounded column.</summary>
[Scalar]
public readonly partial record struct TenantKey
{
	[System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 1)]
	public string Value { get; }

	static partial void OnNormalize(ref string value) => value = value?.Trim().ToLowerInvariant()!;
}

/// <summary>A normalized email address.</summary>
[Scalar]
public readonly partial record struct EmailAddress
{
	public string Value { get; }

	static partial void OnNormalize(ref string value) => value = value?.Trim().ToLowerInvariant()!;

	static partial void OnValidate(string value)
	{
		if (!value.Contains('@', StringComparison.Ordinal))
			throw new ArgumentException("Email address must contain '@'.", nameof(value));
	}
}

/// <summary>
/// A domain entity: it owns its invariants, holds value objects rather than primitives, and knows nothing
/// about Entity Framework Core or persistence.
/// </summary>
public sealed class Customer
{
	Customer(CustomerId id, TenantId tenantId, EmailAddress email, string name)
	{
		Id = id;
		TenantId = tenantId;
		Email = email;
		Name = name;
	}

	public CustomerId Id { get; }

	public TenantId TenantId { get; }

	public EmailAddress Email { get; private set; }

	public string Name { get; private set; }

	public static Customer Create(CustomerId id, TenantId tenantId, EmailAddress email, string name) =>
		new(id, tenantId, email, name?.Trim()!);

	public void Rename(string name) => Name = name?.Trim()!;

	public void ChangeEmail(EmailAddress email) => Email = email;
}
