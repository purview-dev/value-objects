using System.Data.SqlTypes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Purview.ValueObjects.Serialization;

/// <summary>
/// End-to-end tests for the generated Entity Framework Core key value generator: a Guid-backed scalar
/// value object that opts in with <c>GenerateEFValueGenerator</c> receives a time-ordered (UUIDv7)
/// identifier for an unset key, and never overrides a value the domain supplied.
/// </summary>
public sealed class EntityFrameworkValueGenerationTests
{
	[Test]
	public async Task UnsetValueObjectKey_IsGeneratedAsVersion7Identifier()
	{
		// Arrange
		await using SqliteConnection connection = new("Data Source=:memory:");
		await connection.OpenAsync();

		await using TestEFDbContext context = new(
			new DbContextOptionsBuilder<TestEFDbContext>().UseSqlite(connection).Options
		);
		await context.Database.EnsureCreatedAsync();

		EFAutoKeyEntity entity = new();
		context.AutoKeys.Add(entity);
		await context.SaveChangesAsync();

		// Assert
		await Assert.That(entity.Id.Value).IsNotEqualTo(Guid.Empty);
		await Assert.That(entity.Id.Value.Version).IsEqualTo(7);
	}

	[Test]
	public async Task UnsetValueObjectKeys_AreTimeOrdered()
	{
		// Arrange
		await using SqliteConnection connection = new("Data Source=:memory:");
		await connection.OpenAsync();

		await using TestEFDbContext context = new(
			new DbContextOptionsBuilder<TestEFDbContext>().UseSqlite(connection).Options
		);
		await context.Database.EnsureCreatedAsync();

		EFAutoKeyEntity first = new();
		context.AutoKeys.Add(first);
		await context.SaveChangesAsync();

		// The version 7 timestamp has millisecond resolution, so leave a gap before the second insert.
		await Task.Delay(20);

		EFAutoKeyEntity second = new();
		context.AutoKeys.Add(second);
		await context.SaveChangesAsync();

		// Assert
		await Assert.That(second.Id.Value.CompareTo(first.Id.Value)).IsGreaterThan(0);
	}

	[Test]
	public async Task DomainAssignedValueObjectKey_IsNotOverwritten()
	{
		// Arrange
		await using SqliteConnection connection = new("Data Source=:memory:");
		await connection.OpenAsync();

		await using TestEFDbContext context = new(
			new DbContextOptionsBuilder<TestEFDbContext>().UseSqlite(connection).Options
		);
		await context.Database.EnsureCreatedAsync();

		var assigned = EFSequentialId.Create(Guid.NewGuid());
		EFAutoKeyEntity entity = new() { Id = assigned };
		context.AutoKeys.Add(entity);
		await context.SaveChangesAsync();

		// Assert
		await Assert.That(entity.Id).IsEqualTo(assigned);
	}

	[Test]
	public async Task ClientOwnedValueObjectKey_GivenValueGeneratedNever_IsNotGenerated()
	{
		// Arrange
		await using SqliteConnection connection = new("Data Source=:memory:");
		await connection.OpenAsync();

		await using TestEFDbContext context = new(
			new DbContextOptionsBuilder<TestEFDbContext>().UseSqlite(connection).Options
		);
		await context.Database.EnsureCreatedAsync();

		EFClientOwnedKeyEntity entity = new();
		context.ClientOwnedKeys.Add(entity);
		await context.SaveChangesAsync();

		// Assert: the entity owns its identifiers, so the generated convention left the key alone.
		await Assert.That(entity.Id.Value).IsEqualTo(Guid.Empty);
	}

	[Test]
	public async Task ValueObjectKey_IsPersistedAndQueryableByValueObject()
	{
		// Arrange
		await using SqliteConnection connection = new("Data Source=:memory:");
		await connection.OpenAsync();

		await using TestEFDbContext context = new(
			new DbContextOptionsBuilder<TestEFDbContext>().UseSqlite(connection).Options
		);
		await context.Database.EnsureCreatedAsync();

		EFAutoKeyEntity entity = new();
		context.AutoKeys.Add(entity);
		await context.SaveChangesAsync();
		var generated = entity.Id;
		context.ChangeTracker.Clear();

		// Act
		var found = await context.AutoKeys.SingleAsync(row => row.Id == generated);

		// Assert
		await Assert.That(found.Id).IsEqualTo(generated);
	}

	[Test]
	public async Task KeyMintedByTheApplication_BeforeTheSave_IsPersistedAndReadsBackItsTimestamp()
	{
		// Arrange
		await using SqliteConnection connection = new("Data Source=:memory:");
		await connection.OpenAsync();

		await using TestEFDbContext context = new(
			new DbContextOptionsBuilder<TestEFDbContext>().UseSqlite(connection).Options
		);
		await context.Database.EnsureCreatedAsync();

		// The identifier helper is public, so application code can mint the key an entity will be saved with
		// and use it before the round trip; the generated generator then leaves it alone.
		DateTimeOffset createdAt = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
		var assigned = EFSequentialId.Create(ValueObjectSequentialGuid.NewGuid(createdAt));
		EFAutoKeyEntity entity = new() { Id = assigned };
		context.AutoKeys.Add(entity);

		// Act
		await context.SaveChangesAsync();
		context.ChangeTracker.Clear();
		var found = await context.AutoKeys.SingleAsync(row => row.Id == assigned);

		// Assert
		await Assert.That(found.Id).IsEqualTo(assigned);
		await Assert.That(ValueObjectSequentialGuid.TryGetTimestamp(found.Id.Value, out var recovered)).IsTrue();
		await Assert.That(recovered).IsEqualTo(createdAt);
	}

	[Test]
	public async Task KeyMintedWithSqlServerOrdering_IsPersistedAndAscendsInSqlGuidOrder()
	{
		// Arrange
		await using SqliteConnection connection = new("Data Source=:memory:");
		await connection.OpenAsync();

		await using TestEFDbContext context = new(
			new DbContextOptionsBuilder<TestEFDbContext>().UseSqlite(connection).Options
		);
		await context.Database.EnsureCreatedAsync();

		DateTimeOffset createdAt = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
		var first = EFSequentialId.Create(ValueObjectSequentialGuid.NewSqlServerGuid(createdAt));
		var second = EFSequentialId.Create(ValueObjectSequentialGuid.NewSqlServerGuid(createdAt.AddMilliseconds(1)));
		context.AutoKeys.Add(new EFAutoKeyEntity { Id = first });
		context.AutoKeys.Add(new EFAutoKeyEntity { Id = second });

		// Act
		await context.SaveChangesAsync();
		context.ChangeTracker.Clear();
		var found = await context.AutoKeys.SingleAsync(row => row.Id == first);

		// Assert: the SQL Server strategy is available to application code too, and its values persist like
		// any other minted key.
		await Assert.That(found.Id).IsEqualTo(first);
		await Assert.That(new SqlGuid(second.Value).CompareTo(new SqlGuid(first.Value))).IsGreaterThan(0);
		await Assert
			.That(ValueObjectSequentialGuid.TryGetSqlServerTimestamp(found.Id.Value, out var recovered))
			.IsTrue();
		await Assert.That(recovered).IsEqualTo(createdAt);
	}
}
