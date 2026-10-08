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

/// <summary>
/// The manual <c>[Scalar]</c> form with a nullable reference property and the generated converter
/// disabled, so <see cref="ScalarJsonConverterFactory"/> has to accept JSON null for it.
/// </summary>
[Scalar(
	GenerateJsonConverter = false,
	GenerateComparable = false,
	GenerateComparisonOperators = false,
	GenerateEnumProperties = false,
	GenerateImplicitFromPrimitive = false,
	GenerateImplicitToPrimitive = false,
	GenerateEmpty = false
)]
readonly partial record struct NullableEmailAddress
{
	public string? Value { get; }

	NullableEmailAddress(string? value) => Value = value;

	public static NullableEmailAddress Create(string? value) => new(value);

	public static NullableEmailAddress Hydrate(string? value) => new(value);
}

/// <summary>
/// Uses the automatic <c>[Scalar&lt;T&gt;]</c> form (the generator declares the property) with the
/// generated JSON converter disabled, so <see cref="ScalarJsonConverterFactory"/> has to recognise the
/// generic attribute and resolve the property it declares.
/// </summary>
[Scalar<string>(
	GenerateJsonConverter = false,
	GenerateComparable = false,
	GenerateComparisonOperators = false,
	GenerateEnumProperties = false,
	GenerateImplicitFromPrimitive = false,
	GenerateImplicitToPrimitive = false,
	GenerateEmpty = false
)]
readonly partial record struct AutomaticEmailAddress
{
	AutomaticEmailAddress(string value) => Value = value;

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase")]
	public static AutomaticEmailAddress Create(string value) => new(value.Trim().ToLowerInvariant());

	public static AutomaticEmailAddress Hydrate(string value) => new(value);
}

/// <summary>
/// Holds a strict scalar, so a deserialization failure can be observed with a JSON path.
/// </summary>
sealed class StrictEmailHolder
{
	public StrictEmailAddress Email { get; set; }
}
