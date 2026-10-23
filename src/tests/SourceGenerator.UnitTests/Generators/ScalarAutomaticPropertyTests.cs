using System.Reflection;

namespace Purview.ValueObjects.SourceGenerator.Generators;

/// <summary>
/// Tests for the automatic <c>[Scalar&lt;T&gt;]</c> / <c>[Scalar(typeof(T))]</c> forms, which declare the
/// underlying property themselves rather than requiring the author to. These compile the generated output,
/// because the property is emitted into a partial the consumer cannot edit.
/// </summary>
public sealed class ScalarAutomaticPropertyTests : ValueObjectSourceGeneratorTestBase
{
	[Test]
	public async Task ScalarGeneration_GivenGenericAttribute_GeneratesValueProperty(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
			[Purview.ValueObjects.Serialization.Scalar<System.Guid>]
			public readonly partial record struct InstallationId
			{
				static partial void OnValidate(System.Guid value)
				{
					if (value == System.Guid.Empty)
						throw new System.ArgumentException("required", nameof(value));
				}
			}

			public static class Harness
			{
				public static System.Guid RoundTrip(System.Guid id) => InstallationId.Create(id).Value;

				public static bool RejectsEmpty()
				{
					try
					{
						InstallationId.Create(System.Guid.Empty);
						return false;
					}
					catch (System.ArgumentException)
					{
						return true;
					}
				}
			}
			}
			""";

		using var result = await GenerateAsync(
			source,
			ValueObjectsGeneratorTestOptions.Default.Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();

		var harnessType = assembly.GetType("Testing.Harness")!;
		var id = Guid.NewGuid();
		var roundTrip = (Guid)harnessType.GetMethod("RoundTrip")!.Invoke(null, [id])!;
		var rejectsEmpty = (bool)harnessType.GetMethod("RejectsEmpty")!.Invoke(null, null)!;

		await Assert.That(roundTrip).IsEqualTo(id);
		await Assert.That(rejectsEmpty).IsTrue();
	}

	[Test]
	public async Task ScalarGeneration_GivenNullableReferenceType_GeneratesNullableProperty(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
			[Purview.ValueObjects.Serialization.Scalar<string>(Nullable = true)]
			public readonly partial record struct Handle { }

			public static class Harness
			{
				public static string? RoundTrip(string? value) => Handle.Hydrate(value).Value;
			}
			}
			""";

		using var result = await GenerateAsync(
			source,
			ValueObjectsGeneratorTestOptions.Default.Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();

		var harnessType = assembly.GetType("Testing.Harness")!;
		var roundTripNull = (string?)harnessType.GetMethod("RoundTrip")!.Invoke(null, [null])!;

		await Assert.That(roundTripNull).IsNull();
	}

	[Test]
	public async Task ScalarGeneration_GivenCustomPropertyName_GeneratesNamedPropertyAndForwardsValue(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
			[Purview.ValueObjects.Serialization.Scalar<decimal>("Amount")]
			public readonly partial record struct Money { }

			public static class Harness
			{
				public static decimal ReadNamed() => Money.Create(12.5m).Amount;

				public static decimal ReadThroughInterface()
				{
					global::Purview.ValueObjects.IScalarValueObject<Money, decimal> asInterface = Money.Create(12.5m);

					return asInterface.Value;
				}
			}
			}
			""";

		using var result = await GenerateAsync(
			source,
			ValueObjectsGeneratorTestOptions.Default.Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();

		var harnessType = assembly.GetType("Testing.Harness")!;
		var named = (decimal)harnessType.GetMethod("ReadNamed")!.Invoke(null, null)!;
		var throughInterface = (decimal)harnessType.GetMethod("ReadThroughInterface")!.Invoke(null, null)!;

		await Assert.That(named).IsEqualTo(12.5m);
		await Assert.That(throughInterface).IsEqualTo(12.5m);
	}

	[Test]
	public async Task ScalarGeneration_GivenNullableString_RoundTripsNullAndValidatesNullOrNonWhitespace(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
			[Purview.ValueObjects.Serialization.Scalar<string>(Nullable = true)]
			public readonly partial record struct Nickname
			{
				static partial void OnValidate(string? value)
				{
					if (value is not null && string.IsNullOrWhiteSpace(value))
						throw new System.ArgumentException("Value must be null or non-whitespace.", nameof(value));
				}
			}

			public static class Harness
			{
				public static string? Deserialize(string json) =>
					System.Text.Json.JsonSerializer.Deserialize<Nickname>(json)!.Value;

				public static string SerializeNull() =>
					System.Text.Json.JsonSerializer.Serialize(Nickname.Hydrate(null));

				public static bool CreateAcceptsNull() => Nickname.Create(null).Value is null;

				public static bool CreateRejectsWhitespace()
				{
					try
					{
						Nickname.Create("   ");
						return false;
					}
					catch (System.ArgumentException)
					{
						return true;
					}
				}
			}
			}
			""";

