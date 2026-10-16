namespace Purview.ValueObjects.SourceGenerator.Generators;

/// <summary>
/// Covers the per-type mirroring of the underlying value's standard interfaces: equatable, span
/// formatting, and parsing. The generated value object should behave like the type it wraps in equality,
/// formatting, and parsing contexts.
/// </summary>
public sealed class ScalarInterfaceMirroringTests : ValueObjectSourceGeneratorTestBase
{
	[Test]
	public async Task ScalarMirroring_GivenGuid_ImplementsEquatableSpanFormattableAndParsable(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
			[Purview.ValueObjects.Serialization.Scalar<System.Guid>]
			public readonly partial record struct InstallationId { }

			public static class InstallationIdHarness
			{
				public static bool IsValueEquatable() =>
					InstallationId.Create(System.Guid.NewGuid()) is System.IEquatable<System.Guid>;

				public static bool IsSpanFormattable() =>
					InstallationId.Create(System.Guid.NewGuid()) is System.ISpanFormattable;

				public static bool IsUtf8SpanFormattable() =>
					InstallationId.Create(System.Guid.NewGuid()) is System.IUtf8SpanFormattable;

				public static bool IsParsable() =>
					InstallationId.Create(System.Guid.NewGuid()) is System.IParsable<InstallationId>;

				public static bool IsSpanParsable() =>
					InstallationId.Create(System.Guid.NewGuid()) is System.ISpanParsable<InstallationId>;

				public static bool IsUtf8SpanParsable() =>
					InstallationId.Create(System.Guid.NewGuid()) is System.IUtf8SpanParsable<InstallationId>;

				public static string TryFormatSpan(System.Guid id)
				{
					System.Span<char> buffer = stackalloc char[64];
					var value = InstallationId.Create(id);
					if (((System.ISpanFormattable)value).TryFormat(buffer, out var written, "N", System.Globalization.CultureInfo.InvariantCulture))
						return new string(buffer[..written]);
					return "FAILED";
				}

				public static string TryFormatUtf8(System.Guid id)
				{
					System.Span<byte> buffer = stackalloc byte[64];
					var value = InstallationId.Create(id);
					if (((System.IUtf8SpanFormattable)value).TryFormat(buffer, out var written, "N", System.Globalization.CultureInfo.InvariantCulture))
						return System.Text.Encoding.UTF8.GetString(buffer[..written]);
					return "FAILED";
				}

				public static System.Guid ParseString(string s) =>
					InstallationId.Parse(s, System.Globalization.CultureInfo.InvariantCulture).Value;

				public static System.Guid ParseSpan(string s) =>
					InstallationId.Parse(s.AsSpan(), System.Globalization.CultureInfo.InvariantCulture).Value;

				public static System.Guid ParseUtf8(string s) =>
					InstallationId.Parse(System.Text.Encoding.UTF8.GetBytes(s), System.Globalization.CultureInfo.InvariantCulture).Value;

				public static bool TryParseInvalid() =>
					InstallationId.TryParse("not-a-guid", System.Globalization.CultureInfo.InvariantCulture, out _);
			}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsGeneratorTestOptions.Default.Compile(), cancellationToken);
		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harnessType = assembly.GetType("Testing.InstallationIdHarness")!;
		var id = Guid.NewGuid();
		var text = id.ToString("N", System.Globalization.CultureInfo.InvariantCulture);

		await Assert.That((bool)harnessType.GetMethod("IsValueEquatable")!.Invoke(null, null)!).IsTrue();
		await Assert.That((bool)harnessType.GetMethod("IsSpanFormattable")!.Invoke(null, null)!).IsTrue();
		await Assert.That((bool)harnessType.GetMethod("IsUtf8SpanFormattable")!.Invoke(null, null)!).IsTrue();
		await Assert.That((bool)harnessType.GetMethod("IsParsable")!.Invoke(null, null)!).IsTrue();
		await Assert.That((bool)harnessType.GetMethod("IsSpanParsable")!.Invoke(null, null)!).IsTrue();
		await Assert.That((bool)harnessType.GetMethod("IsUtf8SpanParsable")!.Invoke(null, null)!).IsTrue();

