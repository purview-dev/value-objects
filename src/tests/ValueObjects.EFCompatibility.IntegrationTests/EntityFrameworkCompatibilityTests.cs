using System.Data.SqlTypes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Purview.ValueObjects.EFCompatibility;

/// <summary>
/// Proves the generated Entity Framework Core integration compiles and runs against EF Core 7, 9, and 10.
/// Each target framework resolves a different EF Core version, so assertions that depend on EF Core 8 APIs
/// are guarded per target: the complex type is mapped from EF Core 8, and the compiled-model members the
/// generated converters expose exist only there.
/// </summary>
public sealed class EntityFrameworkCompatibilityTests
{
	[Test]
	public async Task GeneratedKeyValueObject_GivenUnsetKey_IsGeneratedAndRoundTrips()
	{
		// Arrange
		await using var connection = await OpenConnectionAsync();
		await using var context = CreateContext(connection);
		await context.Database.EnsureCreatedAsync();

		var before = DateTimeOffset.UtcNow;
		var entity = CreateEntity("ABC");
		context.Entities.Add(entity);

		// Act
		await context.SaveChangesAsync();
		var generated = entity.Id;
		context.ChangeTracker.Clear();
		var found = await context.Entities.SingleAsync(row => row.Id == generated);

		// Assert: the key was generated in process, is a version 7 identifier, and reads back as a
		// millisecond timestamp inside the window this test ran in.
		await Assert.That(generated.Value).IsNotEqualTo(Guid.Empty);
		await Assert.That(GuidVersion(generated.Value)).IsEqualTo(7);
		await Assert.That(found.Id).IsEqualTo(generated);
		await Assert.That(found.Code).IsEqualTo(CompatibilityCode.Create("ABC"));

		await Assert.That(ValueObjectSequentialGuid.TryGetTimestamp(generated.Value, out var createdAt)).IsTrue();
		await Assert.That(createdAt).IsGreaterThanOrEqualTo(before.AddMilliseconds(-1));
		await Assert.That(createdAt).IsLessThanOrEqualTo(DateTimeOffset.UtcNow.AddMilliseconds(1));
	}

	[Test]
	public async Task GeneratedIdentifierHelper_AscendsInSqlServerOrderingForLaterTimes()
	{
		// Arrange: the helper is emitted into this assembly, so both ordering strategies are exercised here
		// on every target framework.
		DateTimeOffset start = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

		// Act
		var version7 = ValueObjectSequentialGuid.NewGuid(start);
		var previous = ValueObjectSequentialGuid.NewSqlServerGuid(start.AddMilliseconds(1));
		var next = ValueObjectSequentialGuid.NewSqlServerGuid(start.AddMilliseconds(2));

		// Assert
		await Assert.That(GuidVersion(version7)).IsEqualTo(7);
		await Assert.That(new SqlGuid(next).CompareTo(new SqlGuid(previous))).IsGreaterThan(0);
		await Assert.That(ValueObjectSequentialGuid.TryGetSqlServerTimestamp(next, out var recovered)).IsTrue();
		await Assert.That(recovered).IsEqualTo(start.AddMilliseconds(2));
		await Assert
			.That(new SqlGuid(ValueObjectSequentialGuid.MinSqlServerGuidFor(start)).CompareTo(new SqlGuid(previous)))
			.IsLessThan(0);
	}

	[Test]
	public async Task NullableScalarValueObject_ConverterHandlesNullAndColumnRoundTrips()
	{
		// The provider is int?, so the generated converter must accept and produce null on both directions.
		var converter = CompatibilityScore.EF.Converter;
		await Assert.That(converter.ProviderClrType).IsEqualTo(typeof(int?));
		await Assert.That(converter.ConvertToProvider(CompatibilityScore.Hydrate(null))).IsNull();

		var fromNull = converter.ConvertFromProvider(null);
		await Assert.That(fromNull).IsNull();

		// A non-null value round-trips through a real column.
		await using var connection = await OpenConnectionAsync();
		await using var context = CreateContext(connection);
		await context.Database.EnsureCreatedAsync();

		var entity = CreateEntity("NULLABLE");
		entity.Score = CompatibilityScore.Create(42);
		context.Entities.Add(entity);
		await context.SaveChangesAsync();
		var generated = entity.Id;
		context.ChangeTracker.Clear();
		var found = await context.Entities.SingleAsync(row => row.Id == generated);

		await Assert.That(found.Score.GetValueOrDefault().Value).IsEqualTo(42);
	}

