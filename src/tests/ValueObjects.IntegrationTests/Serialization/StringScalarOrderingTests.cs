namespace Purview.ValueObjects.Serialization;

/// <summary>
/// Covers the agreement between equality and ordering for a <see cref="string"/>-backed scalar.
/// </summary>
/// <remarks>
/// Equality and <c>GetHashCode</c> are generated with <c>EqualityComparer&lt;string&gt;.Default</c>, which is
/// ordinal. <c>CompareTo</c> used <c>Comparer&lt;string&gt;.Default</c>, which orders by the current culture.
/// That broke the <see cref="IComparable{T}"/> contract — <c>CompareTo == 0</c> no longer implied
/// <c>Equals</c> — so <c>SortedSet</c>, <c>SortedDictionary</c> and <c>List.BinarySearch</c>, which use
/// <c>CompareTo</c> for identity, disagreed with the type's own notion of equality. It also made ordering
/// depend on the thread's culture.
/// </remarks>
public class StringScalarOrderingTests
{
	[Test]
	public async Task CompareTo_AgreesWithEquality()
	{
		// Arrange — "a" vs "A" is where ordinal and culture-aware comparison disagree.
		var lower = OrderingCode.Create("abc");
		var upper = OrderingCode.Create("ABC");

		// Act
		var comparesEqual = lower.CompareTo(upper) == 0;
		var equals = lower.Equals(upper);

		// Assert — whatever the answer, the two must agree. Under the culture-aware comparer they did not.
		await Assert.That(comparesEqual).IsEqualTo(equals);
	}

	[Test]
	public async Task CompareTo_UsesOrdinalOrdering()
	{
		// Arrange — ordinal puts uppercase before lowercase, because 'A' (0x41) < 'a' (0x61). A
		// culture-aware comparison does not.
		var upper = OrderingCode.Create("ABC");
		var lower = OrderingCode.Create("abc");

		// Act / Assert
		await Assert.That(upper.CompareTo(lower)).IsLessThan(0);
	}

	[Test]
	public async Task SortedSet_DoesNotCollapseValuesTheTypeConsidersDistinct()
	{
		// Arrange — SortedSet uses CompareTo for identity. Under the culture-aware comparer these two
		// compared equal while Equals said otherwise, so one was silently dropped.
		var lower = OrderingCode.Create("abc");
		var upper = OrderingCode.Create("ABC");

		// Act
		SortedSet<OrderingCode> set = [lower, upper];

		// Assert
		await Assert.That(lower.Equals(upper)).IsFalse();
		await Assert.That(set.Count).IsEqualTo(2);
	}

	[Test]
	public async Task CompareTo_IsIndependentOfTheThreadCulture()
	{
		// Arrange — a culture-aware comparison can reorder these; ordinal cannot.
		var first = OrderingCode.Create("cote");
		var second = OrderingCode.Create("cote\u0301");

		var original = System.Globalization.CultureInfo.CurrentCulture;

		try
		{
			// Act
			System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("en-US");
			var underInvariantish = Math.Sign(first.CompareTo(second));

			System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
			var underFrench = Math.Sign(first.CompareTo(second));

			// Assert
			await Assert.That(underFrench).IsEqualTo(underInvariantish);
		}
		finally
		{
			System.Globalization.CultureInfo.CurrentCulture = original;
		}
	}

	[Test]
	public async Task CompareTo_AgainstThePrimitive_AlsoUsesOrdinalOrdering()
	{
		// The primitive overload is what the relational operators and the self overload delegate to.
		var upper = OrderingCode.Create("ABC");

		await Assert.That(upper.CompareTo("abc")).IsLessThan(0);
	}
}

/// <summary>
/// A string scalar with no normalization, so case is preserved and ordering is observable.
/// </summary>
/// <remarks>
/// The other string fixtures lowercase in <c>OnNormalize</c>, which makes case-based ordering untestable
/// through them.
/// </remarks>
[Scalar]
public readonly partial record struct OrderingCode
{
	public string Value { get; }
}
