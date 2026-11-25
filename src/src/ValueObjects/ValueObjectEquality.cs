using System.ComponentModel;

namespace Purview.ValueObjects;

/// <summary>
/// Comparison helpers for the equality, hashing and ordering the value object generator emits for a
/// complex value object's collection members. Generated members call these instead of inlining loop
/// semantics, so contents comparison is implemented and tested once.
/// </summary>
/// <remarks>
/// <para>
/// An ordered sequence compares element by element, so a different order is a different value. A set
/// (<see cref="ISet{T}"/> or <see cref="IReadOnlySet{T}"/>) and a dictionary
/// (<see cref="IDictionary{TKey, TValue}"/> or <see cref="IReadOnlyDictionary{TKey, TValue}"/>) are
/// unordered, so their contents compare without regard to order and their hashes combine
/// order-insensitively.
/// </para>
/// <para>
/// Two <see langword="null"/> collections are equal; a <see langword="null"/> collection and an empty
/// one are not. Element comparison and hashing use <see cref="EqualityComparer{T}.Default"/>, so a
/// nested value object keeps its own generated equality. Generated code supplies a structural comparer
/// for a dictionary value that is itself a collection, so that value compares by contents too. A set's or
/// dictionary's own comparer is not observed: using it for equality but
/// <see cref="EqualityComparer{T}.Default"/> for hashing would let equal values produce different hashes.
/// </para>
/// <para>
/// These members exist for generated code. They are public so a consumer's generated members can call
/// them, and hidden from IntelliSense because application code has no reason to.
/// </para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ValueObjectEquality
{
	/// <summary>
	/// Compares two ordered sequences element by element. Two <see langword="null"/> sequences are
	/// equal; a <see langword="null"/> sequence and an empty one are not.
	/// </summary>
	/// <typeparam name="T">The element type.</typeparam>
	/// <param name="left">The first sequence.</param>
	/// <param name="right">The second sequence.</param>
	/// <param name="comparer">The element comparer, or <see langword="null"/> for <see cref="EqualityComparer{T}.Default"/>.</param>
	/// <returns><see langword="true"/> when both sequences contain equal elements in the same order.</returns>
	public static bool SequenceEquals<T>(
		IEnumerable<T>? left,
		IEnumerable<T>? right,
		IEqualityComparer<T>? comparer = null
	)
	{
		if (ReferenceEquals(left, right))
			return true;

		if (left is null || right is null)
			return false;

		comparer ??= EqualityComparer<T>.Default;

		if (left is IReadOnlyList<T> leftList && right is IReadOnlyList<T> rightList)
		{
			if (leftList.Count != rightList.Count)
				return false;

			for (var index = 0; index < leftList.Count; index++)
			{
				if (!comparer.Equals(leftList[index], rightList[index]))
					return false;
			}

			return true;
		}

		if (
			left is IReadOnlyCollection<T> leftCollection
			&& right is IReadOnlyCollection<T> rightCollection
			&& leftCollection.Count != rightCollection.Count
		)
			return false;

		using var leftEnumerator = left.GetEnumerator();
		using var rightEnumerator = right.GetEnumerator();

		while (leftEnumerator.MoveNext())
		{
			if (!rightEnumerator.MoveNext())
				return false;

			if (!comparer.Equals(leftEnumerator.Current, rightEnumerator.Current))
				return false;
		}

		return !rightEnumerator.MoveNext();
	}

	/// <summary>
	/// Returns a hash that agrees with <see cref="SequenceEquals{T}"/>: equal sequences produce the same
	/// hash. A <see langword="null"/> sequence hashes to zero.
	/// </summary>
	/// <typeparam name="T">The element type.</typeparam>
	/// <param name="values">The sequence to hash.</param>
	/// <param name="comparer">The element comparer, or <see langword="null"/> for <see cref="EqualityComparer{T}.Default"/>.</param>
	/// <returns>The sequence's hash.</returns>
	public static int SequenceHash<T>(IEnumerable<T>? values, IEqualityComparer<T>? comparer = null)
	{
		if (values is null)
			return 0;

		comparer ??= EqualityComparer<T>.Default;

		HashCode hash = new();
		if (values is IReadOnlyCollection<T> collection)
			hash.Add(collection.Count);

		foreach (var value in values)
			hash.Add(comparer.GetHashCode(value!));

		return hash.ToHashCode();
	}

	/// <summary>
	/// Orders two ordered sequences: a <see langword="null"/> sequence sorts first, then the shorter
	/// sequence, then the elements through <paramref name="comparer"/>.
	/// </summary>
	/// <typeparam name="T">The element type.</typeparam>
	/// <param name="left">The first sequence.</param>
	/// <param name="right">The second sequence.</param>
	/// <param name="comparer">The element comparer, or <see langword="null"/> for <see cref="Comparer{T}.Default"/>.</param>
	/// <returns>A negative value, zero, or a positive value.</returns>
	public static int SequenceCompare<T>(IEnumerable<T>? left, IEnumerable<T>? right, IComparer<T>? comparer = null)
	{
		if (ReferenceEquals(left, right))
			return 0;

		if (left is null)
			return -1;

		if (right is null)
			return 1;

		comparer ??= Comparer<T>.Default;

		if (left is IReadOnlyList<T> leftList && right is IReadOnlyList<T> rightList)
		{
			var sharedLength = Math.Min(leftList.Count, rightList.Count);
			for (var index = 0; index < sharedLength; index++)
			{
				var comparison = comparer.Compare(leftList[index], rightList[index]);
				if (comparison != 0)
					return comparison;
			}

			return leftList.Count.CompareTo(rightList.Count);
		}

		using var leftEnumerator = left.GetEnumerator();
		using var rightEnumerator = right.GetEnumerator();

		while (true)
		{
			var hasLeft = leftEnumerator.MoveNext();
			var hasRight = rightEnumerator.MoveNext();

			if (!hasLeft)
				return hasRight ? -1 : 0;

			if (!hasRight)
				return 1;

			var comparison = comparer.Compare(leftEnumerator.Current, rightEnumerator.Current);
			if (comparison != 0)
				return comparison;
		}
	}

	/// <summary>
	/// Compares two sets without regard to order. Two <see langword="null"/> sets are equal; a
	/// <see langword="null"/> set and an empty one are not.
	/// </summary>
	/// <typeparam name="T">The element type.</typeparam>
	/// <param name="left">The first set.</param>
	/// <param name="right">The second set.</param>
	/// <param name="comparer">The element comparer, or <see langword="null"/> for <see cref="EqualityComparer{T}.Default"/>.</param>
	/// <returns><see langword="true"/> when both sets contain the same elements.</returns>
	public static bool SetEquals<T>(IEnumerable<T>? left, IEnumerable<T>? right, IEqualityComparer<T>? comparer = null)
	{
		if (ReferenceEquals(left, right))
			return true;

		if (left is null || right is null)
			return false;

		comparer ??= EqualityComparer<T>.Default;

		HashSet<T> contents = new(left, comparer);
		if (contents.Count != Count(right))
			return false;

		foreach (var value in right)
		{
			if (!contents.Contains(value))
				return false;
		}

		return true;
	}

	/// <summary>
	/// Returns an order-insensitive hash that agrees with <see cref="SetEquals{T}"/>: sets containing
	/// the same elements produce the same hash. A <see langword="null"/> set hashes to zero.
	/// </summary>
	/// <typeparam name="T">The element type.</typeparam>
	/// <param name="values">The set to hash.</param>
	/// <param name="comparer">The element comparer, or <see langword="null"/> for <see cref="EqualityComparer{T}.Default"/>.</param>
	/// <returns>The set's hash.</returns>
	public static int SetHash<T>(IEnumerable<T>? values, IEqualityComparer<T>? comparer = null)
	{
		if (values is null)
			return 0;

		comparer ??= EqualityComparer<T>.Default;

		var combined = 0;
		var count = 0;
		foreach (var value in values)
		{
			combined ^= comparer.GetHashCode(value!);
			count++;
		}

		return HashCode.Combine(0x5E7, count, combined);
	}

	/// <summary>
	/// Orders two sets: a <see langword="null"/> set sorts first, then the shorter set, then an
	/// order-insensitive content hash. Equal sets always compare as zero.
	/// </summary>
	/// <typeparam name="T">The element type.</typeparam>
	/// <param name="left">The first set.</param>
	/// <param name="right">The second set.</param>
	/// <param name="comparer">The element comparer, or <see langword="null"/> for <see cref="EqualityComparer{T}.Default"/>.</param>
	/// <returns>A negative value, zero, or a positive value.</returns>
	public static int SetCompare<T>(IEnumerable<T>? left, IEnumerable<T>? right, IEqualityComparer<T>? comparer = null)
	{
		if (ReferenceEquals(left, right))
			return 0;

		if (left is null)
			return -1;

		if (right is null)
			return 1;

		var countComparison = Count(left).CompareTo(Count(right));
		if (countComparison != 0)
			return countComparison;

		if (SetEquals(left, right, comparer))
			return 0;

		return SetHash(left, comparer).CompareTo(SetHash(right, comparer));
	}

	/// <summary>
	/// Compares two dictionaries without regard to order. Two <see langword="null"/> dictionaries are
	/// equal; a <see langword="null"/> dictionary and an empty one are not.
	/// </summary>
	/// <typeparam name="TKey">The key type.</typeparam>
	/// <typeparam name="TValue">The value type.</typeparam>
	/// <param name="left">The first dictionary.</param>
	/// <param name="right">The second dictionary.</param>
	/// <param name="valueComparer">
	/// The comparer for a value that is itself a collection, or <see langword="null"/> for
	/// <see cref="EqualityComparer{T}.Default"/>.
	/// </param>
	/// <returns><see langword="true"/> when both dictionaries contain the same keys mapped to equal values.</returns>
	public static bool DictionaryEquals<TKey, TValue>(
		IEnumerable<KeyValuePair<TKey, TValue>>? left,
		IEnumerable<KeyValuePair<TKey, TValue>>? right,
		IEqualityComparer<TValue>? valueComparer = null
	)
		where TKey : notnull
	{
		if (ReferenceEquals(left, right))
			return true;

		if (left is null || right is null)
			return false;

		valueComparer ??= EqualityComparer<TValue>.Default;

		if (
			left is IReadOnlyCollection<KeyValuePair<TKey, TValue>> leftCollection
			&& right is IReadOnlyCollection<KeyValuePair<TKey, TValue>> rightCollection
			&& leftCollection.Count != rightCollection.Count
		)
			return false;

		Dictionary<TKey, TValue> contents = new();
		foreach (var pair in left)
			contents[pair.Key] = pair.Value;

		var count = 0;
		foreach (var pair in right)
		{
			count++;
			if (!contents.TryGetValue(pair.Key, out var value))
				return false;

			if (!valueComparer.Equals(value, pair.Value))
				return false;
		}

		return contents.Count == count;
	}

	/// <summary>
	/// Returns an order-insensitive hash that agrees with <see cref="DictionaryEquals{TKey, TValue}"/>:
	/// dictionaries containing the same pairs produce the same hash. A <see langword="null"/>
	/// dictionary hashes to zero.
	/// </summary>
	/// <typeparam name="TKey">The key type.</typeparam>
	/// <typeparam name="TValue">The value type.</typeparam>
	/// <param name="values">The dictionary to hash.</param>
	/// <param name="valueComparer">
	/// The comparer for a value that is itself a collection, or <see langword="null"/> for
	/// <see cref="EqualityComparer{T}.Default"/>.
	/// </param>
	/// <returns>The dictionary's hash.</returns>
	public static int DictionaryHash<TKey, TValue>(
		IEnumerable<KeyValuePair<TKey, TValue>>? values,
		IEqualityComparer<TValue>? valueComparer = null
	)
		where TKey : notnull
	{
		if (values is null)
			return 0;

		valueComparer ??= EqualityComparer<TValue>.Default;

		var combined = 0;
		var count = 0;
		foreach (var pair in values)
		{
			combined ^= HashCode.Combine(
				EqualityComparer<TKey>.Default.GetHashCode(pair.Key!),
				valueComparer.GetHashCode(pair.Value!)
			);
			count++;
		}

		return HashCode.Combine(0x0D1C, count, combined);
	}

	/// <summary>
	/// Orders two dictionaries: a <see langword="null"/> dictionary sorts first, then the shorter
	/// dictionary, then an order-insensitive content hash. Equal dictionaries always compare as zero.
	/// </summary>
	/// <typeparam name="TKey">The key type.</typeparam>
	/// <typeparam name="TValue">The value type.</typeparam>
	/// <param name="left">The first dictionary.</param>
	/// <param name="right">The second dictionary.</param>
	/// <param name="valueComparer">
	/// The comparer for a value that is itself a collection, or <see langword="null"/> for
	/// <see cref="EqualityComparer{T}.Default"/>.
	/// </param>
	/// <returns>A negative value, zero, or a positive value.</returns>
	public static int DictionaryCompare<TKey, TValue>(
		IEnumerable<KeyValuePair<TKey, TValue>>? left,
		IEnumerable<KeyValuePair<TKey, TValue>>? right,
		IEqualityComparer<TValue>? valueComparer = null
	)
		where TKey : notnull
	{
		if (ReferenceEquals(left, right))
			return 0;

		if (left is null)
			return -1;

		if (right is null)
			return 1;

		var countComparison = Count(left).CompareTo(Count(right));
		if (countComparison != 0)
			return countComparison;

		if (DictionaryEquals(left, right, valueComparer))
			return 0;

		return DictionaryHash(left, valueComparer).CompareTo(DictionaryHash(right, valueComparer));
	}

	static int Count<T>(IEnumerable<T> values)
	{
		if (values is IReadOnlyCollection<T> collection)
			return collection.Count;

		var count = 0;
		foreach (var _ in values)
			count++;

		return count;
	}
}

