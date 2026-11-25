using System.Collections.Immutable;

namespace Purview.ValueObjects.Equality;

/// <summary>
/// End-to-end equality for a complex value object with a collection member: the generated
/// <c>Equals</c>, <c>==</c>, <c>GetHashCode</c> and <c>CompareTo</c> must compare a collection by its
/// contents rather than by reference, for records and non-records alike.
/// </summary>
public sealed class CollectionEqualityTests
{
	[Test]
	public async Task Record_EqualContents_AreEqualAndHashEqual()
	{
		var first = EqualityRecordProbe.Create("Document", new List<int> { 1, 2, 3 });
		var second = EqualityRecordProbe.Create("Document", new List<int> { 1, 2, 3 });

		await Assert.That(first.Equals(second)).IsTrue();
		await Assert.That(first == second).IsTrue();
		await Assert.That(first != second).IsFalse();
		await Assert.That(first.GetHashCode()).IsEqualTo(second.GetHashCode());
	}

	[Test]
	public async Task Class_EqualContents_AreEqualAndHashEqual()
	{
		var first = EqualityClassProbe.Create("Document", new List<int> { 1, 2, 3 });
		var second = EqualityClassProbe.Create("Document", new List<int> { 1, 2, 3 });

		await Assert.That(first.Equals(second)).IsTrue();
		await Assert.That(first == second).IsTrue();
		await Assert.That(first.GetHashCode()).IsEqualTo(second.GetHashCode());
	}

	[Test]
	public async Task RecordStruct_EqualContents_AreEqualAndHashEqual()
	{
		var first = EqualityRecordStructProbe.Create("Document", new List<int> { 1, 2, 3 });
		var second = EqualityRecordStructProbe.Create("Document", new List<int> { 1, 2, 3 });

		await Assert.That(first.Equals(second)).IsTrue();
		await Assert.That(first == second).IsTrue();
		await Assert.That(first.GetHashCode()).IsEqualTo(second.GetHashCode());
	}

	[Test]
	public async Task Array_EqualContents_AreEqualAndHashEqual()
	{
		var first = EqualityArrayProbe.Create("Document", [1, 2, 3]);
		var second = EqualityArrayProbe.Create("Document", [1, 2, 3]);

		await Assert.That(first.Equals(second)).IsTrue();
		await Assert.That(first.GetHashCode()).IsEqualTo(second.GetHashCode());
	}

	[Test]
	public async Task Sequence_DifferentOrderOrLength_IsNotEqual()
	{
		var first = EqualityRecordProbe.Create("Document", new List<int> { 1, 2, 3 });
		var reordered = EqualityRecordProbe.Create("Document", new List<int> { 3, 2, 1 });
		var shorter = EqualityRecordProbe.Create("Document", new List<int> { 1, 2 });

		await Assert.That(first.Equals(reordered)).IsFalse();
		await Assert.That(first.Equals(shorter)).IsFalse();
	}

	[Test]
	public async Task Sequence_DifferentRuntimeCollectionTypes_AreEqual()
	{
		var list = EqualityRecordProbe.Create("Document", new List<int> { 1, 2, 3 });
		IReadOnlyList<int> array = [1, 2, 3];
		var other = EqualityRecordProbe.Create("Document", array);

		await Assert.That(list.Equals(other)).IsTrue();
		await Assert.That(list.GetHashCode()).IsEqualTo(other.GetHashCode());
	}

	[Test]
	public async Task Nullable_NullEqualsNullAndNullDiffersFromEmpty()
	{
		var first = EqualityNullableProbe.Create("Document", null);
		var second = EqualityNullableProbe.Create("Document", null);
		var empty = EqualityNullableProbe.Create("Document", Array.Empty<int>());

		await Assert.That(first.Equals(second)).IsTrue();
		await Assert.That(first.GetHashCode()).IsEqualTo(second.GetHashCode());
		await Assert.That(first.Equals(empty)).IsFalse();
		await Assert.That(first.GetHashCode() == empty.GetHashCode()).IsFalse();
	}

	[Test]
	public async Task Nullable_NullMember_DoesNotThrow()
	{
		var value = EqualityNullableProbe.Create("Document", null);
		var other = EqualityNullableProbe.Create("Document", null);

		await Assert.That(value.Equals(other)).IsTrue();
		await Assert.That(value.GetHashCode()).IsEqualTo(other.GetHashCode());
	}

	[Test]
	public async Task StringList_EqualContents_AreEqual()
	{
		var first = EqualityStringListProbe.Create("Document", new List<string> { "a", "b" });
		var second = EqualityStringListProbe.Create("Document", new List<string> { "a", "b" });

		await Assert.That(first.Equals(second)).IsTrue();
		await Assert.That(first.GetHashCode()).IsEqualTo(second.GetHashCode());
	}

	[Test]
	public async Task Set_InsertionOrder_DoesNotAffectEqualityOrHash()
	{
		var ascending = EqualitySetProbe.Create("Document", new HashSet<int> { 1, 2, 3 });
		var descending = EqualitySetProbe.Create("Document", new HashSet<int> { 3, 2, 1 });
		var different = EqualitySetProbe.Create("Document", new HashSet<int> { 1, 2, 4 });

		await Assert.That(ascending.Equals(descending)).IsTrue();
		await Assert.That(ascending.GetHashCode()).IsEqualTo(descending.GetHashCode());
		await Assert.That(ascending.Equals(different)).IsFalse();
	}