	[Test]
	public async Task NullableScalar_GivenNullableProperty_IsAnOptionalColumnWithTheGeneratedConverter()
	{
		await using var connection = await OpenConnectionAsync();
		await using var context = CreateContext(connection);

		var property = context
			.Model.FindEntityType(typeof(CompatibilityEntity))!
			.FindProperty(nameof(CompatibilityEntity.Nickname))!;

		// T? unwraps to T for the converter lookup; the column follows the nullable CLR type.
		await Assert.That(property.IsNullable).IsTrue();
		await Assert
			.That(property.GetValueConverter()?.GetType())
			.IsEqualTo(CompatibilityNickname.EF.Converter.GetType());
		await Assert.That(property.GetRelationalTypeMapping().StoreType).IsEqualTo("TEXT");
	}

	[Test]
	public async Task NullableScalar_GivenNonNullableProperty_RemainsRequired()
	{
		await using var connection = await OpenConnectionAsync();
		await using var context = CreateContext(connection);

		var entityType = context.Model.FindEntityType(typeof(CompatibilityEntity))!;
		var required = entityType.FindProperty(nameof(CompatibilityEntity.RequiredNickname))!;
		var requiredTimestamp = entityType.FindProperty(nameof(CompatibilityEntity.RecordedAt))!;
		var requiredCount = entityType.FindProperty(nameof(CompatibilityEntity.Count))!;

		// Entity Framework Core refuses to mark a non-nullable struct property optional, so a nullable
		// scalar on the non-nullable shape stays required (VO1031 reports it at compile time).
		await Assert.That(required.IsNullable).IsFalse();
		await Assert
			.That(required.GetValueConverter()?.GetType())
			.IsEqualTo(CompatibilityNickname.EF.Converter.GetType());
		await Assert.That(requiredTimestamp.IsNullable).IsFalse();
		await Assert.That(requiredCount.IsNullable).IsFalse();
	}

	[Test]
	public async Task NullableScalar_GivenTwoScalarShape_MapsNullableColumnsWithTheNonNullableConverter()
	{
		await using var connection = await OpenConnectionAsync();
		await using var context = CreateContext(connection);

		var entityType = context.Model.FindEntityType(typeof(CompatibilityEntity))!;
		var preferredName = entityType.FindProperty(nameof(CompatibilityEntity.PreferredName))!;
		var observedAt = entityType.FindProperty(nameof(CompatibilityEntity.ObservedAt))!;
		var optionalQuantity = entityType.FindProperty(nameof(CompatibilityEntity.OptionalQuantity))!;

		await Assert.That(preferredName.IsNullable).IsTrue();
		await Assert
			.That(preferredName.GetValueConverter()?.GetType())
			.IsEqualTo(CompatibilityName.EF.Converter.GetType());
		await Assert.That(observedAt.IsNullable).IsTrue();
		await Assert
			.That(observedAt.GetValueConverter()?.GetType())
			.IsEqualTo(CompatibilityObservedAt.EF.Converter.GetType());
		await Assert.That(optionalQuantity.IsNullable).IsTrue();
		await Assert
			.That(optionalQuantity.GetValueConverter()?.GetType())
			.IsEqualTo(CompatibilityQuantity.EF.Converter.GetType());
	}

	[Test]
	public async Task NullableScalar_GivenNullableValueTypeScalar_IsAnOptionalColumn()
	{
		await using var connection = await OpenConnectionAsync();
		await using var context = CreateContext(connection);

		var score = context
			.Model.FindEntityType(typeof(CompatibilityEntity))!
			.FindProperty(nameof(CompatibilityEntity.Score))!;

		await Assert.That(score.IsNullable).IsTrue();
		await Assert.That(score.GetValueConverter()?.ProviderClrType).IsEqualTo(typeof(int?));
	}

	[Test]
	public async Task NullableScalar_GivenExplicitIsRequired_LeavesTheExplicitConfigurationAlone()
	{
		await using var connection = await OpenConnectionAsync();
		await using var context = CreateContext(connection);

		var configured = context
			.Model.FindEntityType(typeof(CompatibilityEntity))!
			.FindProperty(nameof(CompatibilityEntity.MandatoryNickname))!;

		// The property is a nullable scalar on a T? CLR type, so the registry would normally mark it
		// optional. The explicit IsRequired configured in OnModelCreating must win instead.
		await Assert.That(configured.IsNullable).IsFalse();
	}