/// <summary>
/// An equality and ordering comparer for an ordered sequence, used when a collection member's element is
/// itself a collection. A generated member passes it to <see cref="ValueObjectEquality.SequenceEquals{T}"/>,
/// <see cref="ValueObjectEquality.SequenceHash{T}"/> or <see cref="ValueObjectEquality.SequenceCompare{T}"/>.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <param name="elementEqualityComparer">The comparer for a nested element's equality, or <see langword="null"/>.</param>
/// <param name="elementComparer">The comparer for a nested element's order, or <see langword="null"/>.</param>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ValueObjectSequenceComparer<T>(
	IEqualityComparer<T>? elementEqualityComparer = null,
	IComparer<T>? elementComparer = null
) : IEqualityComparer<IEnumerable<T>>, IComparer<IEnumerable<T>>
{
	/// <inheritdoc />
	public bool Equals(IEnumerable<T>? x, IEnumerable<T>? y) =>
		ValueObjectEquality.SequenceEquals(x, y, elementEqualityComparer);

	/// <inheritdoc />
	public int GetHashCode(IEnumerable<T> obj) => ValueObjectEquality.SequenceHash(obj, elementEqualityComparer);

	/// <inheritdoc />
	public int Compare(IEnumerable<T>? x, IEnumerable<T>? y) =>
		ValueObjectEquality.SequenceCompare(x, y, elementComparer);
}

