using Microsoft.EntityFrameworkCore;

namespace Purview.ValueObjects.Serialization;

/// <summary>
/// Registered via <c>AddDbContextFactory</c> with <c>UseValueObjects()</c>; intentionally has no
/// <c>OnModelCreating</c> override to prove the generated model customizer applies the mapping.
/// </summary>
sealed class FactoryTestDbContext(DbContextOptions<FactoryTestDbContext> options) : DbContext(options)
{
	public DbSet<EFCustomer> Customers => Set<EFCustomer>();
}
