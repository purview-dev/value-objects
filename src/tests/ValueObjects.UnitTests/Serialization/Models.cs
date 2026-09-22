namespace Purview.ValueObjects.Serialization;

[Scalar(
	GenerateJsonConverter = false,
	GenerateComparable = false,
	GenerateComparisonOperators = false,
	GenerateEnumProperties = false,
	GenerateImplicitFromPrimitive = false,
	GenerateImplicitToPrimitive = false,
	GenerateEmpty = false
)]
readonly partial record struct HydratingEmailAddress
{
	public string Value { get; }

	HydratingEmailAddress(string value) => Value = value;

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase")]
	public static HydratingEmailAddress Create(string value)
	{
		return value.Contains('@', StringComparison.Ordinal)
			? new(value.Trim().ToLowerInvariant())
			: throw new ArgumentException("Invalid email address.", nameof(value));
	}

	public static HydratingEmailAddress Hydrate(string value) => new(value);
}

[Scalar(
	DeserializationMode = ValueObjectDeserializationMode.Strict,
	GenerateJsonConverter = false,
	GenerateComparable = false,
	GenerateComparisonOperators = false,
	GenerateEnumProperties = false,
	GenerateImplicitFromPrimitive = false,
	GenerateImplicitToPrimitive = false,
	GenerateEmpty = false
)]
readonly partial record struct StrictEmailAddress
{
	public string Value { get; }

	StrictEmailAddress(string value) => Value = value;

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase")]
	public static StrictEmailAddress Create(string value)
	{
		return value.Contains('@', StringComparison.Ordinal)
			? new(value.Trim().ToLowerInvariant())
			: throw new ArgumentException("Invalid email address.", nameof(value));
	}

	public static StrictEmailAddress Hydrate(string value) => new(value);
}

[Scalar(
	GenerateJsonConverter = false,
	GenerateComparable = false,
	GenerateComparisonOperators = false,
	GenerateEnumProperties = false,
	GenerateImplicitFromPrimitive = false,
	GenerateImplicitToPrimitive = false,
	GenerateEmpty = false
)]
readonly partial record struct CustomerId
{
	public Guid Value { get; }

	CustomerId(Guid value) => Value = value;

	public static CustomerId Create(Guid value)
	{
		return value == Guid.Empty ? throw new ArgumentException("Value cannot be empty.", nameof(value)) : new(value);
	}

	public static CustomerId Hydrate(Guid value) => new(value);
}
