namespace Purview.ValueObjects.SourceGenerator.Generators;

/// <summary>
/// Entity Framework Core registry and diagnostic coverage for nullable scalars: a <c>T?</c> property
/// matches the <c>T</c> converter, a nullable scalar maps to an optional column where Entity Framework
/// Core permits it, and using the scalar on a non-nullable mapped property is reported as <c>VO1031</c>.
/// </summary>
public sealed class NullableScalarEntityFrameworkTests : ValueObjectEFSourceGeneratorTestBase
{
	const string RegistrySource = """
		namespace Testing
		{
			[Purview.ValueObjects.Serialization.Scalar<string>(Nullable = true)]
			public readonly partial record struct NullableText { }

			[Purview.ValueObjects.Serialization.Scalar<string>]
			public readonly partial record struct RequiredText { }

			public sealed class Widget
			{
				public int Id { get; set; }

				public NullableText DataJson { get; set; }

				public NullableText? Alias { get; set; }

				public RequiredText Required { get; set; }
			}
		}
		""";

	static string Normalize(string source) => string.Concat(source.Where(static c => !char.IsWhiteSpace(c)));

	[Test]
	public async Task EFRegistry_GivenNullableScalar_MapsBothPropertyShapes(CancellationToken cancellationToken)
	{
		using var result = await GenerateAsync(
			RegistrySource,
			ValueObjectsEFGeneratorTestOptions.Default.Compile(),
			cancellationToken
		);

		var registry = Normalize(
			result.Generated().GetClass("ValueObjectEFExtensions", "Microsoft.EntityFrameworkCore").Node.ToString()
		);

		// The Nullable<T> unwrap is what makes a T? property find the T converter.
		await Assert
			.That(registry)
			.Contains("global::System.Nullable.GetUnderlyingType(property.PropertyType)??property.PropertyType");
		await Assert
			.That(registry)
			.Contains(
				"[typeof(global::Testing.NullableText)]=newScalarMapping(global::Testing.NullableText.EF.Converter,global::Testing.NullableText.EF.Comparer,true,true)"
			);
		await Assert
			.That(registry)
			.Contains(
				"[typeof(global::Testing.RequiredText)]=newScalarMapping(global::Testing.RequiredText.EF.Converter,global::Testing.RequiredText.EF.Comparer,true,false)"
			);

		// Only EF-accepted optional configuration is emitted: no non-nullable struct, no key, and never
		// over an explicit IsRequired.
		await Assert.That(registry).Contains(".IsRequired(false)");
		await Assert.That(registry).Contains("!scalarBuilder.Metadata.IsKey()");
		await Assert.That(registry).Contains("GetIsNullableConfigurationSource()");
		await Assert
			.That(registry)
			.Contains("scalar.IsNullable||global::System.Nullable.GetUnderlyingType(property.PropertyType)isnotnull");
	}

	[Test]
	public async Task EFRegistry_GivenComplexTypeMember_UnwrapsAndMarksNullableMembers(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar<string>(Nullable = true)]
				public readonly partial record struct NullableText { }

				[Purview.ValueObjects.Serialization.ValueObject]
				public readonly partial record struct Basket
				{
					public NullableText Note { get; }

					public NullableText? OptionalNote { get; }
				}

				public sealed class Widget
				{
					public int Id { get; set; }

