using System.Collections.Immutable;
using Purview.ValueObjects.Serialization;

namespace Purview.ValueObjects.Equality;

// Each fixture opts out of Entity Framework complex-type mapping: the test project references Entity
// Framework Core, and a collection member cannot be mapped as a complex type (VO1018). The equality
// behaviour under test is independent of persistence.
[ValueObject(EFMapping = EntityFrameworkMapping.None, GenerateEFComparer = false)]
public sealed partial record EqualityRecordProbe
{
	public string Head { get; }

	public IReadOnlyList<int> Items { get; }
}

[ValueObject(EFMapping = EntityFrameworkMapping.None, GenerateEFComparer = false)]
public sealed partial class EqualityClassProbe
{
	public string Head { get; }

	public IReadOnlyList<int> Items { get; }
}

[ValueObject(EFMapping = EntityFrameworkMapping.None, GenerateEFComparer = false)]
public readonly partial record struct EqualityRecordStructProbe
{
	public string Head { get; }

	public IReadOnlyList<int> Items { get; }
}

[ValueObject(EFMapping = EntityFrameworkMapping.None, GenerateEFComparer = false)]
public sealed partial record EqualityArrayProbe
{
	public string Head { get; }

	public int[] Items { get; }
}

[ValueObject(EFMapping = EntityFrameworkMapping.None, GenerateEFComparer = false)]
public sealed partial record EqualityStringListProbe
{
	public string Text { get; }

	public List<string> Items { get; }
}

[ValueObject(EFMapping = EntityFrameworkMapping.None, GenerateEFComparer = false)]
public sealed partial record EqualitySetProbe
{
	public string Head { get; }

	public HashSet<int> Items { get; }
}

[ValueObject(EFMapping = EntityFrameworkMapping.None, GenerateEFComparer = false)]
public sealed partial record EqualityDictionaryProbe
{
	public string Head { get; }

	public Dictionary<string, int> Items { get; }
}

[ValueObject(EFMapping = EntityFrameworkMapping.None, GenerateEFComparer = false)]
public sealed partial record EqualityDictionaryOfListsProbe
{
	public string Head { get; }

	public Dictionary<string, IReadOnlyList<int>> Items { get; }
}

[ValueObject(EFMapping = EntityFrameworkMapping.None, GenerateEFComparer = false)]
public sealed partial record EqualityNullableProbe
{
	public string Head { get; }

	public IReadOnlyList<int>? Items { get; }
}

[ValueObject(EFMapping = EntityFrameworkMapping.None, GenerateEFComparer = false)]
public sealed partial record EqualityElementProbe
{
	public string Head { get; }
}

[ValueObject(EFMapping = EntityFrameworkMapping.None, GenerateEFComparer = false)]
public sealed partial record EqualityElementListProbe
{
	public string Head { get; }

	public IReadOnlyList<EqualityElementProbe> Items { get; }
}

[ValueObject(EFMapping = EntityFrameworkMapping.None, GenerateEFComparer = false)]
public sealed partial record EqualityNestedListProbe
{
	public string Head { get; }

	public IReadOnlyList<IReadOnlyList<int>> Items { get; }
}

[ValueObject(EFMapping = EntityFrameworkMapping.None, GenerateEFComparer = false)]
public sealed partial record EqualityImmutableArrayProbe
{
	public string Head { get; }

	public ImmutableArray<int> Items { get; }
}

[ValueObject(GenerateComparable = true, EFMapping = EntityFrameworkMapping.None, GenerateEFComparer = false)]
public sealed partial record EqualityComparableProbe
{
	public string Head { get; }

	public IReadOnlyList<int> Items { get; }
}
