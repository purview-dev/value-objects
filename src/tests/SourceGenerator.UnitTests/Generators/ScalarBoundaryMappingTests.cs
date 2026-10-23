namespace Purview.ValueObjects.SourceGenerator.Generators;

/// <summary>
/// The documented boundary mapping between a nullable domain scalar and a non-nullable persistence
/// scalar (<c>NonNullableNickname?</c> on the entity, <c>NullableNickname</c> on the DTO). Both the
/// extension-method form and the explicit-operator form must compile and agree.
/// </summary>
public sealed class ScalarBoundaryMappingTests : ValueObjectSourceGeneratorTestBase
{
	const string Source = """
		namespace Testing
		{
		[Purview.ValueObjects.Serialization.Scalar<string>(Nullable = true)]
		public readonly partial record struct NullableNickname
		{
			public static explicit operator NullableNickname(NonNullableNickname? value) =>
				value is null ? Null() : Hydrate(value.Value.Value);

			public static explicit operator NonNullableNickname?(NullableNickname value) =>
				value.Value is null ? (NonNullableNickname?)null : NonNullableNickname.Create(value.Value);
		}

		[Purview.ValueObjects.Serialization.Scalar<string>(Trim = true, Casing = Purview.ValueObjects.Serialization.StringCasing.LowerInvariant)]
		public readonly partial record struct NonNullableNickname { }

		[Purview.ValueObjects.Serialization.Scalar<System.DateTimeOffset>(Nullable = true)]
		public readonly partial record struct NullableTimestamp { }

		[Purview.ValueObjects.Serialization.Scalar<System.DateTimeOffset>]
		public readonly partial record struct NonNullableTimestamp { }

		[Purview.ValueObjects.Serialization.Scalar<int?>]
		public readonly partial record struct NullableQuantity { }

		[Purview.ValueObjects.Serialization.Scalar<int>]
		public readonly partial record struct NonNullableQuantity { }

		public sealed class Order
		{
			public NonNullableNickname? Nickname { get; set; }

			public NonNullableTimestamp? DeliveredAt { get; set; }

			public NonNullableQuantity? Quantity { get; set; }
		}

		public sealed class OrderDto
		{
			public NullableNickname Nickname { get; set; }

			public NullableTimestamp DeliveredAt { get; set; }

			public NullableQuantity Quantity { get; set; }
		}

		public static class OrderMappings
		{
			public static OrderDto ToDto(Order order) => new()
			{
				Nickname = order.Nickname is null
					? NullableNickname.Null()
					: NullableNickname.Hydrate(order.Nickname.Value.Value),
				DeliveredAt = order.DeliveredAt is null
					? NullableTimestamp.Null()
					: NullableTimestamp.Hydrate(order.DeliveredAt.Value.Value),
				Quantity = order.Quantity is null
					? NullableQuantity.Null()
					: NullableQuantity.Hydrate(order.Quantity.Value.Value),
			};

			public static Order ToEntity(OrderDto dto) => new()
			{
				Nickname = dto.Nickname.Value is null
					? (NonNullableNickname?)null
					: NonNullableNickname.Create(dto.Nickname.Value),
				DeliveredAt = dto.DeliveredAt.Value is null
					? null
					: NonNullableTimestamp.Create(dto.DeliveredAt.Value.Value),
				Quantity = dto.Quantity.Value is null
					? null
					: NonNullableQuantity.Create(dto.Quantity.Value.Value),
			};
		}

		public static class Harness
		{
			public static bool[] RoundTripPreservesValues()
			{
				var deliveredAt = new System.DateTimeOffset(2026, 3, 4, 5, 6, 7, System.TimeSpan.Zero);
				var entity = new Order
				{
					Nickname = NonNullableNickname.Create("  Mixed  "),
					DeliveredAt = NonNullableTimestamp.Create(deliveredAt),
					Quantity = NonNullableQuantity.Create(3),
				};

				var dto = OrderMappings.ToDto(entity);
				var back = OrderMappings.ToEntity(dto);

				return
				[
					dto.Nickname.Value == "mixed",
					dto.DeliveredAt.Value == deliveredAt,
					dto.Quantity.Value == 3,
					back.Nickname!.Value.Value == "mixed",
					back.DeliveredAt!.Value.Value == deliveredAt,
					back.Quantity!.Value.Value == 3,
				];
			}

			public static bool[] NullMapsToNullValueAndBack()
			{
				var entity = new Order();
				var dto = OrderMappings.ToDto(entity);
				var back = OrderMappings.ToEntity(dto);

				return
				[
					dto.Nickname.Value is null,
					dto.DeliveredAt.Value is null,
					dto.Quantity.Value is null,
					back.Nickname is null,
					back.DeliveredAt is null,
					back.Quantity is null,
				];
			}

			public static bool[] CastsAgreeWithTheMappingMethods()
			{
				var deliveredAt = new System.DateTimeOffset(2026, 3, 4, 5, 6, 7, System.TimeSpan.Zero);
				var entity = new Order
				{
					Nickname = NonNullableNickname.Create(" Mixed "),
					DeliveredAt = NonNullableTimestamp.Create(deliveredAt),
					Quantity = NonNullableQuantity.Create(7),
				};

				var cast = new NullableNickname[] { (NullableNickname)entity.Nickname };
				var nullCast = new NullableNickname[] { (NullableNickname)new Order().Nickname };
				var back = (NonNullableNickname?)cast[0];

				return
				[
					cast[0].Value == "mixed",
					nullCast[0].Value is null,
					back!.Value.Value == "mixed",
					((NonNullableNickname?)NullableNickname.Null()) is null,
				];
			}
		}
		}
		""";

	async Task<System.Reflection.Assembly> GenerateAssemblyAsync(CancellationToken cancellationToken)
	{
		using var result = await GenerateAsync(
			Source,
			ValueObjectsGeneratorTestOptions.Default.Compile(),
			cancellationToken
		);
		return await Assert.That(result.CompilationResult.Assembly).IsNotNull();
	}

	static async Task AssertResultsAsync(System.Reflection.Assembly assembly, string methodName, bool[] expected)
	{
		var harness = assembly.GetType("Testing.Harness")!;
		var actual = (bool[])harness.GetMethod(methodName)!.Invoke(null, null)!;

		await Assert.That(string.Join(",", actual)).IsEqualTo(string.Join(",", expected));
	}

	[Test]
	public async Task BoundaryMapping_GivenValues_RoundTripsAndNormalizesOnTheEntityPath(
		CancellationToken cancellationToken
	)
	{
		var assembly = await GenerateAssemblyAsync(cancellationToken);

		await AssertResultsAsync(assembly, "RoundTripPreservesValues", [true, true, true, true, true, true]);
	}

	[Test]
	public async Task BoundaryMapping_GivenNullValues_MapsToNullAndBack(CancellationToken cancellationToken)
	{
		var assembly = await GenerateAssemblyAsync(cancellationToken);

		await AssertResultsAsync(assembly, "NullMapsToNullValueAndBack", [true, true, true, true, true, true]);
	}

	[Test]
	public async Task BoundaryMapping_GivenExplicitOperators_AgreesWithTheMappingMethods(
		CancellationToken cancellationToken
	)
	{
		var assembly = await GenerateAssemblyAsync(cancellationToken);

		await AssertResultsAsync(assembly, "CastsAgreeWithTheMappingMethods", [true, true, true, true]);
	}
}