		using var result = await GenerateAsync(
			source,
			ValueObjectsGeneratorTestOptions.Default.Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();

		var harnessType = assembly.GetType("Testing.Harness")!;
		var deserialized = harnessType.GetMethod("Deserialize")!.Invoke(null, ["null"]);
		var serialized = (string)harnessType.GetMethod("SerializeNull")!.Invoke(null, null)!;
		var acceptsNull = (bool)harnessType.GetMethod("CreateAcceptsNull")!.Invoke(null, null)!;
		var rejectsWhitespace = (bool)harnessType.GetMethod("CreateRejectsWhitespace")!.Invoke(null, null)!;

		await Assert.That(deserialized).IsNull();
		await Assert.That(serialized).IsEqualTo("null");
		await Assert.That(acceptsNull).IsTrue();
		await Assert.That(rejectsWhitespace).IsTrue();
	}

	[Test]
	public async Task ScalarGeneration_GivenNullableValueType_RoundTripsNull(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
			[Purview.ValueObjects.Serialization.Scalar<int?>]
			public readonly partial record struct Score { }

			public static class Harness
			{
				public static int? Deserialize(string json) =>
					System.Text.Json.JsonSerializer.Deserialize<Score>(json)!.Value;

				public static string SerializeNull() =>
					System.Text.Json.JsonSerializer.Serialize(Score.Hydrate(null));
			}
			}
			""";

		using var result = await GenerateAsync(
			source,
			ValueObjectsGeneratorTestOptions.Default.Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();

		var harnessType = assembly.GetType("Testing.Harness")!;
		var deserialized = harnessType.GetMethod("Deserialize")!.Invoke(null, ["null"]);
		var serialized = (string)harnessType.GetMethod("SerializeNull")!.Invoke(null, null)!;

		await Assert.That(deserialized).IsNull();
		await Assert.That(serialized).IsEqualTo("null");
	}

	[Test]
	public async Task ScalarGeneration_GivenNonNullableString_StillRejectsNull(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
			[Purview.ValueObjects.Serialization.Scalar<string>]
			public readonly partial record struct RequiredName { }

			public static class Harness
			{
				public static bool RejectsNull()
				{
					try
					{
						System.Text.Json.JsonSerializer.Deserialize<RequiredName>("null");
						return false;
					}
					catch (System.Text.Json.JsonException)
					{
						return true;
					}
				}
			}
			}
			""";

		using var result = await GenerateAsync(
			source,
			ValueObjectsGeneratorTestOptions.Default.Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();

		var harnessType = assembly.GetType("Testing.Harness")!;
		var rejectsNull = (bool)harnessType.GetMethod("RejectsNull")!.Invoke(null, null)!;

		await Assert.That(rejectsNull).IsTrue();
	}

	[Test]
	public async Task ScalarGeneration_GivenTypeofArgument_GeneratesValueProperty(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
			[Purview.ValueObjects.Serialization.Scalar(typeof(System.Guid))]
			public readonly partial record struct InstallationId { }

			public static class Harness
			{
				public static System.Guid RoundTrip(System.Guid id) => InstallationId.Create(id).Value;
			}
			}
			""";

		using var result = await GenerateAsync(
			source,
			ValueObjectsGeneratorTestOptions.Default.Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();

		var harnessType = assembly.GetType("Testing.Harness")!;
		var id = Guid.NewGuid();
		var roundTrip = (Guid)harnessType.GetMethod("RoundTrip")!.Invoke(null, [id])!;

		await Assert.That(roundTrip).IsEqualTo(id);
	}

	[Test]
	public async Task ScalarGeneration_GivenTypeofNullableString_RoundTripsNull(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
			[Purview.ValueObjects.Serialization.Scalar(typeof(string), Nullable = true)]
			public readonly partial record struct Nickname { }

			public static class Harness
			{
				public static string? Deserialize(string json) =>
					System.Text.Json.JsonSerializer.Deserialize<Nickname>(json)!.Value;

				public static string SerializeNull() =>
					System.Text.Json.JsonSerializer.Serialize(Nickname.Hydrate(null));
			}
			}
			""";

