using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Purview.ValueObjects.Ef;
using Purview.ValueObjects.Serialization;

JsonSerializerOptions options = new() { WriteIndented = true };
options.Converters.Add(new ScalarJsonConverterFactory());

Console.WriteLine("== Scalar value objects ==");
var email = EmailAddress.Create("  Demo@Example.COM  ");
Console.WriteLine($"EmailAddress.Create -> '{email.Value}'");
Console.WriteLine($"EmailAddress == string -> {email == "demo@example.com"}");

var raw = EmailAddress.Hydrate("already-normalized@example.com");
Console.WriteLine($"EmailAddress.Hydrate -> '{raw.Value}'");

var created = EmailAddress.TryCreate("not-an-email", out _);
Console.WriteLine($"TryCreate invalid -> {created}");

Console.WriteLine();
Console.WriteLine("== Complex value objects ==");
var money = Money.Create(19.99m, CurrencyCode.Create("usd"));
Console.WriteLine($"Money -> {money.Amount} {money.Currency.Value}");

Console.WriteLine();
Console.WriteLine("== JSON (Entity Framework JSON column) ==");

Order order = new()
{
	Id = OrderId.Hydrate(Guid.NewGuid()),
	CustomerEmail = email,
	Total = money,
	Status = OrderStatus.Hydrate(OrderStatusKind.Shipped),
};
var json = JsonSerializer.Serialize(order, options);
Console.WriteLine(json);

var roundTripped = JsonSerializer.Deserialize<Order>(json, options);
Console.WriteLine($"Round-trip -> {roundTripped!.CustomerEmail.Value}, {roundTripped.Total.Currency.Value}");

Console.WriteLine();
Console.WriteLine("== Entity Framework Core ==");

await using Microsoft.Data.Sqlite.SqliteConnection connection = new("Data Source=:memory:");
await connection.OpenAsync();
DbContextOptionsBuilder<SampleDbContext> builder = new();
builder.UseSqlite(connection).UseValueObjects();
await using SampleDbContext db = new(builder.Options);
await db.Database.EnsureCreatedAsync();

db.Orders.Add(
	new Order
	{
		Id = OrderId.Hydrate(Guid.NewGuid()),
		CustomerEmail = EmailAddress.Create("demo@example.com"),
		Total = Money.Create(29.99m, CurrencyCode.Create("USD")),
		Status = OrderStatus.Hydrate(OrderStatusKind.Shipped),
	}
);
await db.SaveChangesAsync();

// Queries use the value object type directly - no `.Value` required.
var shipped = await db.Orders.Where(o => o.Status == OrderStatus.Hydrate(OrderStatusKind.Shipped)).ToListAsync();
Console.WriteLine($"Orders shipped -> {shipped.Count}");

var expensive = await db.Orders.Where(o => o.Total.Amount > 20m).ToListAsync();
Console.WriteLine($"Orders over $20 -> {expensive.Count}");

var emailToMatch = EmailAddress.Create("demo@example.com");
var matched = await db.Orders.Where(o => o.CustomerEmail == emailToMatch).ToListAsync();
Console.WriteLine($"Orders for {emailToMatch.Value} -> {matched.Count}");
