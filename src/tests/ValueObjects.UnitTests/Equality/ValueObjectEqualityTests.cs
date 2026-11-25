namespace Purview.ValueObjects.Equality;

/// <summary>
/// The contract of the runtime helpers the generated collection equality calls: ordered sequences,
/// unordered sets and dictionaries, null handling, and a paired hash.
/// </summary>
public sealed class ValueObjectEqualityTests
{
	[Test]
	public async Task SequenceEquals_EqualContents_AreEqualAcrossRuntimeTypes()
	{
		IReadOnlyList<int> list = new List<int> { 1, 2, 3 };
		int[] array = [1, 2, 3];

		await Assert.That(ValueObjectEquality.SequenceEquals<int>(list, array)).IsTrue();
	}

	[Test]
	public async Task SequenceEquals_DifferentOrderOrLength_IsNotEqual()
	{
		await Assert.That(ValueObjectEquality.SequenceEquals<int>([1, 2, 3], [3, 2, 1])).IsFalse();
		await Assert.That(ValueObjectEquality.SequenceEquals<int>([1, 2, 3], [1, 2])).IsFalse();
	}

	[Test]
	public async Task SequenceEquals_NullAndEmpty_AreNotEqual()
	{
		await Assert.That(ValueObjectEquality.SequenceEquals<int>(null, null)).IsTrue();
		await Assert.That(ValueObjectEquality.SequenceEquals<int>(null, [])).IsFalse();
		await Assert.That(ValueObjectEquality.SequenceEquals<int>([], null)).IsFalse();
	}

	[Test]
	public async Task SequenceHash_AgreesWithSequenceEquals()
	{
		IReadOnlyList<int> list = new List<int> { 1, 2, 3 };
		int[] array = [1, 2, 3];

		await Assert
			.That(ValueObjectEquality.SequenceHash<int>(list))
			.IsEqualTo(ValueObjectEquality.SequenceHash<int>(array));
		await Assert.That(ValueObjectEquality.SequenceHash<int>(null)).IsEqualTo(0);
	}

	[Test]
	public async Task SequenceCompare_OrdersByCountThenElements()
	{
		await Assert.That(ValueObjectEquality.SequenceCompare<int>([1, 2], [1, 2])).IsEqualTo(0);
		await Assert.That(ValueObjectEquality.SequenceCompare<int>([1, 2], [1, 3]) < 0).IsTrue();
		await Assert.That(ValueObjectEquality.SequenceCompare<int>([1, 2], [1, 2, 3]) < 0).IsTrue();
		await Assert.That(ValueObjectEquality.SequenceCompare<int>(null, []) < 0).IsTrue();
		await Assert.That(ValueObjectEquality.SequenceCompare<int>(null, null)).IsEqualTo(0);
	}

	[Test]
	public async Task SetEquals_InsertionOrder_IsIgnored()
	{
		HashSet<int> ascending = [1, 2, 3];
		HashSet<int> descending = [3, 2, 1];

		await Assert.That(ValueObjectEquality.SetEquals<int>(ascending, descending)).IsTrue();
		await Assert.That(ValueObjectEquality.SetEquals<int>(ascending, new HashSet<int> { 1, 2, 4 })).IsFalse();
	}

	[Test]
	public async Task SetHash_InsertionOrder_IsIgnored()
	{
		HashSet<int> ascending = [1, 2, 3];
		HashSet<int> descending = [3, 2, 1];

		await Assert
			.That(ValueObjectEquality.SetHash<int>(ascending))
			.IsEqualTo(ValueObjectEquality.SetHash<int>(descending));
	}

	[Test]
	public async Task SetCompare_EqualSets_AreZero()
	{
		HashSet<int> ascending = [1, 2, 3];
		HashSet<int> descending = [3, 2, 1];

		await Assert.That(ValueObjectEquality.SetCompare<int>(ascending, descending)).IsEqualTo(0);
		await Assert.That(ValueObjectEquality.SetCompare<int>(ascending, new HashSet<int> { 1, 2, 4 }) != 0).IsTrue();
	}

