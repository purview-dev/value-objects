using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Purview.ValueObjects.Serialization;

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

		TestEFDbContext context = new(new DbContextOptionsBuilder<TestEFDbContext>().UseSqlite(connection).Options);
		await using (context)
		{
			await context.Database.EnsureCreatedAsync();

			var email = EmailAddress.Create("demo@example.com");
			EFCustomer customer = new()
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
	public async Task ScalarValueObjects_TranslateEqualityPredicateAgainstGuidBackedKey()
	{
		// Arrange
		await using SqliteConnection connection = new("Data Source=:memory:");
		await connection.OpenAsync();

		TestEFDbContext context = new(new DbContextOptionsBuilder<TestEFDbContext>().UseSqlite(connection).Options);
		await using (context)
		{
			await context.Database.EnsureCreatedAsync();

			var customerId = CustomerId.Hydrate(Guid.NewGuid());
			var email = EmailAddress.Create("regression@example.com");
			context.Customers.Add(
				new EFCustomer
				{
					Id = customerId,
					Email = email,
					Status = OrderStatus.Hydrate(OrderStatusKind.Shipped),
				}
			);
			await context.SaveChangesAsync();
			context.ChangeTracker.Clear();

			// Act
			var found = await context.Customers.SingleAsync(c => c.Id == customerId);

			// Assert
			await Assert.That(found.Id).IsEqualTo(customerId);
			await Assert.That(found.Email).IsEqualTo(email);
		}
	}

	[Test]
	public async Task ComplexValueObjects_MapAsComplexTypesAndTranslateNestedMemberQueries()
	{
		await using SqliteConnection connection = new("Data Source=:memory:");
		await connection.OpenAsync();

		TestEFDbContext context = new(new DbContextOptionsBuilder<TestEFDbContext>().UseSqlite(connection).Options);
		await using (context)
		{
			await context.Database.EnsureCreatedAsync();

			var money = Money.Create(19.99m, CurrencyCode.Create("USD"));
			context.Orders.Add(new EFOrder { Id = OrderId.Hydrate(Guid.NewGuid()), Total = money });
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
			new EFCustomer
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

		TestEFDbContext context = new(new DbContextOptionsBuilder<TestEFDbContext>().UseSqlite(connection).Options);
		await using (context)
		{
			await context.Database.EnsureCreatedAsync();

			var audit = Audit.Create(DateTimeOffset.UtcNow);
			context.Events.Add(new EFDomainEvent { Id = Guid.NewGuid(), Audit = audit });
			await context.SaveChangesAsync();
			context.ChangeTracker.Clear();

			var found = await context.Events.SingleAsync();
			await Assert.That(found.Audit).IsEqualTo(audit);
		}
	}
}