		using var result = await GenerateAsync(
			source,
			ValueObjectsGeneratorTestOptions.Default.Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();

		var harnessType = assembly.GetType("Testing.Harness")!;
		var deserialized = harnessType.GetMethod("Deserialize")!.Invoke(null, ["null"]);
		var serialized = (string)harnessType.GetMethod("SerializeNull")!.Invoke(null, null)!;

		await Assert.That(deserialized).IsNull();
		await Assert.That(serialized).IsEqualTo("null");
	}

	[Test]
	public async Task ScalarGeneration_GivenTypeofNullableValueType_RoundTripsNull(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
			[Purview.ValueObjects.Serialization.Scalar(typeof(int?))]
			public readonly partial record struct Score { }

			public static class Harness
			{
				public static int? Deserialize(string json) =>
					System.Text.Json.JsonSerializer.Deserialize<Score>(json)!.Value;
			}
			}
			""";

		using var result = await GenerateAsync(
			source,
			ValueObjectsGeneratorTestOptions.Default.Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();

		var harnessType = assembly.GetType("Testing.Harness")!;
		var deserialized = harnessType.GetMethod("Deserialize")!.Invoke(null, ["null"]);

		await Assert.That(deserialized).IsNull();
	}

	[Test]
	public async Task ScalarGeneration_GivenTypeofWithCustomPropertyName_GeneratesNamedProperty(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
			[Purview.ValueObjects.Serialization.Scalar(typeof(decimal), "Amount")]
			public readonly partial record struct Money { }

			public static class Harness
			{
				public static decimal Read() => Money.Create(12.5m).Amount;
			}
			}
			""";

		using var result = await GenerateAsync(
			source,
			ValueObjectsGeneratorTestOptions.Default.Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();

		var harnessType = assembly.GetType("Testing.Harness")!;
		var read = (decimal)harnessType.GetMethod("Read")!.Invoke(null, null)!;

		await Assert.That(read).IsEqualTo(12.5m);
	}