	[Test]
	public async Task DictionaryEquals_InsertionOrder_IsIgnored()
	{
		Dictionary<string, int> forward = new() { ["a"] = 1, ["b"] = 2 };
		Dictionary<string, int> reverse = new() { ["b"] = 2, ["a"] = 1 };

		await Assert.That(ValueObjectEquality.DictionaryEquals(forward, reverse)).IsTrue();
		await Assert
			.That(ValueObjectEquality.DictionaryEquals(forward, new Dictionary<string, int> { ["a"] = 1 }))
			.IsFalse();
	}

	[Test]
	public async Task DictionaryHash_InsertionOrder_IsIgnored()
	{
		Dictionary<string, int> forward = new() { ["a"] = 1, ["b"] = 2 };
		Dictionary<string, int> reverse = new() { ["b"] = 2, ["a"] = 1 };

		await Assert
			.That(ValueObjectEquality.DictionaryHash(forward))
			.IsEqualTo(ValueObjectEquality.DictionaryHash(reverse));
	}

	[Test]
	public async Task Dictionary_CollectionValues_UseTheValueComparer()
	{
		Dictionary<string, IReadOnlyList<int>> forward = new()
		{
			["a"] = new List<int> { 1, 2 },
		};
		Dictionary<string, IReadOnlyList<int>> reverse = new()
		{
			["a"] = new List<int> { 1, 2 },
		};

		// The default value comparer is reference equality for a list.
		await Assert.That(ValueObjectEquality.DictionaryEquals(forward, reverse)).IsFalse();

		ValueObjectSequenceComparer<int> valueComparer = new();
		await Assert.That(ValueObjectEquality.DictionaryEquals(forward, reverse, valueComparer)).IsTrue();
		await Assert
			.That(ValueObjectEquality.DictionaryHash(forward, valueComparer))
			.IsEqualTo(ValueObjectEquality.DictionaryHash(reverse, valueComparer));
		await Assert.That(ValueObjectEquality.DictionaryCompare(forward, reverse, valueComparer)).IsEqualTo(0);
	}

	[Test]
	public async Task DictionaryCompare_EqualDictionaries_AreZero()
	{
		Dictionary<string, int> forward = new() { ["a"] = 1, ["b"] = 2 };
		Dictionary<string, int> reverse = new() { ["b"] = 2, ["a"] = 1 };

		await Assert.That(ValueObjectEquality.DictionaryCompare(forward, reverse)).IsEqualTo(0);
	}

	[Test]
	public async Task SequenceEquals_NestedCollections_UseTheStructuralComparer()
	{
		IReadOnlyList<IReadOnlyList<int>> first =
		[
			[1, 2],
			[3],
		];
		IReadOnlyList<IReadOnlyList<int>> second = [new List<int> { 1, 2 }, new List<int> { 3 }];

		await Assert
			.That(
				ValueObjectEquality.SequenceEquals<IReadOnlyList<int>>(
					first,
					second,
					new ValueObjectSequenceComparer<int>()
				)
			)
			.IsTrue();
	}

	[Test]
	public async Task SetEquals_NestedCollections_UseTheStructuralComparer()
	{
		HashSet<IReadOnlyList<int>> first = new(
			new[]
			{
				new List<int> { 1, 2 },
			},
			new RefComparer()
		);
		HashSet<IReadOnlyList<int>> second = new(
			new[]
			{
				new List<int> { 1, 2 },
			},
			new RefComparer()
		);

		await Assert
			.That(ValueObjectEquality.SetEquals<IReadOnlyList<int>>(first, second, new ValueObjectSetComparer<int>()))
			.IsTrue();
	}

	sealed class RefComparer : IEqualityComparer<IReadOnlyList<int>>
	{
		public bool Equals(IReadOnlyList<int>? x, IReadOnlyList<int>? y) => ReferenceEquals(x, y);

		public int GetHashCode(IReadOnlyList<int> obj) =>
			System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
	}
}
