namespace Purview.ValueObjects.SourceGenerator.Generators;

/// <summary>
/// Tests for the generated <c>Null()</c> factory. It is the explicit unknown value for a nullable scalar and
/// mirrors <c>Empty</c>: it constructs through <c>Hydrate(null)</c>, so it never validates or normalizes.
/// </summary>
public sealed class ScalarNullFactoryTests : ValueObjectSourceGeneratorTestBase
{
	[Test]
	public async Task NullFactory_GivenNullableValueType_ReturnsNull(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
			[Purview.ValueObjects.Serialization.Scalar<int?>]
			public readonly partial record struct OptionalQuantity { }

			[Purview.ValueObjects.Serialization.Scalar<System.DateTimeOffset>(Nullable = true)]
			public readonly partial record struct OptionalTimestamp { }

			public static class Harness
			{
				public static bool QuantityIsNull() => OptionalQuantity.Null().Value is null;

				public static bool TimestampIsNull() => OptionalTimestamp.Null().Value is null;
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

		var quantityIsNull = (bool)harness.GetMethod("QuantityIsNull")!.Invoke(null, null)!;
		var timestampIsNull = (bool)harness.GetMethod("TimestampIsNull")!.Invoke(null, null)!;

		await Assert.That(quantityIsNull).IsTrue();
		await Assert.That(timestampIsNull).IsTrue();
	}

	[Test]
	public async Task NullFactory_GivenNullableReferenceType_ReturnsNull(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
			[Purview.ValueObjects.Serialization.Scalar<string>(Nullable = true)]
			public readonly partial record struct Nickname { }

			[Purview.ValueObjects.Serialization.Scalar]
			public readonly partial record struct ManualNickname
			{
				public string? Value { get; init; }
			}

			public static class Harness
			{
				public static bool AutomaticIsNull() => Nickname.Null().Value is null;

				public static bool ManualIsNull() => ManualNickname.Null().Value is null;
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

		var automaticIsNull = (bool)harness.GetMethod("AutomaticIsNull")!.Invoke(null, null)!;
		var manualIsNull = (bool)harness.GetMethod("ManualIsNull")!.Invoke(null, null)!;

		await Assert.That(automaticIsNull).IsTrue();
		await Assert.That(manualIsNull).IsTrue();
	}

	[Test]
	public async Task NullFactory_GivenNonNullableScalar_IsNotGenerated(CancellationToken cancellationToken)
	{
		// A non-nullable value type's default (Guid.Empty, 0, DateTimeOffset.MinValue) is a real value, not
		// the unknown one; a non-nullable reference scalar rejects null. Neither gets a Null factory.
		const string source = """
			namespace Testing
			{
			[Purview.ValueObjects.Serialization.Scalar<System.Guid>]
			public readonly partial record struct InstallationId { }

			[Purview.ValueObjects.Serialization.Scalar]
			public readonly partial record struct RequiredName
			{
				public string Value { get; init; }
			}
			}
			""";

		using var result = await GenerateAsync(
			source,
			ValueObjectsGeneratorTestOptions.Default.Compile(),
			cancellationToken
		);

		await Assert.That(result.Generated().GetRecord("InstallationId", "Testing").HasMethod("Null")).IsFalse();
		await Assert.That(result.Generated().GetRecord("RequiredName", "Testing").HasMethod("Null")).IsFalse();
	}

	[Test]
	public async Task NullFactory_GivenAuthorDeclaredNull_IsNotDuplicated(CancellationToken cancellationToken)
	{
		// The generated partial cannot edit the consumer's copy, so a duplicate member would be CS0111. The
		// author's factory wins and the generated one is suppressed.
		const string source = """
			namespace Testing
			{
			[Purview.ValueObjects.Serialization.Scalar<int?>]
			public readonly partial record struct OptionalQuantity
			{
				public static OptionalQuantity Null() => Hydrate(null);
			}

			public static class Harness
			{
				public static bool IsNull() => OptionalQuantity.Null().Value is null;
			}
			}
			""";

		using var result = await GenerateAsync(
			source,
			ValueObjectsGeneratorTestOptions.Default.Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();

		var optionalQuantity = assembly!.GetType("Testing.OptionalQuantity")!;
		var nullMethods = optionalQuantity
			.GetMethods()
			.Where(method => method.Name == "Null" && method.GetParameters().Length == 0)
			.ToArray();
		var harness = assembly.GetType("Testing.Harness")!;
		var isNull = (bool)harness.GetMethod("IsNull")!.Invoke(null, null)!;

		await Assert.That(nullMethods.Length).IsEqualTo(1);
		await Assert.That(isNull).IsTrue();
	}
}
