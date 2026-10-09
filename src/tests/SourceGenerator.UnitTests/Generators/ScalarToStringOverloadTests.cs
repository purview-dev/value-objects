namespace Purview.ValueObjects.SourceGenerator.Generators;

/// <summary>
/// Covers the per-type mirroring of the underlying value's format-only <c>ToString(string? format)</c>
/// overload. When the wrapped type declares it (for example <c>Guid.ToString(string? format)</c>), the
/// generated value object forwards to it; when it does not (<c>string</c>), no such overload is emitted.
/// </summary>
public sealed class ScalarToStringOverloadTests : ValueObjectSourceGeneratorTestBase
{
	[Test]
	public async Task ScalarToString_GivenGuid_ForwardsTheFormatOnlyOverload(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
			[Purview.ValueObjects.Serialization.Scalar<System.Guid>]
			public readonly partial record struct InstallationId { }

			public static class InstallationIdHarness
			{
				public static string FormatN(System.Guid id) => InstallationId.Create(id).ToString("N");

				public static string FormatD(System.Guid id) => InstallationId.Create(id).ToString("D");
			}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsGeneratorTestOptions.Default.Compile(), cancellationToken);

		var generated = result.Generated().GetRecord("InstallationId", "Testing").Node.ToString();
		await Assert.That(generated).Contains("string? format");

		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harnessType = assembly.GetType("Testing.InstallationIdHarness")!;
		var id = System.Guid.NewGuid();

		var formatN = (string)harnessType.GetMethod("FormatN")!.Invoke(null, [id])!;
		var formatD = (string)harnessType.GetMethod("FormatD")!.Invoke(null, [id])!;

		await Assert.That(formatN).IsEqualTo(id.ToString("N"));
		await Assert.That(formatD).IsEqualTo(id.ToString("D"));
	}

	[Test]
	public async Task ScalarToString_GivenEnum_ForwardsTheInheritedFormatOnlyOverload(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
			public enum Severity
			{
				Low = 1,
				High = 2,
			}

			[Purview.ValueObjects.Serialization.Scalar<Severity>]
			public readonly partial record struct Priority { }

			public static class PriorityHarness
			{
				public static string FormatName() => Priority.Create(Severity.High).ToString("G");

				public static string FormatNumber() => Priority.Create(Severity.High).ToString("D");
			}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsGeneratorTestOptions.Default.Compile(), cancellationToken);
		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harnessType = assembly.GetType("Testing.PriorityHarness")!;

		var formatName = (string)harnessType.GetMethod("FormatName")!.Invoke(null, null)!;
		var formatNumber = (string)harnessType.GetMethod("FormatNumber")!.Invoke(null, null)!;

		await Assert.That(formatName).IsEqualTo("High");
		await Assert.That(formatNumber).IsEqualTo("2");
	}

	[Test]
	public async Task ScalarToString_GivenNullableValueType_ForwardsTheFormatOnlyOverload(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
			[Purview.ValueObjects.Serialization.Scalar<int?>]
			public readonly partial record struct OptionalQuantity { }

			public static class OptionalQuantityHarness
			{
				public static string FormatNull() => OptionalQuantity.Hydrate(null).ToString("N0");

				public static string FormatValue() => OptionalQuantity.Hydrate(42).ToString("N0");
			}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsGeneratorTestOptions.Default.Compile(), cancellationToken);
		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harnessType = assembly.GetType("Testing.OptionalQuantityHarness")!;

		var formatNull = (string)harnessType.GetMethod("FormatNull")!.Invoke(null, null)!;
		var formatValue = (string)harnessType.GetMethod("FormatValue")!.Invoke(null, null)!;

		await Assert.That(formatNull).IsEqualTo(string.Empty);
		await Assert.That(formatValue).IsEqualTo("42");
	}

	[Test]
	public async Task ScalarToString_GivenString_DoesNotEmitAFormatOnlyOverload(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
			[Purview.ValueObjects.Serialization.Scalar]
			public readonly partial record struct EmailAddress
			{
				public string Value { get; }
			}

			public static class EmailAddressHarness
			{
				public static string ToStringDefault() => EmailAddress.Create("a@b.com").ToString();
			}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsGeneratorTestOptions.Default.Compile(), cancellationToken);

		var generated = result.Generated().GetRecord("EmailAddress", "Testing").Node.ToString();
		await Assert.That(generated).DoesNotContain("string? format");

		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harnessType = assembly.GetType("Testing.EmailAddressHarness")!;

		var toStringDefault = (string)harnessType.GetMethod("ToStringDefault")!.Invoke(null, null)!;

		await Assert.That(toStringDefault).IsEqualTo("a@b.com");
	}

	[Test]
	public async Task ScalarToString_GivenAuthorDeclaredFormatOnlyOverload_IsNotDuplicated(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
			[Purview.ValueObjects.Serialization.Scalar]
			public readonly partial record struct Code
			{
				public decimal Value { get; }

				public string ToString(string? format) => "custom:" + format;
			}

			public static class CodeHarness
			{
				public static string Format() => Code.Create(1m).ToString("N2");
			}
			}
			""";

		// The assembly is only non-null when the generated partial did not collide with the author's method.
		var result = await GenerateAsync(source, ValueObjectsGeneratorTestOptions.Default.Compile(), cancellationToken);
		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harnessType = assembly.GetType("Testing.CodeHarness")!;

		var formatted = (string)harnessType.GetMethod("Format")!.Invoke(null, null)!;

		await Assert.That(formatted).IsEqualTo("custom:N2");
	}
}