	[Test]
	public async Task ScalarGeneration_GivenGenerateJsonConverterFalse_DoesNotEmitConverter(
		CancellationToken cancellationToken
	)
	{
		// The options are read for both automatic spellings.
		const string source = """
			namespace Testing
			{
			[Purview.ValueObjects.Serialization.Scalar<System.Guid>(GenerateJsonConverter = false)]
			public readonly partial record struct NoConverterId { }

			[Purview.ValueObjects.Serialization.Scalar(typeof(System.Guid), GenerateJsonConverter = false)]
			public readonly partial record struct TypeofNoConverterId { }
			}
			""";

		using var result = await GenerateAsync(
			source,
			ValueObjectsGeneratorTestOptions.Default.Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();

		foreach (var typeName in new[] { "Testing.NoConverterId", "Testing.TypeofNoConverterId" })
		{
			var attribute = assembly
				.GetType(typeName)!
				.GetCustomAttribute<System.Text.Json.Serialization.JsonConverterAttribute>();
			await Assert.That(attribute).IsNull();
		}
	}

	[Test]
	public async Task ScalarGeneration_GivenGenerateEmptyFalse_DoesNotEmitEmpty(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
			[Purview.ValueObjects.Serialization.Scalar<System.Guid>(GenerateEmpty = false)]
			public readonly partial record struct NoEmptyId { }

			[Purview.ValueObjects.Serialization.Scalar(typeof(System.Guid), GenerateEmpty = false)]
			public readonly partial record struct TypeofNoEmptyId { }
			}
			""";

		using var result = await GenerateAsync(
			source,
			ValueObjectsGeneratorTestOptions.Default.Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();

		foreach (var typeName in new[] { "Testing.NoEmptyId", "Testing.TypeofNoEmptyId" })
		{
			var empty = assembly.GetType(typeName)!.GetProperty("Empty", BindingFlags.Public | BindingFlags.Static);
			await Assert.That(empty).IsNull();
		}
	}

	[Test]
	public async Task ScalarGeneration_GivenStrictMode_RevalidatesOnDeserialize(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
			[Purview.ValueObjects.Serialization.Scalar<System.Guid>(
				DeserializationMode = Purview.ValueObjects.Serialization.ValueObjectDeserializationMode.Strict
			)]
			public readonly partial record struct StrictId
			{
				static partial void OnValidate(System.Guid value)
				{
					if (value == System.Guid.Empty)
						throw new System.ArgumentException("required", nameof(value));
				}
			}

			public static class Harness
			{
				public static bool RejectsEmpty()
				{
					try
					{
						System.Text.Json.JsonSerializer.Deserialize<StrictId>(
							"\"00000000-0000-0000-0000-000000000000\"");
						return false;
					}
					catch (System.ArgumentException)
					{
						// Strict re-validates through Create, which throws the OnValidate exception.
						return true;
					}
				}
			}
			}
			""";

		using var result = await GenerateAsync(
			source,
			ValueObjectsGeneratorTestOptions.Default.Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();

		var harnessType = assembly.GetType("Testing.Harness")!;
		var rejects = (bool)harnessType.GetMethod("RejectsEmpty")!.Invoke(null, null)!;

		await Assert.That(rejects).IsTrue();
	}

	[Test]
	public async Task ScalarGeneration_GivenAssemblyDefaults_AppliesAndTypeOverrideWins(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			using Purview.ValueObjects.Serialization;

			[assembly: ValueObjectDefaults(GenerateEmpty = false)]

			namespace Testing
			{
			[Purview.ValueObjects.Serialization.Scalar<System.Guid>]
			public readonly partial record struct DefaultedId { }

			[Purview.ValueObjects.Serialization.Scalar<System.Guid>(GenerateEmpty = true)]
			public readonly partial record struct OverriddenId { }
			}
			""";

		using var result = await GenerateAsync(
			source,
			ValueObjectsGeneratorTestOptions.Default.Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();

		var defaultedEmpty = assembly
			.GetType("Testing.DefaultedId")!
			.GetProperty("Empty", BindingFlags.Public | BindingFlags.Static);
		var overriddenEmpty = assembly
			.GetType("Testing.OverriddenId")!
			.GetProperty("Empty", BindingFlags.Public | BindingFlags.Static);

		await Assert.That(defaultedEmpty).IsNull();
		await Assert.That(overriddenEmpty).IsNotNull();
	}

	[Test]
	public async Task ScalarGeneration_GivenNullableValueTypeOption_GeneratesNullableProperty(
		CancellationToken cancellationToken
	)
	{
		// Nullable value types were the one shape Nullable did not apply to, so [Scalar<DateTimeOffset>(
		// Nullable = true)] declared a non-nullable DateTimeOffset property. The option now wraps the value
		// type (and is idempotent for a type that is already nullable), matching the direct T? spellings.
		const string source = """
			namespace Testing
			{
			[Purview.ValueObjects.Serialization.Scalar<System.DateTimeOffset>(Nullable = true)]
			public readonly partial record struct OptionalTimestamp { }

			[Purview.ValueObjects.Serialization.Scalar(typeof(System.DateTimeOffset), Nullable = true)]
			public readonly partial record struct TypeofOptionalTimestamp { }

			[Purview.ValueObjects.Serialization.Scalar<System.DateTimeOffset?>(Nullable = true)]
			public readonly partial record struct AlreadyNullableTimestamp { }

			public static class Harness
			{
				public static System.Type GenericPropertyType() =>
					typeof(OptionalTimestamp).GetProperty("Value")!.PropertyType;

				public static System.Type TypeofPropertyType() =>
					typeof(TypeofOptionalTimestamp).GetProperty("Value")!.PropertyType;

				public static System.Type AlreadyNullablePropertyType() =>
					typeof(AlreadyNullableTimestamp).GetProperty("Value")!.PropertyType;

				public static System.DateTimeOffset? CreateNull() => OptionalTimestamp.Create(null).Value;

				public static System.DateTimeOffset? NullValue() => OptionalTimestamp.Null().Value;

				public static string SerializeNull() =>
					System.Text.Json.JsonSerializer.Serialize(OptionalTimestamp.Null());

				public static System.DateTimeOffset? DeserializeNull() =>
					System.Text.Json.JsonSerializer.Deserialize<OptionalTimestamp>("null").Value;
			}
			}
			""";

		using var result = await GenerateAsync(
			source,
			ValueObjectsGeneratorTestOptions.Default.Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harness = assembly!.GetType("Testing.Harness")!;

		var genericPropertyType = (Type)harness.GetMethod("GenericPropertyType")!.Invoke(null, null)!;
		var typeofPropertyType = (Type)harness.GetMethod("TypeofPropertyType")!.Invoke(null, null)!;
		var alreadyNullablePropertyType = (Type)harness.GetMethod("AlreadyNullablePropertyType")!.Invoke(null, null)!;
		var createNull = harness.GetMethod("CreateNull")!.Invoke(null, null);
		var nullValue = harness.GetMethod("NullValue")!.Invoke(null, null);
		var serialized = (string)harness.GetMethod("SerializeNull")!.Invoke(null, null)!;
		var deserialized = harness.GetMethod("DeserializeNull")!.Invoke(null, null);

		await Assert.That(genericPropertyType).IsEqualTo(typeof(DateTimeOffset?));
		await Assert.That(typeofPropertyType).IsEqualTo(typeof(DateTimeOffset?));
		await Assert.That(alreadyNullablePropertyType).IsEqualTo(typeof(DateTimeOffset?));
		await Assert.That(createNull).IsNull();
		await Assert.That(nullValue).IsNull();
		await Assert.That(serialized).IsEqualTo("null");
		await Assert.That(deserialized).IsNull();
	}

	[Test]
	public async Task ScalarGeneration_GivenNullableValueTypeOption_MatchesManualNullableForm(
		CancellationToken cancellationToken
	)
	{
		// The two declarations differ only in the type name and in who declares the property. The generated
		// members must therefore be equivalent: same nullable property, same factories, same JSON contract.
		const string source = """
			namespace Testing
			{
			[Purview.ValueObjects.Serialization.Scalar<System.DateTimeOffset>(Nullable = true)]
			public readonly partial record struct OptionalByAttribute { }

			[Purview.ValueObjects.Serialization.Scalar]
			public readonly partial record struct OptionalByProperty
			{
				public System.DateTimeOffset? Value { get; init; }
			}

			public static class Harness
			{
				public static bool SamePropertyType() =>
					typeof(OptionalByAttribute).GetProperty("Value")!.PropertyType
					== typeof(OptionalByProperty).GetProperty("Value")!.PropertyType;

				public static bool BothHaveNullableCreate() =>
					typeof(OptionalByAttribute).GetMethod("Create", [typeof(System.DateTimeOffset?)]) is not null
					&& typeof(OptionalByProperty).GetMethod("Create", [typeof(System.DateTimeOffset?)]) is not null;

				public static bool BothHaveNullFactory() =>
					typeof(OptionalByAttribute).GetMethod("Null", System.Type.EmptyTypes) is not null
					&& typeof(OptionalByProperty).GetMethod("Null", System.Type.EmptyTypes) is not null;

				public static string SerializeAttributeNull() =>
					System.Text.Json.JsonSerializer.Serialize(OptionalByAttribute.Null());

				public static string SerializePropertyNull() =>
					System.Text.Json.JsonSerializer.Serialize(OptionalByProperty.Null());
			}
			}
			""";

		using var result = await GenerateAsync(
			source,
			ValueObjectsGeneratorTestOptions.Default.Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harness = assembly!.GetType("Testing.Harness")!;

		var samePropertyType = (bool)harness.GetMethod("SamePropertyType")!.Invoke(null, null)!;
		var bothHaveNullableCreate = (bool)harness.GetMethod("BothHaveNullableCreate")!.Invoke(null, null)!;
		var bothHaveNullFactory = (bool)harness.GetMethod("BothHaveNullFactory")!.Invoke(null, null)!;
		var attributeJson = (string)harness.GetMethod("SerializeAttributeNull")!.Invoke(null, null)!;
		var propertyJson = (string)harness.GetMethod("SerializePropertyNull")!.Invoke(null, null)!;

		await Assert.That(samePropertyType).IsTrue();
		await Assert.That(bothHaveNullableCreate).IsTrue();
		await Assert.That(bothHaveNullFactory).IsTrue();
		await Assert.That(attributeJson).IsEqualTo("null");
		await Assert.That(propertyJson).IsEqualTo(attributeJson);
	}
}
