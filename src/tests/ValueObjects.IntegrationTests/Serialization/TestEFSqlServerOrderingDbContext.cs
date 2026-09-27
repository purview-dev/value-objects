using Microsoft.EntityFrameworkCore;

namespace Purview.ValueObjects.Serialization;

/// <summary>
/// A context that registers the generated key value generator convention with SQL Server-ordered
/// identifiers, so generated keys ascend in SQL Server's <c>uniqueidentifier</c> ordering rather than in
/// plain byte order.
/// </summary>
sealed class TestEFSqlServerOrderingDbContext(DbContextOptions<TestEFSqlServerOrderingDbContext> options)
	: DbContext(options)
{
	public DbSet<EFAutoKeyEntity> AutoKeys => Set<EFAutoKeyEntity>();

	protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
	{
		base.ConfigureConventions(configurationBuilder);

		configurationBuilder.UseValueObjectKeyGenerators(ValueObjectKeyOrdering.SqlServer);
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.ConfigureValueObjects();
	}
}