	[Test]
	public async Task Dictionary_InsertionOrder_DoesNotAffectEqualityOrHash()
	{
		var forward = EqualityDictionaryProbe.Create("Document", new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 });
		var reverse = EqualityDictionaryProbe.Create("Document", new Dictionary<string, int> { ["b"] = 2, ["a"] = 1 });
		var different = EqualityDictionaryProbe.Create(
			"Document",
			new Dictionary<string, int> { ["a"] = 1, ["b"] = 3 }
		);

		await Assert.That(forward.Equals(reverse)).IsTrue();
		await Assert.That(forward.GetHashCode()).IsEqualTo(reverse.GetHashCode());
		await Assert.That(forward.Equals(different)).IsFalse();
	}

	[Test]
	public async Task Dictionary_CollectionValues_CompareByContents()
	{
		var forward = EqualityDictionaryOfListsProbe.Create(
			"Document",
			new Dictionary<string, IReadOnlyList<int>>
			{
				["a"] = new List<int> { 1, 2 },
			}
		);
		IReadOnlyList<int> arrayContents = [1, 2];
		var reverse = EqualityDictionaryOfListsProbe.Create(
			"Document",
			new Dictionary<string, IReadOnlyList<int>> { ["a"] = arrayContents }
		);
		var different = EqualityDictionaryOfListsProbe.Create(
			"Document",
			new Dictionary<string, IReadOnlyList<int>>
			{
				["a"] = new List<int> { 1, 3 },
			}
		);

		await Assert.That(forward.Equals(reverse)).IsTrue();
		await Assert.That(forward.GetHashCode()).IsEqualTo(reverse.GetHashCode());
		await Assert.That(forward.CompareTo(reverse)).IsEqualTo(0);
		await Assert.That(forward.Equals(different)).IsFalse();
	}

	[Test]
	public async Task NestedValueObject_EqualContents_UseTheElementEquality()
	{
		var first = EqualityElementListProbe.Create("Document", [EqualityElementProbe.Create("Element")]);
		var second = EqualityElementListProbe.Create("Document", [EqualityElementProbe.Create("Element")]);

		await Assert.That(first.Equals(second)).IsTrue();
		await Assert.That(first.GetHashCode()).IsEqualTo(second.GetHashCode());
	}

	[Test]
	public async Task NestedCollections_CompareByContentsAtEveryLevel()
	{
		IReadOnlyList<int>[] firstItems =
		[
			[1, 2],
			[3],
		];
		IReadOnlyList<int>[] secondItems = [new List<int> { 1, 2 }, new List<int> { 3 }];
		IReadOnlyList<int>[] differentItems =
		[
			[1, 2],
			[3, 4],
		];

		var first = EqualityNestedListProbe.Create("Document", firstItems);
		var second = EqualityNestedListProbe.Create("Document", secondItems);
		var different = EqualityNestedListProbe.Create("Document", differentItems);

		await Assert.That(first.Equals(second)).IsTrue();
		await Assert.That(first.GetHashCode()).IsEqualTo(second.GetHashCode());
		await Assert.That(first.CompareTo(second)).IsEqualTo(0);
		await Assert.That(first.Equals(different)).IsFalse();
	}

	[Test]
	public async Task ImmutableArray_EqualContents_AreEqualAndHashEqual()
	{
		var first = EqualityImmutableArrayProbe.Create("Document", ImmutableArray.Create(1, 2, 3));
		var second = EqualityImmutableArrayProbe.Create("Document", ImmutableArray.Create(1, 2, 3));

		await Assert.That(first.Equals(second)).IsTrue();
		await Assert.That(first.GetHashCode()).IsEqualTo(second.GetHashCode());
	}

	[Test]
	public async Task SetAndDictionary_UseTheGeneratedHashCode()
	{
		var first = EqualityRecordProbe.Create("Document", new List<int> { 1, 2, 3 });
		var second = EqualityRecordProbe.Create("Document", new List<int> { 1, 2, 3 });

		HashSet<EqualityRecordProbe> set = [first, second];
		Dictionary<EqualityRecordProbe, int> dictionary = new() { [first] = 1, [second] = 2 };

		await Assert.That(set.Count).IsEqualTo(1);
		await Assert.That(dictionary.Count).IsEqualTo(1);
	}

	[Test]
	public async Task Comparable_CollectionMember_HasDefinedOrder()
	{
		var value = EqualityComparableProbe.Create("Document", new List<int> { 1, 2 });
		var equal = EqualityComparableProbe.Create("Document", new List<int> { 1, 2 });
		var later = EqualityComparableProbe.Create("Document", new List<int> { 1, 3 });
		var longer = EqualityComparableProbe.Create("Document", new List<int> { 1, 2, 3 });

		await Assert.That(value.CompareTo(equal)).IsEqualTo(0);
		await Assert.That(value.CompareTo(later) < 0).IsTrue();
		await Assert.That(value.CompareTo(longer) < 0).IsTrue();
		await Assert.That(value.CompareTo(null) > 0).IsTrue();
	}
}
