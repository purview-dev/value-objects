using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Purview.ValueObjects.Serialization;

/// <summary>
/// Covers which members the generated <c>ConfigureValueObjects</c> registry is allowed to map.
/// </summary>
/// <remarks>
/// The registry walks every readable property of every entity type and attaches a value conversion through
/// <c>modelBuilder.Entity(t).Property(type, name)</c>. That is <em>explicit</em> configuration, which
/// outranks <c>[NotMapped]</c> and <c>Ignore(...)</c> — so a member the author deliberately excluded could
/// be pulled back into the model, producing a column and a migration nobody asked for. A computed,
/// setter-less property is the same problem with an added failure: there is nowhere to materialize into.
/// </remarks>
public class EFNotMappedTests
{
	[Test]
	public async Task ConfigureValueObjects_DoesNotMapAPropertyMarkedNotMapped()
	{
		// Arrange
		await using var context = CreateContext();

		// Act
		var entityType = context.Model.FindEntityType(typeof(EFSelectiveMappingEntity))!;

		// Assert
		await Assert.That(entityType.FindProperty(nameof(EFSelectiveMappingEntity.Email))).IsNotNull();
		await Assert.That(entityType.FindProperty(nameof(EFSelectiveMappingEntity.ScratchEmail))).IsNull();
	}

	[Test]
	public async Task ConfigureValueObjects_DoesNotMapAComputedGetOnlyProperty()
	{
		// Arrange
		await using var context = CreateContext();

		// Act
		var entityType = context.Model.FindEntityType(typeof(EFSelectiveMappingEntity))!;

		// Assert
		await Assert.That(entityType.FindProperty(nameof(EFSelectiveMappingEntity.PrimaryContact))).IsNull();
	}

	[Test]
	public async Task ConfigureValueObjects_DoesNotMapAPropertyIgnoredByTheModelBuilder()
	{
		// Arrange
		await using var context = CreateContext();

		// Act
		var entityType = context.Model.FindEntityType(typeof(EFSelectiveMappingEntity))!;

		// Assert
		await Assert.That(entityType.FindProperty(nameof(EFSelectiveMappingEntity.IgnoredEmail))).IsNull();
	}

	[Test]
	public async Task ConfigureValueObjects_StillRoundTripsTheMappedProperty(CancellationToken cancellationToken)
	{
		// Arrange — excluding members must not break the conversion on the ones that are mapped.
		await using var context = CreateContext();
		await context.Database.EnsureCreatedAsync(cancellationToken);

		context.Entities.Add(
			new EFSelectiveMappingEntity { Id = 1, Email = EmailAddress.Create("someone@example.com") }
		);
		await context.SaveChangesAsync(cancellationToken);
		context.ChangeTracker.Clear();

		// Act
		var loaded = await context.Entities.SingleAsync(cancellationToken);

		// Assert
		await Assert.That(loaded.Email).IsEqualTo(EmailAddress.Create("someone@example.com"));
	}

	// The connection's lifetime is deliberately handed to the context, which closes it in its own Dispose
	// override. An in-memory SQLite database exists only while the connection is open, so it cannot be
	// disposed here without destroying the schema the test just created.
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Reliability",
		"CA2000:Dispose objects before losing scope",
		Justification = "Ownership transfers to SelectiveMappingDbContext, which disposes it."
	)]
	static SelectiveMappingDbContext CreateContext()
	{
		SqliteConnection connection = new("DataSource=:memory:");
		connection.Open();

		var options = new DbContextOptionsBuilder<SelectiveMappingDbContext>().UseSqlite(connection).Options;

		return new SelectiveMappingDbContext(options, connection);
	}
}

sealed class SelectiveMappingDbContext(DbContextOptions<SelectiveMappingDbContext> options, SqliteConnection connection)
	: DbContext(options)
{
	public DbSet<EFSelectiveMappingEntity> Entities => Set<EFSelectiveMappingEntity>();

	public override void Dispose()
	{
		base.Dispose();
		connection.Dispose();
	}

	public override async ValueTask DisposeAsync()
	{
		await base.DisposeAsync();
		await connection.DisposeAsync();
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		// Ignore declared before the registry runs: the registry must respect it rather than re-adding it.
		modelBuilder.Entity<EFSelectiveMappingEntity>().Ignore(entity => entity.IgnoredEmail);

		modelBuilder.ConfigureValueObjects();
	}
}

sealed class EFSelectiveMappingEntity
{
	public int Id { get; set; }

	/// <summary>A normal mapped value-object property.</summary>
	public EmailAddress Email { get; set; }

	/// <summary>Deliberately excluded by the author.</summary>
	[System.ComponentModel.DataAnnotations.Schema.NotMapped]
	public EmailAddress ScratchEmail { get; set; }

	/// <summary>Excluded through the model builder rather than an attribute.</summary>
	public EmailAddress IgnoredEmail { get; set; }

	/// <summary>Computed and setter-less: there is nowhere for Entity Framework Core to materialize into.</summary>
	public EmailAddress PrimaryContact => Email;
}
