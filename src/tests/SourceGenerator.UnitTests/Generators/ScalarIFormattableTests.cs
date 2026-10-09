namespace Purview.ValueObjects.SourceGenerator.Generators;

/// <summary>
/// Covers the generated scalar's <c>IFormattable</c> support: a value object wrapping an
/// <see cref="IFormattable"/> value exposes the same formatting options as that value (format string and
/// provider), while a scalar whose underlying type does not implement <see cref="IFormattable"/> is
/// unchanged.
/// </summary>
public sealed class ScalarIFormattableTests : ValueObjectSourceGeneratorTestBase
{
	[Test]
	public async Task ScalarIFormattable_GivenFormattableValue_ForwardsFormatAndProvider(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
			[Purview.ValueObjects.Serialization.Scalar]
			public readonly partial record struct Money
			{
				public decimal Value { get; }
			}

			public static class MoneyFormattingHarness
			{
				public static bool IsFormattable() => Money.Create(1m) is System.IFormattable;

				public static string Format(string format) =>
					Money.Create(1234.5m).ToString(format, System.Globalization.CultureInfo.InvariantCulture);

				public static string CompositeFormat() =>
					string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:N2}", Money.Create(1234.5m));
			}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsGeneratorTestOptions.Default.Compile(), cancellationToken);

		var generated = result.Generated().GetRecord("Money", "Testing").Node.ToString();
		await Assert.That(generated).Contains("global::System.IFormattable");
		await Assert.That(generated).Contains("string? format");
		await Assert.That(generated).Contains("global::System.IFormatProvider? formatProvider");

		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harnessType = assembly.GetType("Testing.MoneyFormattingHarness")!;

		var isFormattable = (bool)harnessType.GetMethod("IsFormattable")!.Invoke(null, null)!;
		var formatted = (string)harnessType.GetMethod("Format")!.Invoke(null, ["N2"])!;
		var composite = (string)harnessType.GetMethod("CompositeFormat")!.Invoke(null, null)!;

		await Assert.That(isFormattable).IsTrue();
		await Assert
			.That(formatted)
			.IsEqualTo(1234.5m.ToString("N2", System.Globalization.CultureInfo.InvariantCulture));
		await Assert.That(composite).IsEqualTo(formatted);
	}

	[Test]
	public async Task ScalarIFormattable_GivenNonFormattableValue_DoesNotImplementIFormattable(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
			[Purview.ValueObjects.Serialization.Scalar]
			public readonly partial record struct EmailAddress
			{
				public string Value { get; }
			}

			public static class EmailFormattingHarness
			{
				public static bool IsFormattable() => EmailAddress.Create("a@b.com") is System.IFormattable;
			}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsGeneratorTestOptions.Default.Compile(), cancellationToken);

		var generated = result.Generated().GetRecord("EmailAddress", "Testing").Node.ToString();
		await Assert.That(generated).DoesNotContain("IFormattable");

		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harnessType = assembly.GetType("Testing.EmailFormattingHarness")!;

		var isFormattable = (bool)harnessType.GetMethod("IsFormattable")!.Invoke(null, null)!;

		await Assert.That(isFormattable).IsFalse();
	}

	[Test]
	public async Task ScalarIFormattable_GivenNullableValueType_FormatsNullAsEmpty(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
			[Purview.ValueObjects.Serialization.Scalar<int?>]
			public readonly partial record struct OptionalQuantity { }

			public static class OptionalQuantityHarness
			{
				public static string FormatNull() =>
					OptionalQuantity.Hydrate(null).ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

				public static string FormatValue() =>
					OptionalQuantity.Hydrate(42).ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
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
	public async Task ScalarIFormattable_GivenNullableReferenceType_FormatsNullAsEmpty(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
			public sealed class MoneyText : System.IFormattable
			{
				public MoneyText(string text) => Text = text;

				public string Text { get; }

				public string ToString(string? format, System.IFormatProvider? formatProvider) =>
					format is null ? Text : Text + ":" + format;

				public override string ToString() => Text;
			}

			[Purview.ValueObjects.Serialization.Scalar<MoneyText>(Nullable = true)]
			public readonly partial record struct MoneyLabel { }

			public static class MoneyLabelHarness
			{
				public static string FormatNull() =>
					MoneyLabel.Hydrate(null).ToString("X", System.Globalization.CultureInfo.InvariantCulture);

				public static string FormatValue() =>
					MoneyLabel.Hydrate(new MoneyText("v")).ToString("X", System.Globalization.CultureInfo.InvariantCulture);
			}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsGeneratorTestOptions.Default.Compile(), cancellationToken);
		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harnessType = assembly.GetType("Testing.MoneyLabelHarness")!;

		var formatNull = (string)harnessType.GetMethod("FormatNull")!.Invoke(null, null)!;
		var formatValue = (string)harnessType.GetMethod("FormatValue")!.Invoke(null, null)!;

		await Assert.That(formatNull).IsEqualTo(string.Empty);
		await Assert.That(formatValue).IsEqualTo("v:X");
	}

	[Test]
	public async Task ScalarIFormattable_GivenEnumValue_FormatsThroughTheUnderlyingEnum(
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
				public static string FormatName() =>
					Priority.Hydrate(Severity.High).ToString("G", System.Globalization.CultureInfo.InvariantCulture);

				public static string FormatNumber() =>
					Priority.Hydrate(Severity.High).ToString("D", System.Globalization.CultureInfo.InvariantCulture);
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
	public async Task ScalarIFormattable_GivenAuthorDeclaredOverload_IsNotDuplicated(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
			[Purview.ValueObjects.Serialization.Scalar]
			public readonly partial record struct Money
			{
				public decimal Value { get; }

				public string ToString(string? format, System.IFormatProvider? formatProvider) => "custom";
			}

			public static class AuthorFormattingHarness
			{
				public static string Format() =>
					Money.Create(1234.5m).ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
			}
			}
			""";

		// The assembly is only non-null when the generated partial did not collide with the author's
		// method (CS0111), which is the actual assertion.
		var result = await GenerateAsync(source, ValueObjectsGeneratorTestOptions.Default.Compile(), cancellationToken);
		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harnessType = assembly.GetType("Testing.AuthorFormattingHarness")!;

		var formatted = (string)harnessType.GetMethod("Format")!.Invoke(null, null)!;

		await Assert.That(formatted).IsEqualTo("custom");
	}
}
