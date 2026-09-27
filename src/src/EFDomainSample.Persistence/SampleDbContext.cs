using Microsoft.EntityFrameworkCore;
using Purview.ValueObjects.EFDomainSample.Domain;

namespace Purview.ValueObjects.EFDomainSample.Persistence;

/// <summary>A tenant as it is stored: its identifier is a value object and its key is a column.</summary>
sealed class TenantRecord
{
	public TenantId Id { get; set; }

	public TenantKey Key { get; set; }

	public string DisplayName { get; set; } = string.Empty;
}

/// <summary>A customer as it is stored: every value object converted to its primitive column.</summary>
sealed class CustomerRecord
{
	public CustomerId Id { get; set; }

	public TenantId TenantId { get; set; }

	public EmailAddress Email { get; set; }

	public string Name { get; set; } = string.Empty;

	public TenantRecord Tenant { get; set; } = default!;
}

/// <summary>
/// The context for the sample. The domain project does not reference Entity Framework Core, so the
/// generated registry lives here and maps the domain value objects inline.
/// </summary>
sealed class SampleDbContext(DbContextOptions<SampleDbContext> options) : DbContext(options)
{
	public DbSet<TenantRecord> Tenants => Set<TenantRecord>();

	public DbSet<CustomerRecord> Customers => Set<CustomerRecord>();

	/// <summary>Gets or sets the tenant the query filter and writes are scoped to.</summary>
	public TenantId CurrentTenantId { get; set; } = TenantId.Hydrate(Guid.Empty);

	protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
	{
		ArgumentNullException.ThrowIfNull(configurationBuilder);

		base.ConfigureConventions(configurationBuilder);

		// Applies the generated key value generators to key properties typed as an opted-in value object.
		// Version 7 identifiers ascend in plain byte order, which is what PostgreSQL, SQLite, MySQL, and a
		// non-clustered SQL Server key compare. A clustered SQL Server key would instead pass
		// ValueObjectKeyOrdering.SqlServer, which moves the timestamp into the bytes SQL Server compares first.
		configurationBuilder.UseValueObjectKeyGenerators();
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		ArgumentNullException.ThrowIfNull(modelBuilder);

		// Maps every value object in this compilation and in the referenced domain assembly.
		modelBuilder.ConfigureValueObjects();

		modelBuilder.Entity<TenantRecord>(tenant =>
		{
			tenant.HasKey(record => record.Id);
			tenant.Property(record => record.Key).HasMaxLength(100).IsRequired();
			tenant.HasIndex(record => record.Key).IsUnique();
			tenant.Property(record => record.DisplayName).HasMaxLength(200).IsRequired();
		});

		modelBuilder.Entity<CustomerRecord>(customer =>
		{
			customer.HasKey(record => record.Id);
			customer.Property(record => record.Name).HasMaxLength(200).IsRequired();
			customer
				.HasOne(record => record.Tenant)
				.WithMany()
				.HasForeignKey(record => record.TenantId)
				.OnDelete(DeleteBehavior.Cascade);
		});

		// A tenant-scoped filter over value object properties: the converters translate, so no `.Value`.
		modelBuilder.Entity<CustomerRecord>().HasQueryFilter(record => record.TenantId == CurrentTenantId);
	}
}
