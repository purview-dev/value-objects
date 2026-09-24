using Microsoft.EntityFrameworkCore;

namespace Purview.ValueObjects.Serialization;

sealed class TestEFDbContext(DbContextOptions<TestEFDbContext> options) : DbContext(options)
{
	public DbSet<EFCustomer> Customers => Set<EFCustomer>();

	public DbSet<EFOrder> Orders => Set<EFOrder>();

	public DbSet<EFDomainEvent> Events => Set<EFDomainEvent>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.ConfigureValueObjects();
	}
}
