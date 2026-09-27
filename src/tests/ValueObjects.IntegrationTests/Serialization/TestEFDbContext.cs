using Microsoft.EntityFrameworkCore;

namespace Purview.ValueObjects.Serialization;

sealed class TestEFDbContext(DbContextOptions<TestEFDbContext> options) : DbContext(options)
{
	public DbSet<EFCustomer> Customers => Set<EFCustomer>();

	public DbSet<EFOrder> Orders => Set<EFOrder>();

	public DbSet<EFDomainEvent> Events => Set<EFDomainEvent>();

	public DbSet<EFAutoKeyEntity> AutoKeys => Set<EFAutoKeyEntity>();

	public DbSet<EFClientOwnedKeyEntity> ClientOwnedKeys => Set<EFClientOwnedKeyEntity>();

	protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
	{
		base.ConfigureConventions(configurationBuilder);

		// Applies the generated key value generators to key properties typed as a value object that opted
		// in with GenerateEFValueGenerator.
		configurationBuilder.UseValueObjectKeyGenerators();
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.ConfigureValueObjects();

		// An entity that owns its identifiers wins over the generated convention.
		modelBuilder.Entity<EFClientOwnedKeyEntity>().Property(entity => entity.Id).ValueGeneratedNever();
	}
}
