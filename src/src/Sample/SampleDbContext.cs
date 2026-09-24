using Microsoft.EntityFrameworkCore;

namespace Purview.ValueObjects.Sample;

/// <summary>
/// Entity Framework Core context for the sample. No <c>OnModelCreating</c> override is needed: the
/// options builder calls <c>UseValueObjects()</c> (generated into this project because it references
/// <c>Microsoft.EntityFrameworkCore</c>), which registers a model customizer that applies the automatic
/// value object mapping after <c>OnModelCreating</c>.
/// </summary>
sealed class SampleDbContext(DbContextOptions<SampleDbContext> options) : DbContext(options)
{
	public DbSet<Order> Orders => Set<Order>();
}
