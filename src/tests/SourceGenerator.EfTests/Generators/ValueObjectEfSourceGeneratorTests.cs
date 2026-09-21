using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Purview.ValueObjects.SourceGenerator.Generators;

/// <summary>
/// Source-generator tests for the Entity Framework Core integration: the conditional <c>Ef</c> nested
/// members, the marker interfaces, the assembly-level <c>ValueObjectEfExtensions</c> registry, and the
/// three opt-out levels (MSBuild property, assembly defaults, per-type options).
/// </summary>
public sealed class ValueObjectEfSourceGeneratorTests : ValueObjectEfSourceGeneratorTestBase
{
	const string ScalarSource = """
		namespace Testing
		{
			[Purview.ValueObjects.Serialization.Scalar]
			public readonly partial record struct EmailAddress
			{
				public string Value { get; }

				static partial void OnValidate(string value)
				{
					if (string.IsNullOrWhiteSpace(value))
						throw new System.ArgumentException("Email address cannot be empty.", nameof(value));
				}
			}

			[Purview.ValueObjects.Serialization.Scalar]
			public readonly partial record struct CustomerId
			{
				public System.Guid Value { get; }
			}
		}
		""";

	const string ComplexSource = """
		namespace Testing
		{
			[Purview.ValueObjects.Serialization.Scalar]
			public readonly partial record struct CurrencyCode
			{
				public string Value { get; }
			}

			[Purview.ValueObjects.Serialization.ValueObject]
			public readonly partial record struct Money
			{
				public decimal Amount { get; }

				public CurrencyCode Currency { get; }
			}
		}
		""";

	[Test]
	public async Task ScalarEfGeneration_EmitsEfClassConverterAndComparer(CancellationToken cancellationToken)
	{
		var result = await GenerateAsync(
			ScalarSource,
			ValueObjectsEfGeneratorTestOptions.Default.Compile(),
			cancellationToken
		);

		var compilation = result.CompilationResult.Compilation;
		var emailAddress = compilation.GetTypeByMetadataName("Testing.EmailAddress")!;

		await Assert.That(emailAddress.AllInterfaces.Any(static i => i.Name == "IEfScalarValueObject")).IsTrue();

		var efType = emailAddress.GetTypeMembers("Ef").Single();
		var converter = efType.GetMembers("Converter").Single() as IFieldSymbol;
		var comparer = efType.GetMembers("Comparer").Single() as IFieldSymbol;

		await Assert.That(converter).IsNotNull();
		await Assert.That(comparer).IsNotNull();
		var converterType = converter!.Type as INamedTypeSymbol;
		await Assert.That(converterType).IsNotNull();
		await Assert.That(converterType!.Name).IsEqualTo("ValueConverter");
		await Assert.That(converterType.TypeArguments.Length).IsEqualTo(2);
		await Assert.That(converterType.TypeArguments[0].Name).IsEqualTo("EmailAddress");
		await Assert.That(converterType.TypeArguments[1].SpecialType).IsEqualTo(SpecialType.System_String);
		await Assert.That(converter.IsStatic).IsTrue();
		await Assert.That(comparer!.Type.Name).IsEqualTo("ValueComparer");
		await Assert.That(comparer.IsStatic).IsTrue();
	}

	[Test]
	public async Task ScalarEfGeneration_UsesHydrateFactoryByDefault(CancellationToken cancellationToken)
	{
		var result = await GenerateAsync(ScalarSource, ValueObjectsEfGeneratorTestOptions.Default, cancellationToken);

		var query = result.Generated();
		var emailAddress = query.GetRecord("EmailAddress", "Testing");

		await Assert.That(query.HasClass("Ef")).IsTrue();
		await Assert.That(emailAddress.Node.BaseList?.ToString()).Contains("IEfScalarValueObject");

		var converterInitializer = GetFieldInitializer(query.GetClass("Ef").Node, "Converter");
		await Assert.That(converterInitializer).Contains("Testing.EmailAddress.Hydrate(v)");
	}

