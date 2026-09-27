using System.Data.SqlTypes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Purview.ValueObjects.Serialization;

/// <summary>
/// Tests for the SQL Server key ordering strategy: identifiers generated for unset keys ascend in SQL
/// Server's <c>uniqueidentifier</c> ordering, and the generated helper's timestamp readers and range
/// bounds agree with that layout.
/// <c>SqlGuid</c> is used as the ordering oracle because it is SQL Server's own comparison implementation.
/// </summary>
public sealed class SqlServerKeyOrderingTests
{
	const int SameInstantSampleCount = 100;

	[Test]
	public async Task SqlGuid_ComparesTheTrailingSixBytesFirst()
	{
		// Arrange: two values whose leading and trailing bytes disagree, so the two orderings differ.
		var leadingHigh = new byte[16];
		leadingHigh[0] = 0xFF;
		var trailingHigh = new byte[16];
		trailingHigh[15] = 0x01;

		// Assert: plain byte order is decided by the leading byte...
		await Assert
			.That(new Guid(leadingHigh, bigEndian: true).CompareTo(new Guid(trailingHigh, bigEndian: true)))
			.IsGreaterThan(0);

		// ...while SQL Server's ordering is decided by the trailing byte, which is why the generated
		// SQL Server strategy writes the timestamp there.
		await Assert.That(new SqlGuid(trailingHigh).CompareTo(new SqlGuid(leadingHigh))).IsGreaterThan(0);
	}

	[Test]
	public async Task UnsetValueObjectKey_GivenSqlServerOrdering_AscendsInSqlGuidOrder()
	{
		// Arrange
		await using SqliteConnection connection = new("Data Source=:memory:");
		await connection.OpenAsync();

		await using TestEFSqlServerOrderingDbContext context = new(
			new DbContextOptionsBuilder<TestEFSqlServerOrderingDbContext>().UseSqlite(connection).Options
		);
		await context.Database.EnsureCreatedAsync();

		EFAutoKeyEntity first = new();
		context.AutoKeys.Add(first);
		await context.SaveChangesAsync();

		// The timestamp has millisecond resolution, so leave a gap before the second insert.
		await Task.Delay(20);

		EFAutoKeyEntity second = new();
		context.AutoKeys.Add(second);
		await context.SaveChangesAsync();

		// Assert: the later key is greater in SQL Server's ordering, so a clustered index stays in
		// insertion order. The value is a version 4 UUID because its timestamp occupies the trailing bytes.
		await Assert.That(new SqlGuid(second.Id.Value).CompareTo(new SqlGuid(first.Id.Value))).IsGreaterThan(0);
		await Assert.That(first.Id.Value.Version).IsEqualTo(4);
	}

	[Test]
	public async Task NewSqlServerGuid_AscendsForLaterCreationTimes()
	{
		// Arrange
		DateTimeOffset start = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
		var previous = ValueObjectSequentialGuid.NewSqlServerGuid(start);

		// Act / Assert
		for (var offset = 1; offset <= 50; offset++)
		{
			var timestamp = start.AddMilliseconds(offset);
			var next = ValueObjectSequentialGuid.NewSqlServerGuid(timestamp);

			await Assert.That(new SqlGuid(next).CompareTo(new SqlGuid(previous))).IsGreaterThan(0);
			await Assert.That(ValueObjectSequentialGuid.TryGetSqlServerTimestamp(next, out var recovered)).IsTrue();
			await Assert.That(recovered).IsEqualTo(timestamp);
			previous = next;
		}
	}

	[Test]
	public async Task NewSqlServerGuid_GivenOneCreationTime_StaysWithinItsRangeBounds()
	{
		// Arrange
		DateTimeOffset timestamp = new(2026, 7, 8, 9, 10, 11, 500, TimeSpan.Zero);
		var min = ValueObjectSequentialGuid.MinSqlServerGuidFor(timestamp);
		var max = ValueObjectSequentialGuid.MaxSqlServerGuidFor(timestamp);
		var nextMillisecond = ValueObjectSequentialGuid.MinSqlServerGuidFor(timestamp.AddMilliseconds(1));

		// Act / Assert: every value created at that time falls inside the bounds...
		for (var iteration = 0; iteration < SameInstantSampleCount; iteration++)
		{
			var value = ValueObjectSequentialGuid.NewSqlServerGuid(timestamp);

			await Assert.That(new SqlGuid(value).CompareTo(new SqlGuid(min))).IsGreaterThanOrEqualTo(0);
			await Assert.That(new SqlGuid(value).CompareTo(new SqlGuid(max))).IsLessThanOrEqualTo(0);
			await Assert.That(ValueObjectSequentialGuid.TryGetSqlServerTimestamp(value, out _)).IsTrue();
		}

		// ...and the bounds of adjacent milliseconds do not overlap, so a range query is exact.
		await Assert.That(new SqlGuid(nextMillisecond).CompareTo(new SqlGuid(max))).IsGreaterThan(0);
	}

	[Test]
	public async Task ValueObjectSequentialGuid_GivenAValueFromTheOtherLayout_RefusesToReadATimestamp()
	{
		// Arrange
		DateTimeOffset timestamp = new(2026, 5, 6, 7, 8, 9, TimeSpan.Zero);
		var version7 = ValueObjectSequentialGuid.NewGuid(timestamp);
		var sqlServerOrdered = ValueObjectSequentialGuid.NewSqlServerGuid(timestamp);

		// Assert: each reader accepts only the layout its creator produces, so neither reports a
		// timestamp read from the wrong byte offset.
		await Assert.That(ValueObjectSequentialGuid.TryGetTimestamp(version7, out var recovered)).IsTrue();
		await Assert.That(recovered).IsEqualTo(timestamp);
		await Assert.That(ValueObjectSequentialGuid.TryGetTimestamp(sqlServerOrdered, out _)).IsFalse();
		await Assert
			.That(ValueObjectSequentialGuid.TryGetSqlServerTimestamp(sqlServerOrdered, out var sqlServerRecovered))
			.IsTrue();
		await Assert.That(sqlServerRecovered).IsEqualTo(timestamp);
	}

	[Test]
	public async Task SqlServerOrderedKey_IsPersistedAndQueryableByValueObject()
	{
		// Arrange
		await using SqliteConnection connection = new("Data Source=:memory:");
		await connection.OpenAsync();

		await using TestEFSqlServerOrderingDbContext context = new(
			new DbContextOptionsBuilder<TestEFSqlServerOrderingDbContext>().UseSqlite(connection).Options
		);
		await context.Database.EnsureCreatedAsync();

		EFAutoKeyEntity entity = new();
		context.AutoKeys.Add(entity);
		await context.SaveChangesAsync();
		var generated = entity.Id;
		context.ChangeTracker.Clear();

		// Act
		var found = await context.AutoKeys.SingleAsync(row => row.Id == generated);

		// Assert
		await Assert.That(found.Id).IsEqualTo(generated);
	}
}