	[Test]
	public async Task NullableScalar_GivenNullableProperty_RoundTripsNullAndNullValueAsDatabaseNull()
	{
		await using var connection = await OpenConnectionAsync();
		await using var context = CreateContext(connection);
		await context.Database.EnsureCreatedAsync();

		var withoutValue = CreateEntity("NULL");
		withoutValue.Nickname = null;
		withoutValue.PreferredName = null;
		withoutValue.ObservedAt = null;
		withoutValue.OptionalQuantity = null;

		var explicitNull = CreateEntity("NULLVALUE");
		explicitNull.Nickname = CompatibilityNickname.Null();
		explicitNull.PreferredName = CompatibilityName.Create("  Mixed  ");
		explicitNull.ObservedAt = CompatibilityObservedAt.Null();
		explicitNull.OptionalQuantity = CompatibilityQuantity.Null();

		context.Entities.AddRange(withoutValue, explicitNull);
		await context.SaveChangesAsync();
		var withoutValueId = withoutValue.Id;
		var explicitNullId = explicitNull.Id;
		context.ChangeTracker.Clear();

		var nullRow = await context.Entities.SingleAsync(row => row.Id == withoutValueId);
		var nullValueRow = await context.Entities.SingleAsync(row => row.Id == explicitNullId);

		await Assert.That(nullRow.Nickname).IsNull();
		await Assert.That(nullRow.PreferredName).IsNull();
		await Assert.That(nullRow.ObservedAt).IsNull();
		await Assert.That(nullRow.OptionalQuantity).IsNull();

		// A nullable scalar's Null() is a null provider value, so a T? property reads it back as null.
		await Assert.That(nullValueRow.Nickname).IsNull();
		await Assert.That(nullValueRow.ObservedAt).IsNull();
		await Assert.That(nullValueRow.OptionalQuantity).IsNull();

		// The non-nullable scalar normalises through Create and the nullable property preserves absence.
		await Assert.That(nullValueRow.PreferredName!.Value.Value).IsEqualTo("Mixed");
	}

	[Test]
	public async Task ComplexTypeValueObjectMappedToJsonColumn_RoundTrips()
	{
		// Arrange
		await using var connection = await OpenConnectionAsync();
		await using var context = CreateContext(connection);
		await context.Database.EnsureCreatedAsync();

		var stamp = CreateStamp();
		var entity = CreateEntity("JSON");
		context.Entities.Add(entity);
		await context.SaveChangesAsync();
		context.ChangeTracker.Clear();

		// Act: the JSON column round-trips through the generated converter. A deep predicate over the JSON
		// value is provider-specific, so the row is located without one.
		var found = await context.Entities.SingleAsync();

		// Assert
		await Assert.That(found.Stamp).IsEqualTo(stamp);
		await Assert.That(found.Code).IsEqualTo(CompatibilityCode.Create("JSON"));
	}

	[Test]
	public async Task CompiledModelMembers_AreEmittedOnlyWhenEntityFramework8IsReferenced()
	{
		// The compiled-model members use Entity Framework Core 8's JsonValueReaderWriter, so their presence
		// is the observable difference between the two generations of the API. The design-time model
		// generator reads the member by reflection, which is what this test does.
		var idReaderWriter = CompiledModelReaderWriter(CompatibilityId.EF.Converter);
		var stampReaderWriter = CompiledModelReaderWriter(CompatibilityStamp.EF.Converter);

#if NET8_0
		await Assert.That(idReaderWriter).IsNull();
		await Assert.That(stampReaderWriter).IsNull();
#else
		await Assert.That(idReaderWriter).IsNotNull();
		await Assert.That(stampReaderWriter).IsNotNull();
#endif
	}

#if !NET8_0
	[Test]
	public async Task ComplexTypeValueObject_GivenEntityFramework8OrLater_IsMappedToColumns()
	{
		// Arrange
		await using var connection = await OpenConnectionAsync();
		await using var context = CreateContext(connection);
		await context.Database.EnsureCreatedAsync();

		// Act: the generated registry maps the value object as a complex type, so its members are columns
		// that queries translate against rather than a single JSON value.
		var complexProperty = context
			.Model.FindEntityType(typeof(CompatibilityEntity))!
			.FindComplexProperty(nameof(CompatibilityEntity.Total));
		var matches = await context.Entities.CountAsync(row => row.Total.Amount > 100m);

		// Assert
		await Assert.That(complexProperty).IsNotNull();
		await Assert.That(matches).IsEqualTo(0);
	}

