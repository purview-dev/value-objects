using Microsoft.EntityFrameworkCore;

namespace Purview.ValueObjects.Sample;

/// <summary>
/// Demonstrates the nullable-scalar shapes: a nullable scalar behind a <c>T?</c> property, the
/// two-scalar pattern (a non-nullable persistence scalar behind a nullable property), and the DTO
/// mapping between them. Entity Framework Core maps a <c>T?</c> property to a nullable column; a
/// non-nullable struct property stays required because EF Core cannot mark it optional.
/// </summary>
static class NullableScalarDemo
{
	public static async Task RunAsync(SampleDbContext db)
	{
		Console.WriteLine();
		Console.WriteLine("== Nullable scalars, nullable columns and DTO mapping ==");

		var entity = NewOrder("nullable@example.com", 10m, OrderStatusKind.Shipped);
		entity.Nickname = NullableNickname.Create("  Mixed Case  ");
		entity.PreferredName = NonNullableNickname.Create("  Mixed Case  ");
		entity.DeliveredAt = NonNullableTimestamp.Create(DateTimeOffset.UtcNow);
		entity.Quantity = NonNullableQuantity.Create(3);

		var dto = entity.ToDto();
		Console.WriteLine($"DTO PreferredName -> '{dto.PreferredName.Value}'");
		Console.WriteLine($"Entity PreferredName -> '{entity.PreferredName!.Value.Value}'");

		var mappedBack = dto.ToEntity();
		Console.WriteLine($"DTO -> entity -> '{mappedBack.PreferredName!.Value.Value}' (Create trims/lowercases)");
		var castBack = (NonNullableNickname?)dto.PreferredName;
		Console.WriteLine($"DTO -> cast -> '{castBack!.Value.Value}'");

		var nullScalars = NewOrder("null@example.com", 5m, OrderStatusKind.Pending);
		// A nullable scalar's Null() is a null provider value, so the nullable column stores NULL.
		nullScalars.Nickname = NullableNickname.Null();

		var nullScalarsDto = nullScalars.ToDto();
		Console.WriteLine($"Null() -> DTO PreferredName HasValue -> {nullScalarsDto.PreferredName.HasValue}");
		Console.WriteLine(
			$"Null() -> DTO -> entity PreferredName is null -> {nullScalarsDto.ToEntity().PreferredName is null}"
		);

		db.Orders.AddRange(entity, nullScalars);
		await db.SaveChangesAsync();
		db.ChangeTracker.Clear();

		var stored = await db.Orders.SingleAsync(order => order.Id == entity.Id);
		Console.WriteLine($"Stored PreferredName -> '{stored.PreferredName!.Value.Value}'");
		Console.WriteLine($"Stored DeliveredAt -> {stored.DeliveredAt!.Value.Value:O}");
		Console.WriteLine($"Stored Quantity -> {stored.Quantity!.Value.Value}");

		var storedNulls = await db.Orders.SingleAsync(order => order.Id == nullScalars.Id);
		Console.WriteLine($"Null() persisted as NULL, read back as null -> {storedNulls.Nickname is null}");

		var orderType = db.Model.FindEntityType(typeof(Order))!;
		foreach (
			var propertyName in new[]
			{
				nameof(Order.Nickname),
				nameof(Order.PreferredName),
				nameof(Order.DeliveredAt),
				nameof(Order.Quantity),
				nameof(Order.CustomerEmail),
			}
		)
		{
			var property = orderType.FindProperty(propertyName)!;
			Console.WriteLine($"{propertyName} column nullable -> {property.IsNullable}");
		}
	}

	static Order NewOrder(string email, decimal total, OrderStatusKind status) =>
		new()
		{
			Id = OrderId.Hydrate(Guid.NewGuid()),
			CustomerEmail = EmailAddress.Create(email),
			Total = Money.Create(total, CurrencyCode.Create("USD")),
			Status = OrderStatus.Hydrate(status),
		};
}
