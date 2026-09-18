using System.Text.Json;
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