	[Test]
	public async Task ScalarEfGeneration_StrictDeserialization_UsesCreateFactory(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar(DeserializationMode = Purview.ValueObjects.Serialization.ValueObjectDeserializationMode.Strict)]
				public readonly partial record struct EmailAddress
				{
					public string Value { get; }
				}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsEfGeneratorTestOptions.Default, cancellationToken);

		var converterInitializer = GetFieldInitializer(result.Generated().GetClass("Ef").Node, "Converter");
		await Assert.That(converterInitializer).Contains("Testing.EmailAddress.Create(v)");
	}

	[Test]
	public async Task ScalarEfGeneration_PerTypeOptOut_EmitsNoEfMembers(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar(GenerateEfConverter = false, GenerateEfComparer = false)]
				public readonly partial record struct EmailAddress
				{
					public string Value { get; }
				}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsEfGeneratorTestOptions.Default, cancellationToken);

		var query = result.Generated();
		await Assert.That(query.HasClass("Ef")).IsFalse();

		var emailAddress = query.GetRecord("EmailAddress", "Testing");
		await Assert.That(emailAddress.Node.BaseList?.ToString()).DoesNotContain("IEfScalarValueObject");
	}

	[Test]
	public async Task ComplexEfGeneration_EmitsComparerAndComplexTypeMarker(CancellationToken cancellationToken)
	{
		var result = await GenerateAsync(
			ComplexSource,
			ValueObjectsEfGeneratorTestOptions.Default.Compile(),
			cancellationToken
		);

		var compilation = result.CompilationResult.Compilation;
		var money = compilation.GetTypeByMetadataName("Testing.Money")!;

		await Assert.That(money.AllInterfaces.Any(static i => i.Name == "IEfComplexValueObject")).IsTrue();

		var efType = money.GetTypeMembers("Ef").Single();
		await Assert.That(efType.GetMembers("Converter")).IsEmpty();
		await Assert.That(efType.GetMembers("Comparer")).IsNotEmpty();
	}

	[Test]
	public async Task ComplexEfGeneration_JsonMapping_EmitsJsonConverter(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.ValueObject(EfMapping = Purview.ValueObjects.Serialization.EfMapping.Json)]
				public readonly partial record struct Money
				{
					public decimal Amount { get; }
				}
			}
			""";

		var result = await GenerateAsync(
			source,
			ValueObjectsEfGeneratorTestOptions.Default.Compile(),
			cancellationToken
		);

		var compilation = result.CompilationResult.Compilation;
		var money = compilation.GetTypeByMetadataName("Testing.Money")!;
		var efType = money.GetTypeMembers("Ef").Single();
		var converter = efType.GetMembers("Converter").Single() as IFieldSymbol;

		await Assert.That(converter).IsNotNull();
		var converterType = (INamedTypeSymbol)converter!.Type;
		await Assert.That(converterType.Name).IsEqualTo("ValueConverter");
		await Assert.That(converterType.TypeArguments[1].SpecialType).IsEqualTo(SpecialType.System_String);

		var initializer = GetFieldInitializer(efType, "Converter");
		await Assert.That(initializer).Contains("JsonSerializer.Serialize(vo)");
	}

	[Test]
	public async Task ComplexEfGeneration_NoneMapping_EmitsNoEfMembers(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.ValueObject(EfMapping = Purview.ValueObjects.Serialization.EfMapping.None, GenerateEfComparer = false)]
				public readonly partial record struct Money
				{
					public decimal Amount { get; }
				}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsEfGeneratorTestOptions.Default, cancellationToken);

		var query = result.Generated();
		await Assert.That(query.HasClass("Ef")).IsFalse();
		await Assert
			.That(query.GetRecord("Money", "Testing").Node.BaseList?.ToString())
			.DoesNotContain("IEfComplexValueObject");
	}

	[Test]
	public async Task EfRegistry_EmittedWithConfigureValueObjectsExtension(CancellationToken cancellationToken)
	{
		var result = await GenerateAsync(
			ScalarSource,
			ValueObjectsEfGeneratorTestOptions.Default.Compile(),
			cancellationToken
		);

		var query = result.Generated();
		await Assert.That(query.HasClass("ValueObjectEfExtensions", "Purview.ValueObjects.Ef")).IsTrue();

		var registryText = Normalize(
			query.GetClass("ValueObjectEfExtensions", "Purview.ValueObjects.Ef").Node.ToString()
		);

		await Assert.That(registryText).Contains("ConfigureValueObjects(this");
		await Assert.That(registryText).Contains("typeof(global::Testing.EmailAddress)");
		await Assert.That(registryText).Contains("Testing.EmailAddress.Ef.Converter");
		await Assert.That(registryText).Contains("Testing.EmailAddress.Ef.Comparer");
		await Assert.That(registryText).Contains("HasConversion");
		await Assert.That(registryText).Contains(".Property(");
	}

	[Test]
	public async Task EfRegistry_IncludesComplexTypeAndJsonMappings(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar]
				public readonly partial record struct CurrencyCode
				{
					public string Value { get; }
				}

				[Purview.ValueObjects.Serialization.ValueObject]
				public readonly partial record struct Money
				{
					public decimal Amount { get; }

					public CurrencyCode Currency { get; }
				}

				[Purview.ValueObjects.Serialization.ValueObject(EfMapping = Purview.ValueObjects.Serialization.EfMapping.Json)]
				public readonly partial record struct Audit
				{
					public System.DateTimeOffset OccurredAt { get; }
				}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsEfGeneratorTestOptions.Default, cancellationToken);

		var registry = Normalize(
			result.Generated().GetClass("ValueObjectEfExtensions", "Purview.ValueObjects.Ef").Node.ToString()
		);

		await Assert.That(registry).Contains("typeof(global::Testing.Money)");
		await Assert.That(registry).Contains("ComplexProperty(");
		await Assert.That(registry).Contains("typeof(global::Testing.Audit)");
		await Assert.That(registry).Contains("Testing.Audit.Ef.Converter");
	}

	[Test]
	public async Task EfRegistry_NotEmittedWhenAllValueObjectsOptOut(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar(GenerateEfConverter = false, GenerateEfComparer = false)]
				public readonly partial record struct EmailAddress
				{
					public string Value { get; }
				}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsEfGeneratorTestOptions.Default, cancellationToken);

		await Assert.That(result.Generated().HasClass("ValueObjectEfExtensions", "Purview.ValueObjects.Ef")).IsFalse();
	}

	[Test]
	public async Task EfGeneration_DisabledViaMSBuildProperty_EmitsNoEfMembersOrRegistry(
		CancellationToken cancellationToken
	)
	{
		ValueObjectsEfGeneratorTestOptions options = new()
		{
			AnalyzerConfigOptions = ImmutableDictionary<string, string>.Empty.Add(
				"build_property.DisableValueObjectsEfGeneration",
				"true"
			),
		};

		var result = await GenerateAsync(ScalarSource, options, cancellationToken);

		var query = result.Generated();
		await Assert.That(query.HasClass("Ef")).IsFalse();
		await Assert.That(query.HasClass("ValueObjectEfExtensions", "Purview.ValueObjects.Ef")).IsFalse();
	}

	[Test]
	public async Task AssemblyDefaults_EfMappingJson_AppliesToComplexValueObjects(CancellationToken cancellationToken)
	{
		const string source = """
			[assembly: Purview.ValueObjects.Serialization.ValueObjectDefaults(EfMapping = Purview.ValueObjects.Serialization.EfMapping.Json)]
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.ValueObject]
				public readonly partial record struct Money
				{
					public decimal Amount { get; }
				}
			}
			""";

		var result = await GenerateAsync(
			source,
			ValueObjectsEfGeneratorTestOptions.Default.Compile(),
			cancellationToken
		);

		var money = result.CompilationResult.Compilation.GetTypeByMetadataName("Testing.Money")!;
		var efType = money.GetTypeMembers("Ef").Single();
		await Assert.That(efType.GetMembers("Converter")).IsNotEmpty();
	}

	[Test]
	public async Task AssemblyDefaults_GenerateEfConverterFalse_DisablesEfForAssembly(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			[assembly: Purview.ValueObjects.Serialization.ValueObjectDefaults(GenerateEfConverter = false, GenerateEfComparer = false)]
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar]
				public readonly partial record struct EmailAddress
				{
					public string Value { get; }
				}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsEfGeneratorTestOptions.Default, cancellationToken);

		var query = result.Generated();
		await Assert.That(query.HasClass("Ef")).IsFalse();
		await Assert.That(query.HasClass("ValueObjectEfExtensions", "Purview.ValueObjects.Ef")).IsFalse();
	}

	[Test]
	public async Task EfRegistry_EmitsUseValueObjectsAndModelCustomizer(CancellationToken cancellationToken)
	{
		var result = await GenerateAsync(ScalarSource, ValueObjectsEfGeneratorTestOptions.Default, cancellationToken);

		var query = result.Generated();
		await Assert.That(query.HasClass("ValueObjectModelCustomizer", "Purview.ValueObjects.Ef")).IsTrue();

		var registry = Normalize(query.GetClass("ValueObjectEfExtensions", "Purview.ValueObjects.Ef").Node.ToString());
		await Assert.That(registry).Contains("UseValueObjects");
		await Assert.That(registry).Contains("ReplaceService<");
		await Assert.That(registry).Contains("IModelCustomizer");
		await Assert.That(registry).Contains("ValueObjectModelCustomizer");

		var customizer = Normalize(
			query.GetClass("ValueObjectModelCustomizer", "Purview.ValueObjects.Ef").Node.ToString()
		);
		await Assert.That(customizer).Contains("ModelCustomizer");
		await Assert.That(customizer).Contains("ConfigureValueObjects()");
	}

	[Test]
	public async Task ScalarEf_NonMappableProviderType_EmitsAutoConversionSkippedDiagnostic(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
				public sealed record CustomProvider
				{
					public string Raw { get; init; } = string.Empty;
				}

				[Purview.ValueObjects.Serialization.Scalar]
				public readonly partial record struct CustomId
				{
					public CustomProvider Value { get; }
				}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsEfGeneratorTestOptions.Default, cancellationToken);

		await Assert.That(result).HasDiagnostic("VO1010");
	}

	static string Normalize(string source) => string.Concat(source.Where(static c => !char.IsWhiteSpace(c)));

	static string GetFieldInitializer(INamedTypeSymbol efType, string fieldName)
	{
		var field = efType.GetMembers(fieldName).Single();
		var syntax = field.DeclaringSyntaxReferences[0].GetSyntax();
		return syntax.ToString();
	}

	static string GetFieldInitializer(
		Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax efClass,
		string fieldName
	)
	{
		var field = efClass
			.Members.OfType<Microsoft.CodeAnalysis.CSharp.Syntax.FieldDeclarationSyntax>()
			.Single(f => f.Declaration.Variables.Any(v => v.Identifier.ValueText == fieldName));
		return field.ToString();
	}
}
