using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.Json;
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
	public async Task ScalarValueObjects_TranslateEqualityAgainstTheRawUnderlyingValue()
	{
		// Arrange
		await using SqliteConnection connection = new("Data Source=:memory:");
		await connection.OpenAsync();

		TestEFDbContext context = new(new DbContextOptionsBuilder<TestEFDbContext>().UseSqlite(connection).Options);
		await using (context)
		{
			await context.Database.EnsureCreatedAsync();

			var id = Guid.NewGuid();
			var email = EmailAddress.Create("raw-value@example.com");
			var status = OrderStatus.Hydrate(OrderStatusKind.Shipped);
			context.Customers.Add(
				new EFCustomer
				{
					Id = CustomerId.Hydrate(id),
					Email = email,
					Status = status,
				}
			);
			await context.SaveChangesAsync();
			context.ChangeTracker.Clear();

			// Entity Framework Core applies the converted property's value converter to the raw provider
			// value when a query compares that property to the underlying primitive (dotnet/efcore#32030).
			// Its built-in converter coerces that value with Convert.ChangeType, which throws for provider
			// types that do not implement IConvertible (Guid among them) — the generated converter accepts
			// either shape instead, so these comparisons translate.
			var byGuid = await context.Customers.SingleAsync(c => c.Id == id);
			await Assert.That(byGuid.Id).IsEqualTo(CustomerId.Hydrate(id));
			await Assert.That(byGuid.Email).IsEqualTo(email);

			var byString = await context.Customers.SingleAsync(c => c.Email == "raw-value@example.com");
			await Assert.That(byString.Id).IsEqualTo(CustomerId.Hydrate(id));

			// An enum-backed scalar converts through the enum's integral provider type, so comparing the
			// property to the raw enum value translates too.
			var byEnum = await context.Customers.SingleAsync(c => c.Status == OrderStatusKind.Shipped);
			await Assert.That(byEnum.Status).IsEqualTo(status);

			// Value-object-to-value-object comparisons keep working.
			var byValueObject = await context.Customers.SingleAsync(c => c.Email == email);
			await Assert.That(byValueObject.Id).IsEqualTo(CustomerId.Hydrate(id));
		}
	}

	[Test]
	public async Task PlainEnumProperties_KeepEntityFrameworkCoresOwnMapping()
	{
		// Arrange
		await using SqliteConnection connection = new("Data Source=:memory:");
		await connection.OpenAsync();

		TestEFDbContext context = new(new DbContextOptionsBuilder<TestEFDbContext>().UseSqlite(connection).Options);
		await using (context)
		{
			await context.Database.EnsureCreatedAsync();

			var email = EmailAddress.Create("plain-enum@example.com");
			context.Customers.Add(
				new EFCustomer
				{
					Id = CustomerId.Hydrate(Guid.NewGuid()),
					Email = email,
					Status = OrderStatus.Hydrate(OrderStatusKind.Shipped),
					Kind = CustomerKind.Suspended,
				}
			);
			await context.SaveChangesAsync();
			context.ChangeTracker.Clear();

			// Act + Assert — the generated registry converts only value-object-typed properties.
			await AssertPlainEnumsAreLeftToEntityFrameworkCore(context);

			var found = await context.Customers.SingleAsync(c => c.Kind == CustomerKind.Suspended);
			await Assert.That(found.Email).IsEqualTo(email);
		}
	}

	static async Task AssertPlainEnumsAreLeftToEntityFrameworkCore(TestEFDbContext context)
	{
		EFCustomer probe = new();
		var kind = context.Entry(probe).Property(c => c.Kind).Metadata;
		var converted = context.Entry(probe).Property(c => c.Email).Metadata;

		// A plain enum keeps its own CLR type, column type, and Entity Framework Core's own enum-to-number
		// conversion — the value-object machinery leaves it untouched while the value-object property beside
		// it is still converted.
		await Assert.That(kind.ClrType).IsEqualTo(typeof(CustomerKind));
		await Assert.That(kind.GetRelationalTypeMapping().StoreType).IsEqualTo("INTEGER");

		var enumConverter = kind.GetTypeMapping().Converter;
		await Assert.That(enumConverter).IsNotNull();
		await Assert.That(enumConverter!.ModelClrType).IsEqualTo(typeof(CustomerKind));
		await Assert.That(enumConverter.ProviderClrType).IsEqualTo(typeof(int));

		var valueObjectConverter = converted.GetTypeMapping().Converter;
		await Assert.That(valueObjectConverter).IsNotNull();
		await Assert.That(valueObjectConverter!.ModelClrType).IsEqualTo(typeof(EmailAddress));
	}

	[Test]
	public async Task ScalarValueObjects_ConverterSatisfiesTheCompiledModelContract()
	{
		// Entity Framework Core's design-time model generator rebuilds a converter from its own type only
		// when the type declares a constructor taking JsonValueReaderWriter and exposes a property named
		// JsonReaderWriter returning a non-null value; it then renders that value as <Type>.Instance.
		// See CSharpRuntimeAnnotationCodeGenerator.Create(ValueConverter, ...).
		var converter = CustomerId.EF.Converter;
		var converterType = converter.GetType();

		await Assert.That(converterType.GetConstructor([typeof(JsonValueReaderWriter)])).IsNotNull();

		var jsonReaderWriter = converterType.GetProperty("JsonReaderWriter");
		await Assert.That(jsonReaderWriter).IsNotNull();

		var readerWriter = jsonReaderWriter!.GetValue(converter);
		await Assert.That(readerWriter).IsNotNull();

		var instance = readerWriter!.GetType().GetProperty("Instance");
		await Assert.That(instance).IsNotNull();
		await Assert.That(instance!.GetMethod!.IsPublic).IsTrue();
		await Assert.That(instance.GetMethod!.IsStatic).IsTrue();
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