	[Test]
	public async Task ComplexTypeValueObject_GivenEntityFramework8OrLater_RoundTrips()
	{
		// Arrange
		await using var connection = await OpenConnectionAsync();
		await using var context = CreateContext(connection);
		await context.Database.EnsureCreatedAsync();

		var total = CompatibilityMoney.Create(1250.75m, CompatibilityCode.Create("GBP"));
		var entity = CreateEntity("COMPLEX");
		context.Entities.Add(entity);
		await context.SaveChangesAsync();
		context.ChangeTracker.Clear();

		// Act
		var found = await context.Entities.SingleAsync(row => row.Total.Currency == CompatibilityCode.Create("GBP"));

		// Assert
		await Assert.That(found.Total).IsEqualTo(total);
	}
#endif

	/// <summary>
	/// A value object column declared by a base type that is not an entity type is mapped on the entity type
	/// that owns the row. The registry used to configure the property through its declaring type, which added
	/// that type - and its own value object conversion - to the model while the registry was enumerating the
	/// model, so creating a model for an entity with an inherited value object column threw
	/// <c>InvalidOperationException</c>.
	/// </summary>
	[Test]
	public async Task InheritedValueObjectColumn_GivenBaseTypeOutsideTheModel_IsMappedThroughTheEntityType()
	{
		// Arrange
		await using var connection = await OpenConnectionAsync();
		await using var context = CreateContext(connection);

		// Act - creating the model is the operation that used to throw.
		var property = context
			.Model.FindEntityType(typeof(CompatibilityInheritedEntity))!
			.FindProperty(nameof(CompatibilityAuditedEntity.LastChangeCode));
		var baseEntityType = context.Model.FindEntityType(typeof(CompatibilityAuditedEntity));

		// Assert - the base type is not an entity type, and the inherited column keeps its conversion.
		await Assert.That(baseEntityType).IsNull();
		await Assert.That(property).IsNotNull();
		await Assert.That(property!.GetTypeMapping().Converter?.ProviderClrType).IsEqualTo(typeof(string));
	}

	[Test]
	public async Task InheritedValueObjectColumn_GivenRowWithInheritedValue_RoundTrips()
	{
		// Arrange
		await using var connection = await OpenConnectionAsync();
		await using var context = CreateContext(connection);
		await context.Database.EnsureCreatedAsync();

		CompatibilityInheritedEntity entity = new() { LastChangeCode = CompatibilityCode.Create("INHERITED") };
		context.InheritedEntities.Add(entity);

		// Act
		await context.SaveChangesAsync();
		var generated = entity.Id;
		context.ChangeTracker.Clear();
		var found = await context.InheritedEntities.SingleAsync(row => row.Id == generated);

		// Assert
		await Assert.That(found.LastChangeCode).IsEqualTo(CompatibilityCode.Create("INHERITED"));
	}

	static System.Reflection.PropertyInfo? CompiledModelReaderWriter(object converter) =>
		converter.GetType().GetProperty("JsonReaderWriter");

	/// <summary>
	/// Reads the RFC 9562 version nibble from an identifier's bytes. <c>Guid.Version</c> needs net9.0, and
	/// this project deliberately spans earlier frameworks.
	/// </summary>
	static int GuidVersion(Guid value) => (value.ToByteArray(bigEndian: true)[6] & 0xF0) >> 4;

	static CompatibilityStamp CreateStamp() =>
		CompatibilityStamp.Create(new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero), "compatibility");

	static CompatibilityEntity CreateEntity(string code) =>
		new()
		{
			Code = CompatibilityCode.Create(code),
			Score = CompatibilityScore.Create(0),
			RequiredNickname = CompatibilityNickname.Create("required"),
			MandatoryNickname = CompatibilityNickname.Create("mandatory"),
			RecordedAt = CompatibilityRecordedAt.Create(new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero)),
			Count = CompatibilityCount.Create(0),
			Stamp = CreateStamp(),
#if !NET8_0
			Total = CompatibilityMoney.Create(1250.75m, CompatibilityCode.Create("GBP")),
#endif
		};

	static async Task<SqliteConnection> OpenConnectionAsync()
	{
		SqliteConnection connection = new("Data Source=:memory:");
		await connection.OpenAsync();
		return connection;
	}

	static CompatibilityDbContext CreateContext(SqliteConnection connection) =>
		new(new DbContextOptionsBuilder<CompatibilityDbContext>().UseSqlite(connection).Options);
}