		await Assert.That((string)harnessType.GetMethod("TryFormatSpan")!.Invoke(null, [id])!).IsEqualTo(text);
		await Assert.That((string)harnessType.GetMethod("TryFormatUtf8")!.Invoke(null, [id])!).IsEqualTo(text);
		await Assert.That((Guid)harnessType.GetMethod("ParseString")!.Invoke(null, [text])!).IsEqualTo(id);
		await Assert.That((Guid)harnessType.GetMethod("ParseSpan")!.Invoke(null, [text])!).IsEqualTo(id);
		await Assert.That((Guid)harnessType.GetMethod("ParseUtf8")!.Invoke(null, [text])!).IsEqualTo(id);
		await Assert.That((bool)harnessType.GetMethod("TryParseInvalid")!.Invoke(null, null)!).IsFalse();
	}

	[Test]
	public async Task ScalarMirroring_GivenNonRecordClass_ImplementsSelfEquatable(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
			[Purview.ValueObjects.Serialization.Scalar]
			public sealed partial class OrderCode
			{
				public int Value { get; }
			}

			public static class OrderCodeHarness
			{
				public static bool IsSelfEquatable() => OrderCode.Create(1) is System.IEquatable<OrderCode>;

				public static bool EqualsIsSymmetric() => OrderCode.Create(7).Equals(OrderCode.Create(7));

				public static bool IsValueEquatable() => OrderCode.Create(1) is System.IEquatable<int>;
			}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsGeneratorTestOptions.Default.Compile(), cancellationToken);
		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harnessType = assembly.GetType("Testing.OrderCodeHarness")!;

		await Assert.That((bool)harnessType.GetMethod("IsSelfEquatable")!.Invoke(null, null)!).IsTrue();
		await Assert.That((bool)harnessType.GetMethod("EqualsIsSymmetric")!.Invoke(null, null)!).IsTrue();
		await Assert.That((bool)harnessType.GetMethod("IsValueEquatable")!.Invoke(null, null)!).IsTrue();
	}

	[Test]
	public async Task ScalarMirroring_GivenNullableValueType_SkipsValueEquatable(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
			[Purview.ValueObjects.Serialization.Scalar<int?>]
			public readonly partial record struct OptionalQuantity { }

			public static class OptionalQuantityHarness
			{
				public static bool IsValueEquatable() =>
					OptionalQuantity.Hydrate(1) is System.IEquatable<int?>;

				public static bool IsParsable() =>
					OptionalQuantity.Hydrate(1) is System.IParsable<OptionalQuantity>;
			}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsGeneratorTestOptions.Default.Compile(), cancellationToken);
		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harnessType = assembly.GetType("Testing.OptionalQuantityHarness")!;

		await Assert.That((bool)harnessType.GetMethod("IsValueEquatable")!.Invoke(null, null)!).IsFalse();
		await Assert.That((bool)harnessType.GetMethod("IsParsable")!.Invoke(null, null)!).IsTrue();
	}

	[Test]
	public async Task ScalarMirroring_ParseValidatesThroughCreate(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
			[Purview.ValueObjects.Serialization.Scalar<System.Guid>]
			public readonly partial record struct TenantId
			{
				static partial void OnValidate(System.Guid value)
				{
					if (value == System.Guid.Empty)
						throw new System.ArgumentException("required", nameof(value));
				}
			}

			public static class TenantIdHarness
			{
				public static bool TryParseEmpty() =>
					TenantId.TryParse(System.Guid.Empty.ToString("D"), null, out _);

				public static bool ParseEmptyThrows()
				{
					try
					{
						TenantId.Parse(System.Guid.Empty.ToString("D"), null);
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

		var result = await GenerateAsync(source, ValueObjectsGeneratorTestOptions.Default.Compile(), cancellationToken);
		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harnessType = assembly.GetType("Testing.TenantIdHarness")!;

		await Assert.That((bool)harnessType.GetMethod("TryParseEmpty")!.Invoke(null, null)!).IsFalse();
		await Assert.That((bool)harnessType.GetMethod("ParseEmptyThrows")!.Invoke(null, null)!).IsTrue();
	}

	[Test]
	public async Task ScalarMirroring_GivenString_ImplementsValueEquatableAndParsable(
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

			public static class EmailAddressHarness
			{
				public static bool IsValueEquatable() =>
					EmailAddress.Create("a@b.com") is System.IEquatable<string>;

				public static bool IsParsable() =>
					EmailAddress.Create("a@b.com") is System.IParsable<EmailAddress>;

				public static bool IsSpanFormattable() =>
					EmailAddress.Create("a@b.com") is System.ISpanFormattable;

				public static string ParseValue(string s) => EmailAddress.Parse(s, null).Value;
			}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsGeneratorTestOptions.Default.Compile(), cancellationToken);
		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harnessType = assembly.GetType("Testing.EmailAddressHarness")!;

		await Assert.That((bool)harnessType.GetMethod("IsValueEquatable")!.Invoke(null, null)!).IsTrue();
		await Assert.That((bool)harnessType.GetMethod("IsParsable")!.Invoke(null, null)!).IsTrue();
		await Assert.That((bool)harnessType.GetMethod("IsSpanFormattable")!.Invoke(null, null)!).IsFalse();
		await Assert.That((string)harnessType.GetMethod("ParseValue")!.Invoke(null, ["a@b.com"])!).IsEqualTo("a@b.com");
	}

	[Test]
	public async Task ScalarMirroring_GivenEnum_ImplementsSpanFormattableOnly(CancellationToken cancellationToken)
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
				public static bool IsSpanFormattable() => Priority.Create(Severity.High) is System.ISpanFormattable;

				public static bool IsParsable() =>
					System.Array.Exists(typeof(Priority).GetInterfaces(), i => i.Name == "IParsable`1");

				public static bool IsValueEquatable() => Priority.Create(Severity.High) is System.IEquatable<Severity>;

				public static string TryFormatSpan()
				{
					System.Span<char> buffer = stackalloc char[16];
					var value = Priority.Create(Severity.High);
					if (((System.ISpanFormattable)value).TryFormat(buffer, out var written, "D", System.Globalization.CultureInfo.InvariantCulture))
						return new string(buffer[..written]);
					return "FAILED";
				}
			}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsGeneratorTestOptions.Default.Compile(), cancellationToken);
		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harnessType = assembly.GetType("Testing.PriorityHarness")!;

		await Assert.That((bool)harnessType.GetMethod("IsSpanFormattable")!.Invoke(null, null)!).IsTrue();
		await Assert.That((bool)harnessType.GetMethod("IsParsable")!.Invoke(null, null)!).IsFalse();
		await Assert.That((bool)harnessType.GetMethod("IsValueEquatable")!.Invoke(null, null)!).IsFalse();
		await Assert.That((string)harnessType.GetMethod("TryFormatSpan")!.Invoke(null, null)!).IsEqualTo("2");
	}

	[Test]
	public async Task ScalarMirroring_GivenTypeWithoutStandardInterfaces_MirrorsNone(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
			public sealed class Opaque
			{
				public Opaque(string value) => Value = value;

				public string Value { get; }
			}

			[Purview.ValueObjects.Serialization.Scalar<Opaque>(Nullable = true)]
			public readonly partial record struct Wrapper { }

			public static class WrapperHarness
			{
				public static bool IsValueEquatable() => Wrapper.Hydrate(new Opaque("x")) is System.IEquatable<Opaque>;

				public static bool IsFormattable() => Wrapper.Hydrate(new Opaque("x")) is System.IFormattable;

				public static bool IsParsable() =>
					System.Array.Exists(typeof(Wrapper).GetInterfaces(), i => i.Name == "IParsable`1");
			}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsGeneratorTestOptions.Default.Compile(), cancellationToken);
		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harnessType = assembly.GetType("Testing.WrapperHarness")!;

		await Assert.That((bool)harnessType.GetMethod("IsValueEquatable")!.Invoke(null, null)!).IsFalse();
		await Assert.That((bool)harnessType.GetMethod("IsFormattable")!.Invoke(null, null)!).IsFalse();
		await Assert.That((bool)harnessType.GetMethod("IsParsable")!.Invoke(null, null)!).IsFalse();
	}

	[Test]
	public async Task ScalarMirroring_GivenAuthorDeclaredParse_IsNotDuplicated(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
			[Purview.ValueObjects.Serialization.Scalar]
			public readonly partial record struct CustomCode
			{
				public int Value { get; }

				public static CustomCode Parse(string s, System.IFormatProvider? provider) =>
					Create(int.Parse(s, provider));
			}

			public static class CustomCodeHarness
			{
				public static int ParseValue(string s) => CustomCode.Parse(s, null).Value;

				public static bool IsParsable() =>
					System.Array.Exists(typeof(CustomCode).GetInterfaces(), i => i.Name == "IParsable`1");
			}
			}
			""";

		// The assembly is only non-null when the generated partial did not collide with the author's method.
		var result = await GenerateAsync(source, ValueObjectsGeneratorTestOptions.Default.Compile(), cancellationToken);
		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harnessType = assembly.GetType("Testing.CustomCodeHarness")!;

		await Assert.That((int)harnessType.GetMethod("ParseValue")!.Invoke(null, ["42"])!).IsEqualTo(42);
		await Assert.That((bool)harnessType.GetMethod("IsParsable")!.Invoke(null, null)!).IsFalse();
	}

	[Test]
	public async Task ScalarMirroring_GivenAuthorDeclaredNonNullableEquals_SkipsValueEquatable(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
			[Purview.ValueObjects.Serialization.Scalar]
			public readonly partial record struct Code
			{
				public string Value { get; }

				public bool Equals(string other) => Value == other;
			}

			public static class CodeHarness
			{
				public static bool IsValueEquatable() => Code.Create("x") is System.IEquatable<string>;
			}
			}
			""";

		// The generated code must not report CS8767 (the author's non-nullable parameter cannot satisfy
		// IEquatable<string>.Equals(string?)), so the interface is left to the author.
		var result = await GenerateAsync(source, ValueObjectsGeneratorTestOptions.Default.Compile(), cancellationToken);
		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harnessType = assembly.GetType("Testing.CodeHarness")!;

		await Assert.That((bool)harnessType.GetMethod("IsValueEquatable")!.Invoke(null, null)!).IsFalse();
	}

	[Test]
	public async Task ScalarMirroring_GivenAuthorDeclaredNullableEquals_ImplementsValueEquatable(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
			[Purview.ValueObjects.Serialization.Scalar]
			public readonly partial record struct Code
			{
				public string Value { get; }

				public bool Equals(string? other) => Value == other;
			}

			public static class CodeHarness
			{
				public static bool IsValueEquatable() => Code.Create("x") is System.IEquatable<string>;
			}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsGeneratorTestOptions.Default.Compile(), cancellationToken);
		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harnessType = assembly.GetType("Testing.CodeHarness")!;

		await Assert.That((bool)harnessType.GetMethod("IsValueEquatable")!.Invoke(null, null)!).IsTrue();
	}
}