					public Basket Basket { get; set; }
				}
			}
			""";

		using var result = await GenerateAsync(
			source,
			ValueObjectsEFGeneratorTestOptions.Default.Compile(),
			cancellationToken
		);

		var registry = Normalize(
			result.Generated().GetClass("ValueObjectEFExtensions", "Microsoft.EntityFrameworkCore").Node.ToString()
		);

		await Assert
			.That(registry)
			.Contains("global::System.Nullable.GetUnderlyingType(member.PropertyType)??member.PropertyType");
		await Assert.That(registry).Contains("scalarMappings.TryGetValue(memberType,outvarscalar)");
		await Assert.That(registry).Contains("memberBuilder.Metadata.IsKey()");
		await Assert.That(registry).Contains("memberBuilder.IsRequired(false)");
	}

	[Test]
	public async Task VO1031_GivenEntityPropertyOfNullableScalar_ReportsWarning(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar<string>(Nullable = true)]
				public readonly partial record struct NullableText { }

				public sealed class Widget
				{
					public int Id { get; set; }

					public NullableText DataJson { get; set; }
				}

				public sealed class ShopContext : global::Microsoft.EntityFrameworkCore.DbContext
				{
					public global::Microsoft.EntityFrameworkCore.DbSet<Widget> Widgets { get; set; } = null!;
				}
			}
			""";

		using var result = await GenerateAsync(source, ValueObjectsEFGeneratorTestOptions.Default, cancellationToken);

		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.NullableScalarPropertyShouldBeNullable);
	}

	[Test]
	public async Task VO1031_GivenEntityConfiguredByEntityTypeConfiguration_ReportsWarning(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar<System.DateTimeOffset>(Nullable = true)]
				public readonly partial record struct OptionalTimestamp { }

				public sealed class Widget
				{
					public int Id { get; set; }

					public OptionalTimestamp ObservedAt { get; set; }
				}

				public sealed class WidgetConfiguration
					: global::Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<Widget>
				{
					public void Configure(
						global::Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Widget> builder
					)
					{
					}
				}
			}
			""";

		using var result = await GenerateAsync(source, ValueObjectsEFGeneratorTestOptions.Default, cancellationToken);

		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.NullableScalarPropertyShouldBeNullable);
	}

	[Test]
	public async Task VO1031_GivenPlainDtoProperty_DoesNotReport(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar<string>(Nullable = true)]
				public readonly partial record struct NullableText { }

				public sealed class WidgetDto
				{
					public NullableText DataJson { get; set; }
				}
			}
			""";

		using var result = await GenerateAsync(source, ValueObjectsEFGeneratorTestOptions.Default, cancellationToken);

		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.NullableScalarPropertyShouldBeNullable);
	}

	[Test]
	public async Task VO1031_GivenNullablePropertyOrNonNullableScalar_DoesNotReport(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar<string>(Nullable = true)]
				public readonly partial record struct NullableText { }

				[Purview.ValueObjects.Serialization.Scalar<string>]
				public readonly partial record struct RequiredText { }

				public sealed class Widget
				{
					public int Id { get; set; }

					public NullableText? Alias { get; set; }

					public RequiredText Required { get; set; }
				}

				public sealed class ShopContext : global::Microsoft.EntityFrameworkCore.DbContext
				{
					public global::Microsoft.EntityFrameworkCore.DbSet<Widget> Widgets { get; set; } = null!;
				}
			}
			""";

		using var result = await GenerateAsync(source, ValueObjectsEFGeneratorTestOptions.Default, cancellationToken);

		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.NullableScalarPropertyShouldBeNullable);
	}

	[Test]
	public async Task VO1031_GivenKeyProperty_DoesNotReport(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar<string>(Nullable = true)]
				public readonly partial record struct NullableText { }

				public sealed class Widget
				{
					public NullableText Id { get; set; }
				}

				public sealed class ShopContext : global::Microsoft.EntityFrameworkCore.DbContext
				{
					public global::Microsoft.EntityFrameworkCore.DbSet<Widget> Widgets { get; set; } = null!;
				}
			}
			""";

		using var result = await GenerateAsync(source, ValueObjectsEFGeneratorTestOptions.Default, cancellationToken);

		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.NullableScalarPropertyShouldBeNullable);
	}

	[Test]
	public async Task VO1031_GivenComplexTypeMember_ReportsWarning(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar<string>(Nullable = true)]
				public readonly partial record struct NullableText { }

				[Purview.ValueObjects.Serialization.ValueObject]
				public readonly partial record struct Basket
				{
					public NullableText Note { get; }
				}
			}
			""";

		using var result = await GenerateAsync(source, ValueObjectsEFGeneratorTestOptions.Default, cancellationToken);

		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.NullableScalarPropertyShouldBeNullable);
	}

	[Test]
	public async Task VO1031_GivenJsonMappedComplexTypeMember_DoesNotReport(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar<string>(Nullable = true)]
				public readonly partial record struct NullableText { }

				[Purview.ValueObjects.Serialization.ValueObject(
					EFMapping = Purview.ValueObjects.Serialization.EntityFrameworkMapping.Json
				)]
				public readonly partial record struct Basket
				{
					public NullableText Note { get; }
				}
			}
			""";

		using var result = await GenerateAsync(source, ValueObjectsEFGeneratorTestOptions.Default, cancellationToken);

		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.NullableScalarPropertyShouldBeNullable);
	}
}
