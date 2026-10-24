namespace Purview.ValueObjects.Sample;

/// <summary>
/// The DTO shape: nullable scalars for the nullable domain values, while the entity keeps the
/// non-nullable persistence scalars behind nullable properties.
/// </summary>
sealed class OrderDto
{
	public OrderId Id { get; set; }

	public EmailAddress CustomerEmail { get; set; }

	public NullableNickname? Nickname { get; set; }

	public NullableNickname PreferredName { get; set; }

	public NullableTimestamp DeliveredAt { get; set; }

	public NullableQuantity Quantity { get; set; }
}

/// <summary>
/// Boundary mapping between the entity and the DTO. <c>Hydrate</c> is used when reading a persisted
/// value (a replay path that must not re-validate); <c>Create</c> is used when writing input, so the
/// non-nullable scalar applies its trim/casing/validation.
/// </summary>
static class OrderMappings
{
	public static OrderDto ToDto(this Order order) =>
		new()
		{
			Id = order.Id,
			CustomerEmail = order.CustomerEmail,
			Nickname = order.Nickname,
			PreferredName = order.PreferredName.HasValue
				? NullableNickname.Hydrate(order.PreferredName.Value.Value)
				: NullableNickname.Null(),
			DeliveredAt = order.DeliveredAt.HasValue
				? NullableTimestamp.Hydrate(order.DeliveredAt.Value.Value)
				: NullableTimestamp.Null(),
			Quantity = order.Quantity.HasValue
				? NullableQuantity.Hydrate(order.Quantity.Value.Value)
				: NullableQuantity.Null(),
		};

	public static Order ToEntity(this OrderDto dto) =>
		new()
		{
			Id = dto.Id,
			CustomerEmail = dto.CustomerEmail,
			Nickname = dto.Nickname,
			// The explicit cast is required: a bare `null` would convert through the implicit
			// creation operator into a populated scalar whose Value is null.
			PreferredName = dto.PreferredName.HasValue
				? NonNullableNickname.Create(dto.PreferredName.Value!)
				: (NonNullableNickname?)null,
			DeliveredAt = dto.DeliveredAt.HasValue
				? NonNullableTimestamp.Create(dto.DeliveredAt.Value.GetValueOrDefault())
				: null,
			Quantity = dto.Quantity.HasValue
				? NonNullableQuantity.Create(dto.Quantity.Value.GetValueOrDefault())
				: null,
		};
}
