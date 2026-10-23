namespace Purview.ValueObjects.SourceGenerator.Analyzers;

/// <summary>
/// Analyzer coverage for the two nullability declarations of an automatic scalar: the attribute's
/// <c>Nullable</c> option and a user-declared <c>Value</c> property. The manual form is the supported
/// way to own the property, so it is never diagnosed.
/// </summary>
public sealed class ScalarNullabilityDiagnosticTests : AnalyzerTestBase<ValueObjectDiagnosticAnalyzer>
{
	[Test]
	public async Task Generate_GivenAutomaticNullableWithDeclaredValue_ReportsVO1028(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar<string>(Nullable = true)]
				public readonly partial record struct Handle
				{
					public string? Value { get; init; }
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.ScalarNullabilityDeclaredTwice);
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.ScalarPropertyIsGenerated);
	}

	[Test]
	public async Task Generate_GivenTypeofAutomaticNullableWithDeclaredValue_ReportsVO1028(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar(typeof(System.DateTimeOffset), Nullable = true)]
				public readonly partial record struct ObservedAt
				{
					public System.DateTimeOffset? Value { get; init; }
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.ScalarNullabilityDeclaredTwice);
	}

	[Test]
	public async Task Generate_GivenExplicitNonNullableWithNullableDeclaredValue_ReportsVO1029(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar<System.DateTimeOffset>(Nullable = false)]
				public readonly partial record struct ObservedAt
				{
					public System.DateTimeOffset? Value { get; init; }
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.ScalarNullabilityContradictsProperty);
	}

	[Test]
	public async Task Generate_GivenDeclaredValueOfTheWrongType_ReportsVO1030(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar<string>]
				public readonly partial record struct Handle
				{
					public int Value { get; init; }
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.ScalarPropertyTypeMismatch);
	}

	[Test]
	public async Task Generate_GivenDeclaredValueMatchingTheUnderlyingType_ReportsVO1022(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar<System.Guid>]
				public readonly partial record struct InstallationId
				{
					public System.Guid Value { get; init; }
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.ScalarPropertyIsGenerated);
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.ScalarNullabilityDeclaredTwice);
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.ScalarNullabilityContradictsProperty);
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.ScalarPropertyTypeMismatch);
	}

	[Test]
	public async Task Generate_GivenManualNullableScalar_ReportsNoNullabilityDiagnostic(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar]
				public readonly partial record struct Handle
				{
					public string? Value { get; init; }
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.ScalarNullabilityDeclaredTwice);
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.ScalarNullabilityContradictsProperty);
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.ScalarPropertyTypeMismatch);
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.ScalarPropertyIsGenerated);
	}

	[Test]
	public async Task Generate_GivenNullableScalarPropertyWithoutEntityFramework_DoesNotReportVO1031(
		CancellationToken cancellationToken
	)
	{
		// VO1031 explains an Entity Framework Core mapping consequence, so it only runs where the
		// project references Entity Framework Core. This compilation does not.
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar<string>(Nullable = true)]
				public readonly partial record struct NullableText { }

				public sealed class Widget
				{
					public NullableText DataJson { get; set; }
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.NullableScalarPropertyShouldBeNullable);
	}
}
