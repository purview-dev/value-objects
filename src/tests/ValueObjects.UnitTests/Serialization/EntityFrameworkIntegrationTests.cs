using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Purview.ValueObjects.Serialization;

namespace Purview.ValueObjects;

/// <summary>
/// End-to-end Entity Framework Core integration tests: automatic mapping of scalar and complex value
/// objects via the generated <c>ConfigureValueObjects</c> extension against a real SQLite database.
/// These verify that scalar value objects translate in queries (without <c>.Value</c>) and that complex
/// value objects map as EF Core complex types with queryable nested members.
/// </summary>
public sealed class EntityFrameworkIntegrationTests
{
	[Test]
	public async Task ScalarValueObjects_RoundTripAndTranslateInEqualityQueries()
	{
		await using SqliteConnection connection = new("Data Source=:memory:");
		await connection.OpenAsync();

		TestEfDbContext context = new(new DbContextOptionsBuilder<TestEfDbContext>().UseSqlite(connection).Options);
		await using (context)
		{
			await context.Database.EnsureCreatedAsync();

			var email = EmailAddress.Create("demo@example.com");
			EfCustomer customer = new()
			{
				Id = CustomerId.Hydrate(Guid.NewGuid()),
				Email = email,
				Status = OrderStatus.Hydrate(OrderStatusKind.Shipped),
			};

			context.Customers.Add(customer);
			await context.SaveChangesAsync();
			context.ChangeTracker.Clear();

			// Queries use the value object type directly; no `.Value` required.
			var found = await context.Customers.SingleAsync(c => c.Email == email);
			await Assert.That(found.Email).IsEqualTo(email);
			await Assert.That(found.Status).IsEqualTo(OrderStatus.Hydrate(OrderStatusKind.Shipped));
		}
	}

	[Test]
	public async Task ComplexValueObjects_MapAsComplexTypesAndTranslateNestedMemberQueries()
	{
		await using SqliteConnection connection = new("Data Source=:memory:");
		await connection.OpenAsync();

		TestEfDbContext context = new(new DbContextOptionsBuilder<TestEfDbContext>().UseSqlite(connection).Options);
		await using (context)
		{
			await context.Database.EnsureCreatedAsync();

			var money = Money.Create(19.99m, CurrencyCode.Create("USD"));
			context.Orders.Add(new EfOrder { Id = OrderId.Hydrate(Guid.NewGuid()), Total = money });
			await context.SaveChangesAsync();
			context.ChangeTracker.Clear();

			// Nested members of a complex value object translate to real columns.
			var found = await context.Orders.SingleAsync(o => o.Total.Amount > 10m);
			await Assert.That(found.Total).IsEqualTo(money);

			// Nested scalar value objects inside a complex type translate too.
			var usd = await context.Orders.SingleAsync(o => o.Total.Currency == CurrencyCode.Create("USD"));
			await Assert.That(usd.Total.Currency).IsEqualTo(CurrencyCode.Create("USD"));
		}
	}

	[Test]
	public async Task AddDbContextFactory_WithUseValueObjects_MapsWithoutOnModelCreatingOverride()
	{
		await using SqliteConnection connection = new("Data Source=:memory:");
		await connection.OpenAsync();

		ServiceCollection services = new();
		services.AddDbContextFactory<FactoryTestDbContext>(options => options.UseSqlite(connection).UseValueObjects());

		await using var provider = services.BuildServiceProvider();
		var factory = provider.GetRequiredService<IDbContextFactory<FactoryTestDbContext>>();

		await using var context = await factory.CreateDbContextAsync();
		await context.Database.EnsureCreatedAsync();

		var email = EmailAddress.Create("factory@example.com");
		context.Customers.Add(
			new EfCustomer
			{
				Id = CustomerId.Hydrate(Guid.NewGuid()),
				Email = email,
				Status = OrderStatus.Hydrate(OrderStatusKind.Shipped),
			}
		);
		await context.SaveChangesAsync();

		var found = await context.Customers.SingleAsync(c => c.Email == email);
		await Assert.That(found.Email).IsEqualTo(email);
	}

	[Test]
	public async Task JsonMappedComplexValueObjects_RoundTripThroughAJsonColumn()
	{
		await using SqliteConnection connection = new("Data Source=:memory:");
		await connection.OpenAsync();

		TestEfDbContext context = new(new DbContextOptionsBuilder<TestEfDbContext>().UseSqlite(connection).Options);
		await using (context)
		{
			await context.Database.EnsureCreatedAsync();

			var audit = Audit.Create(DateTimeOffset.UtcNow);
			context.Events.Add(new EfDomainEvent { Id = Guid.NewGuid(), Audit = audit });
			await context.SaveChangesAsync();
			context.ChangeTracker.Clear();

			var found = await context.Events.SingleAsync();
			await Assert.That(found.Audit).IsEqualTo(audit);
		}
	}
}

sealed class TestEfDbContext(DbContextOptions<TestEfDbContext> options) : DbContext(options)
{
	public DbSet<EfCustomer> Customers => Set<EfCustomer>();

	public DbSet<EfOrder> Orders => Set<EfOrder>();

	public DbSet<EfDomainEvent> Events => Set<EfDomainEvent>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.ConfigureValueObjects();
	}
}

/// <summary>
/// Registered via <c>AddDbContextFactory</c> with <c>UseValueObjects()</c>; intentionally has no
/// <c>OnModelCreating</c> override to prove the generated model customizer applies the mapping.
/// </summary>
sealed class FactoryTestDbContext(DbContextOptions<FactoryTestDbContext> options) : DbContext(options)
{
	public DbSet<EfCustomer> Customers => Set<EfCustomer>();
}

sealed class EfCustomer
{
	public CustomerId Id { get; set; }

	public EmailAddress Email { get; set; }

	public OrderStatus Status { get; set; }
}

sealed class EfOrder
{
	public OrderId Id { get; set; }

	public Money Total { get; set; }
}

sealed class EfDomainEvent
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

[ValueObject(EfMapping = EfMapping.Json)]
public readonly partial record struct Audit
{
	public DateTimeOffset OccurredAt { get; }

	partial void OnValidate(DateTimeOffset occurredAt)
	{
		if (occurredAt == default)
			throw new ArgumentException("OccurredAt cannot be default.", nameof(occurredAt));
	}
}