/// <summary>
/// An equality and ordering comparer for a set, used when a collection member's element is itself a set.
/// A generated member passes it to <see cref="ValueObjectEquality.SetEquals{T}"/>,
/// <see cref="ValueObjectEquality.SetHash{T}"/> or <see cref="ValueObjectEquality.SetCompare{T}"/>.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <param name="elementEqualityComparer">The comparer for a nested element's equality, or <see langword="null"/>.</param>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ValueObjectSetComparer<T>(IEqualityComparer<T>? elementEqualityComparer = null)
	: IEqualityComparer<IEnumerable<T>>,
		IComparer<IEnumerable<T>>
{
	/// <inheritdoc />
	public bool Equals(IEnumerable<T>? x, IEnumerable<T>? y) =>
		ValueObjectEquality.SetEquals(x, y, elementEqualityComparer);

	/// <inheritdoc />
	public int GetHashCode(IEnumerable<T> obj) => ValueObjectEquality.SetHash(obj, elementEqualityComparer);

	/// <inheritdoc />
	public int Compare(IEnumerable<T>? x, IEnumerable<T>? y) =>
		ValueObjectEquality.SetCompare(x, y, elementEqualityComparer);
}

/// <summary>
/// An equality and ordering comparer for a dictionary, used when a collection member's element is itself a
/// dictionary. A generated member passes it to <see cref="ValueObjectEquality.DictionaryEquals{TKey, TValue}"/>,
/// <see cref="ValueObjectEquality.DictionaryHash{TKey, TValue}"/> or
/// <see cref="ValueObjectEquality.DictionaryCompare{TKey, TValue}"/>.
/// </summary>
/// <typeparam name="TKey">The key type.</typeparam>
/// <typeparam name="TValue">The value type.</typeparam>
/// <param name="valueComparer">The comparer for a value that is itself a collection, or <see langword="null"/>.</param>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ValueObjectDictionaryComparer<TKey, TValue>(IEqualityComparer<TValue>? valueComparer = null)
	: IEqualityComparer<IEnumerable<KeyValuePair<TKey, TValue>>>,
		IComparer<IEnumerable<KeyValuePair<TKey, TValue>>>
	where TKey : notnull
{
	/// <inheritdoc />
	public bool Equals(IEnumerable<KeyValuePair<TKey, TValue>>? x, IEnumerable<KeyValuePair<TKey, TValue>>? y) =>
		ValueObjectEquality.DictionaryEquals(x, y, valueComparer);

	/// <inheritdoc />
	public int GetHashCode(IEnumerable<KeyValuePair<TKey, TValue>> obj) =>
		ValueObjectEquality.DictionaryHash(obj, valueComparer);

	/// <inheritdoc />
	public int Compare(IEnumerable<KeyValuePair<TKey, TValue>>? x, IEnumerable<KeyValuePair<TKey, TValue>>? y) =>
		ValueObjectEquality.DictionaryCompare(x, y, valueComparer);
}
