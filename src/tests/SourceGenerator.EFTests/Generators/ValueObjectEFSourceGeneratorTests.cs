using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Purview.ValueObjects.SourceGenerator.Generators;

/// <summary>
/// Source-generator tests for the Entity Framework Core integration: the conditional <c>EF</c> nested
/// members, the marker interfaces, the assembly-level <c>ValueObjectEFExtensions</c> registry, and the
/// three opt-out levels (MSBuild property, assembly defaults, per-type options).
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Design",
	"CA1506:Avoid excessive class coupling",
	Justification = "Value object EF tests couple many Roslyn test helper types."
)]
public sealed class ValueObjectEFSourceGeneratorTests : ValueObjectEFSourceGeneratorTestBase
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
	public async Task ScalarEFGeneration_EmitsEFClassConverterAndComparer(CancellationToken cancellationToken)
	{
		var result = await GenerateAsync(
			ScalarSource,
			ValueObjectsEFGeneratorTestOptions.Default.Compile(),
			cancellationToken
		);

		var compilation = result.CompilationResult.Compilation;
		var emailAddress = compilation.GetTypeByMetadataName("Testing.EmailAddress")!;

		await Assert.That(emailAddress.AllInterfaces.Any(static i => i.Name == "IEFScalarValueObject")).IsTrue();

		var efType = emailAddress.GetTypeMembers("EF").Single();
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
	public async Task ScalarEFGeneration_UsesHydrateFactoryByDefault(CancellationToken cancellationToken)
	{
		var result = await GenerateAsync(ScalarSource, ValueObjectsEFGeneratorTestOptions.Default, cancellationToken);

		var query = result.Generated();
		var emailAddress = query.GetRecord("EmailAddress", "Testing");

		await Assert.That(query.HasClass("EF")).IsTrue();
		await Assert.That(emailAddress.Node.BaseList?.ToString()).Contains("IEFScalarValueObject");

		var converterInitializer = GetFieldInitializer(query.GetClass("EF").Node, "Converter");
		await Assert.That(converterInitializer).Contains("Testing.EmailAddress.Hydrate(v)");
	}

	[Test]
	public async Task ScalarEFGeneration_GuidBackedInitOnlyProperty_UsesGuidProvider(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar]
				public readonly partial record struct CustomerId
				{
					public System.Guid Value { get; init; }
				}
			}
			""";

		// Arrange
		var result = await GenerateAsync(source, ValueObjectsEFGeneratorTestOptions.Default, cancellationToken);

		// Act
		var query = result.Generated();
		var customerId = query.GetRecord("CustomerId", "Testing");
		var generatedText = Normalize(customerId.Node.ToString());

		// Assert
		await Assert.That(customerId.Node.BaseList?.ToString()).Contains("IEFScalarValueObject");
		await Assert.That(generatedText).Contains("ValueConverter<global::Testing.CustomerId,global::System.Guid>");
		await Assert.That(generatedText).Contains("Testing.CustomerId.Hydrate(v)");
	}

	[Test]
	public async Task ScalarEFGeneration_StrictDeserialization_UsesHydrateFactoryInEfConverter(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar(DeserializationMode = Purview.ValueObjects.Serialization.ValueObjectDeserializationMode.Strict)]
				public readonly partial record struct EmailAddress
				{
					public string Value { get; }
				}

				[Purview.ValueObjects.Serialization.Scalar(DeserializationMode = Purview.ValueObjects.Serialization.ValueObjectDeserializationMode.Strict)]
				public readonly partial record struct CustomerId
				{
					public System.Guid Value { get; }
				}
			}
			""";

		// Arrange
		var result = await GenerateAsync(source, ValueObjectsEFGeneratorTestOptions.Default.Compile(), cancellationToken);

		// Act
		var query = result.Generated();
		var emailAddress = query.GetRecord("EmailAddress", "Testing");
		var customerId = query.GetRecord("CustomerId", "Testing");
		var emailGeneratedText = Normalize(emailAddress.Node.ToString());
		var customerGeneratedText = Normalize(customerId.Node.ToString());

		// Assert
		await Assert.That(emailAddress.Node.BaseList?.ToString()).Contains("IEFScalarValueObject");
		await Assert.That(customerId.Node.BaseList?.ToString()).Contains("IEFScalarValueObject");
		await Assert.That(emailGeneratedText).Contains("ValueConverter<global::Testing.EmailAddress,global::System.String>");
		await Assert.That(emailGeneratedText).Contains("Testing.EmailAddress.Hydrate(v)");
		await Assert.That(customerGeneratedText).Contains("ValueConverter<global::Testing.CustomerId,global::System.Guid>");
		await Assert.That(customerGeneratedText).Contains("Testing.CustomerId.Hydrate(v)");
	}

	[Test]
	public async Task ScalarEFGeneration_StrictDeserialization_UsesHydrateFactory(CancellationToken cancellationToken)
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

		var result = await GenerateAsync(source, ValueObjectsEFGeneratorTestOptions.Default, cancellationToken);

		var converterInitializer = GetFieldInitializer(result.Generated().GetClass("EF").Node, "Converter");
		await Assert.That(converterInitializer).Contains("Testing.EmailAddress.Hydrate(v)");
	}

	[Test]
	public async Task ScalarEFGeneration_PerTypeOptOut_EmitsNoEFMembers(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar(GenerateEFConverter = false, GenerateEFComparer = false)]
				public readonly partial record struct EmailAddress
				{
					public string Value { get; }
				}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsEFGeneratorTestOptions.Default, cancellationToken);

		var query = result.Generated();
		await Assert.That(query.HasClass("EF")).IsFalse();

		var emailAddress = query.GetRecord("EmailAddress", "Testing");
		await Assert.That(emailAddress.Node.BaseList?.ToString()).DoesNotContain("IEFScalarValueObject");
	}

	[Test]
	public async Task ComplexEFGeneration_EmitsComparerAndComplexTypeMarker(CancellationToken cancellationToken)
	{
		var result = await GenerateAsync(
			ComplexSource,
			ValueObjectsEFGeneratorTestOptions.Default.Compile(),
			cancellationToken
		);

		var compilation = result.CompilationResult.Compilation;
		var money = compilation.GetTypeByMetadataName("Testing.Money")!;

		await Assert.That(money.AllInterfaces.Any(static i => i.Name == "IEFComplexValueObject")).IsTrue();

		var efType = money.GetTypeMembers("EF").Single();
		await Assert.That(efType.GetMembers("Converter")).IsEmpty();
		await Assert.That(efType.GetMembers("Comparer")).IsNotEmpty();
	}

	[Test]
	public async Task ComplexEFGeneration_JsonMapping_EmitsJsonConverter(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.ValueObject(EFMapping = Purview.ValueObjects.Serialization.EntityFrameworkMapping.Json)]
				public readonly partial record struct Money
				{
					public decimal Amount { get; }
				}
			}
			""";

		var result = await GenerateAsync(
			source,
			ValueObjectsEFGeneratorTestOptions.Default.Compile(),
			cancellationToken
		);

		var compilation = result.CompilationResult.Compilation;
		var money = compilation.GetTypeByMetadataName("Testing.Money")!;
		var efType = money.GetTypeMembers("EF").Single();
		var converter = efType.GetMembers("Converter").Single() as IFieldSymbol;

		await Assert.That(converter).IsNotNull();
		var converterType = (INamedTypeSymbol)converter!.Type;
		await Assert.That(converterType.Name).IsEqualTo("ValueConverter");
		await Assert.That(converterType.TypeArguments[1].SpecialType).IsEqualTo(SpecialType.System_String);

		var initializer = GetFieldInitializer(efType, "Converter");
		await Assert.That(initializer).Contains("JsonSerializer.Serialize(vo)");
	}

	[Test]
	public async Task ComplexEFGeneration_NoneMapping_EmitsNoEFMembers(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.ValueObject(EFMapping = Purview.ValueObjects.Serialization.EntityFrameworkMapping.None, GenerateEFComparer = false)]
				public readonly partial record struct Money
				{
					public decimal Amount { get; }
				}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsEFGeneratorTestOptions.Default, cancellationToken);

		var query = result.Generated();
		await Assert.That(query.HasClass("EF")).IsFalse();
		await Assert
			.That(query.GetRecord("Money", "Testing").Node.BaseList?.ToString())
			.DoesNotContain("IEFComplexValueObject");
	}

	[Test]
	public async Task EFRegistry_EmittedWithConfigureValueObjectsExtension(CancellationToken cancellationToken)
	{
		var result = await GenerateAsync(
			ScalarSource,
			ValueObjectsEFGeneratorTestOptions.Default.Compile(),
			cancellationToken
		);

		var query = result.Generated();
		await Assert.That(query.HasClass("ValueObjectEFExtensions", "Microsoft.EntityFrameworkCore")).IsTrue();

		var registryText = Normalize(
			query.GetClass("ValueObjectEFExtensions", "Microsoft.EntityFrameworkCore").Node.ToString()
		);

		await Assert.That(registryText).Contains("ConfigureValueObjects(this");
		await Assert.That(registryText).Contains("typeof(global::Testing.EmailAddress)");
		await Assert.That(registryText).Contains("Testing.EmailAddress.EF.Converter");
		await Assert.That(registryText).Contains("Testing.EmailAddress.EF.Comparer");
		await Assert.That(registryText).Contains("HasConversion");
		await Assert.That(registryText).Contains(".Property(");
	}

	[Test]
	public async Task EFRegistry_IncludesComplexTypeAndJsonMappings(CancellationToken cancellationToken)
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

				[Purview.ValueObjects.Serialization.ValueObject(EFMapping = Purview.ValueObjects.Serialization.EntityFrameworkMapping.Json)]
				public readonly partial record struct Audit
				{
					public System.DateTimeOffset OccurredAt { get; }
				}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsEFGeneratorTestOptions.Default, cancellationToken);

		var query = result.Generated();
		var registry = Normalize(query.GetClass("ValueObjectEFExtensions", "Microsoft.EntityFrameworkCore").Node.ToString());

		await Assert.That(registry).Contains("typeof(global::Testing.Money)");
		await Assert.That(registry).Contains("ComplexProperty(");
		await Assert.That(registry).Contains("typeof(global::Testing.Audit)");
		await Assert.That(registry).Contains("Testing.Audit.EF.Converter");
	}

	[Test]
	public async Task EFRegistry_OptedOutLocalValueObjects_AreNotMappedEvenWhenReferencedAssembliesContribute(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar(GenerateEFConverter = false, GenerateEFComparer = false)]
				public readonly partial record struct EmailAddress
				{
					public string Value { get; }
				}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsEFGeneratorTestOptions.Default, cancellationToken);

		// Referenced assemblies (e.g. shared models with EF value objects) can cause the registry to be
		// emitted, but the opted-out local type must not be mapped by it.
		var registry = Normalize(
			result.Generated().GetClass("ValueObjectEFExtensions", "Microsoft.EntityFrameworkCore").Node.ToString()
		);
		await Assert.That(registry).DoesNotContain("Testing.EmailAddress");
	}

	[Test]
	public async Task EFGeneration_DisabledViaMSBuildProperty_EmitsNoEFMembersOrRegistry(
		CancellationToken cancellationToken
	)
	{
		ValueObjectsEFGeneratorTestOptions options = new()
		{
			AnalyzerConfigOptions = ImmutableDictionary<string, string>.Empty.Add(
				"build_property.DisableValueObjectsEFGeneration",
				"true"
			),
		};

		var result = await GenerateAsync(ScalarSource, options, cancellationToken);

		var query = result.Generated();
		await Assert.That(query.HasClass("EF")).IsFalse();
		await Assert.That(query.HasClass("ValueObjectEFExtensions", "Microsoft.EntityFrameworkCore")).IsFalse();
	}

	[Test]
	public async Task AssemblyDefaults_EFMappingJson_AppliesToComplexValueObjects(CancellationToken cancellationToken)
	{
		const string source = """
			[assembly: Purview.ValueObjects.Serialization.ValueObjectDefaults(EFMapping = Purview.ValueObjects.Serialization.EntityFrameworkMapping.Json)]
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
			ValueObjectsEFGeneratorTestOptions.Default.Compile(),
			cancellationToken
		);

		var money = result.CompilationResult.Compilation.GetTypeByMetadataName("Testing.Money")!;
		var efType = money.GetTypeMembers("EF").Single();
		await Assert.That(efType.GetMembers("Converter")).IsNotEmpty();
	}

	[Test]
	public async Task AssemblyDefaults_GenerateEFConverterFalse_DisablesEFForAssembly(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			[assembly: Purview.ValueObjects.Serialization.ValueObjectDefaults(GenerateEFConverter = false, GenerateEFComparer = false)]
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar]
				public readonly partial record struct EmailAddress
				{
					public string Value { get; }
				}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsEFGeneratorTestOptions.Default, cancellationToken);

		var query = result.Generated();
		await Assert.That(query.HasClass("EF")).IsFalse();

		// Assembly defaults disable EF for the local value object; it must not be mapped by the
		// registry (which may still be emitted for value objects from referenced assemblies).
		var registry = Normalize(
			query.GetClass("ValueObjectEFExtensions", "Microsoft.EntityFrameworkCore").Node.ToString()
		);
		await Assert.That(registry).DoesNotContain("Testing.EmailAddress");
	}

	[Test]
	public async Task EFRegistry_EmitsUseValueObjectsAndModelCustomizer(CancellationToken cancellationToken)
	{
		var result = await GenerateAsync(ScalarSource, ValueObjectsEFGeneratorTestOptions.Default, cancellationToken);

		var query = result.Generated();
		await Assert.That(query.HasClass("ValueObjectModelCustomizer", "Microsoft.EntityFrameworkCore")).IsTrue();

		var registry = Normalize(
			query.GetClass("ValueObjectEFExtensions", "Microsoft.EntityFrameworkCore").Node.ToString()
		);
		await Assert.That(registry).Contains("UseValueObjects");
		await Assert.That(registry).Contains("ReplaceService<");
		await Assert.That(registry).Contains("IModelCustomizer");
		await Assert.That(registry).Contains("ValueObjectModelCustomizer");

		var customizer = Normalize(
			query.GetClass("ValueObjectModelCustomizer", "Microsoft.EntityFrameworkCore").Node.ToString()
		);
		await Assert.That(customizer).Contains("ModelCustomizer");
		await Assert.That(customizer).Contains("ConfigureValueObjects()");
	}

	[Test]
	public async Task ScalarEF_NonMappableProviderType_EmitsAutoConversionSkippedDiagnostic(
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

		var result = await GenerateAsync(source, ValueObjectsEFGeneratorTestOptions.Default, cancellationToken);

		await Assert.That(result).HasDiagnostic("VO1010");
	}

	[Test]
	public async Task ReferencedScalarValueObjects_AreMappedByConsumerRegistry(CancellationToken cancellationToken)
	{
		// A shared models assembly that references EF Core emits the marker interfaces and EF members.
		const string sharedSource = """
			namespace Shared
			{
				[Purview.ValueObjects.Serialization.Scalar]
				public readonly partial record struct EmailAddress
				{
					public string Value { get; }
				}

				[Purview.ValueObjects.Serialization.Scalar]
				public readonly partial record struct CustomerId
				{
					public System.Guid Value { get; }
				}
			}
			""";

		var sharedReference = await EmitSharedReferenceAsync(sharedSource, cancellationToken);

		const string consumerSource = """
			namespace Consumer
			{
				[Purview.ValueObjects.Serialization.Scalar]
				public readonly partial record struct LocalId
				{
					public System.Guid Value { get; }
				}
			}
			""";

		var result = await GenerateAsync(consumerSource, WithSharedReference(sharedReference), cancellationToken);

		var registry = Normalize(
			result.Generated().GetClass("ValueObjectEFExtensions", "Microsoft.EntityFrameworkCore").Node.ToString()
		);
		await Assert.That(registry).Contains("[typeof(global::Shared.EmailAddress)]");
		await Assert.That(registry).Contains("global::Shared.EmailAddress.EF.Converter");
		await Assert.That(registry).Contains("[typeof(global::Shared.CustomerId)]");
		await Assert.That(registry).Contains("global::Shared.CustomerId.EF.Converter");
	}

	[Test]
	public async Task ReferencedComplexValueObject_IsMappedAsComplexTypeByConsumerRegistry(
		CancellationToken cancellationToken
	)
	{
		const string sharedSource = """
			namespace Shared
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

		var sharedReference = await EmitSharedReferenceAsync(sharedSource, cancellationToken);

		const string consumerSource = """
			namespace Consumer
			{
				[Purview.ValueObjects.Serialization.ValueObject(EFMapping = Purview.ValueObjects.Serialization.EntityFrameworkMapping.Json)]
				public readonly partial record struct Audit
				{
					public System.DateTimeOffset OccurredAt { get; }
				}
			}
			""";

		var result = await GenerateAsync(consumerSource, WithSharedReference(sharedReference), cancellationToken);

		var registry = Normalize(
			result.Generated().GetClass("ValueObjectEFExtensions", "Microsoft.EntityFrameworkCore").Node.ToString()
		);
		await Assert.That(registry).Contains("typeof(global::Shared.Money)");
		await Assert.That(registry).Contains("global::Shared.CurrencyCode.EF.Converter");
	}

	[Test]
	public async Task ReferencedScalarValueObjects_WithoutEFInDeclaringAssembly_AreMappedWithInlineConversions(
		CancellationToken cancellationToken
	)
	{
		// A shared models assembly that does NOT reference EF Core (e.g. a domain project that must stay
		// EF-free) emits no markers and no EF members; the consumer discovers its value objects from their
		// attributes and maps them with inline conversions.
		const string sharedSource = """
			namespace Shared
			{
				[Purview.ValueObjects.Serialization.Scalar]
				public readonly partial record struct EmailAddress
				{
					public string Value { get; }
				}

				[Purview.ValueObjects.Serialization.Scalar]
				public readonly partial record struct CustomerId
				{
					public System.Guid Value { get; }
				}
			}
			""";

		var sharedReference = await EmitSharedReferenceWithoutEFAsync(sharedSource, cancellationToken);

		const string consumerSource = """
			namespace Consumer
			{
				[Purview.ValueObjects.Serialization.Scalar]
				public readonly partial record struct LocalId
				{
					public System.Guid Value { get; }
				}
			}
			""";

		var result = await GenerateAsync(consumerSource, WithSharedReference(sharedReference), cancellationToken);

		var registry = Normalize(
			result.Generated().GetClass("ValueObjectEFExtensions", "Microsoft.EntityFrameworkCore").Node.ToString()
		);
		await Assert.That(registry).Contains("[typeof(global::Shared.EmailAddress)]");
		await Assert.That(registry).Contains("ValueConverter<global::Shared.EmailAddress");
		await Assert.That(registry).Contains("v=>global::Shared.EmailAddress.Hydrate(v)");
		await Assert.That(registry).Contains("ValueComparer<global::Shared.EmailAddress>");
		await Assert.That(registry).DoesNotContain("global::Shared.EmailAddress.EF.Converter");
		await Assert.That(registry).Contains("[typeof(global::Shared.CustomerId)]");
		await Assert.That(registry).Contains("ValueConverter<global::Shared.CustomerId,global::System.Guid>");

		// The inline converter/comparer must compile against the referenced (EF-free) value objects.
		var compilationErrors = result
			.CompilationResult.Compilation.GetDiagnostics(cancellationToken)
			.Where(static d => d.Severity == DiagnosticSeverity.Error)
			.ToArray();
		await Assert.That(compilationErrors).IsEmpty();
	}

	[Test]
	public async Task ReferencedScalarValueObjects_StrictDeserializationWithoutEF_UsesCreateFactory(
		CancellationToken cancellationToken
	)
	{
		const string sharedSource = """
			[assembly: Purview.ValueObjects.Serialization.ValueObjectDefaults(DeserializationMode = Purview.ValueObjects.Serialization.ValueObjectDeserializationMode.Strict)]
			namespace Shared
			{
				[Purview.ValueObjects.Serialization.Scalar]
				public readonly partial record struct EmailAddress
				{
					public string Value { get; }
				}
			}
			""";

		var sharedReference = await EmitSharedReferenceWithoutEFAsync(sharedSource, cancellationToken);

		const string consumerSource = """
			namespace Consumer
			{
				[Purview.ValueObjects.Serialization.Scalar]
				public readonly partial record struct LocalId
				{
					public System.Guid Value { get; }
				}
			}
			""";

		var result = await GenerateAsync(consumerSource, WithSharedReference(sharedReference), cancellationToken);

		var registry = Normalize(
			result.Generated().GetClass("ValueObjectEFExtensions", "Microsoft.EntityFrameworkCore").Node.ToString()
		);
		await Assert.That(registry).Contains("v=>global::Shared.EmailAddress.Hydrate(v)");
		await Assert.That(registry).DoesNotContain("global::Shared.EmailAddress.Create(v)");
	}

	[Test]
	public async Task ReferencedComplexValueObjects_JsonMappingWithoutEF_EmitsInlineJsonConverter(
		CancellationToken cancellationToken
	)
	{
		const string sharedSource = """
			namespace Shared
			{
				[Purview.ValueObjects.Serialization.ValueObject(EFMapping = Purview.ValueObjects.Serialization.EntityFrameworkMapping.Json)]
				public readonly partial record struct Audit
				{
					public System.DateTimeOffset OccurredAt { get; }
				}
			}
			""";

		var sharedReference = await EmitSharedReferenceWithoutEFAsync(sharedSource, cancellationToken);

		const string consumerSource = """
			namespace Consumer
			{
				[Purview.ValueObjects.Serialization.Scalar]
				public readonly partial record struct LocalId
				{
					public System.Guid Value { get; }
				}
			}
			""";

		var result = await GenerateAsync(consumerSource, WithSharedReference(sharedReference), cancellationToken);

		var registry = Normalize(
			result.Generated().GetClass("ValueObjectEFExtensions", "Microsoft.EntityFrameworkCore").Node.ToString()
		);
		await Assert.That(registry).Contains("[typeof(global::Shared.Audit)]");
		await Assert.That(registry).Contains("ValueConverter<global::Shared.Audit,global::System.String>");
		await Assert.That(registry).Contains("JsonSerializer.Serialize(vo)");
		await Assert.That(registry).DoesNotContain("global::Shared.Audit.EF.Converter");

		var compilationErrors = result
			.CompilationResult.Compilation.GetDiagnostics(cancellationToken)
			.Where(static d => d.Severity == DiagnosticSeverity.Error)
			.ToArray();
		await Assert.That(compilationErrors).IsEmpty();
	}

	[Test]
	public async Task ReferencedComplexValueObjects_WithoutEF_AreMappedAsComplexTypes(
		CancellationToken cancellationToken
	)
	{
		const string sharedSource = """
			namespace Shared
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

		var sharedReference = await EmitSharedReferenceWithoutEFAsync(sharedSource, cancellationToken);

		const string consumerSource = """
			namespace Consumer
			{
				[Purview.ValueObjects.Serialization.Scalar]
				public readonly partial record struct LocalId
				{
					public System.Guid Value { get; }
				}
			}
			""";

		var result = await GenerateAsync(consumerSource, WithSharedReference(sharedReference), cancellationToken);

		var registry = Normalize(
			result.Generated().GetClass("ValueObjectEFExtensions", "Microsoft.EntityFrameworkCore").Node.ToString()
		);
		await Assert.That(registry).Contains("typeof(global::Shared.Money)");
		await Assert.That(registry).Contains("ValueConverter<global::Shared.CurrencyCode");
		await Assert.That(registry).DoesNotContain("global::Shared.CurrencyCode.EF.Converter");
	}

	[Test]
	public async Task ReferencedValueObjects_OptedOutOfEF_AreNotMappedByConsumerRegistry(
		CancellationToken cancellationToken
	)
	{
		const string sharedSource = """
			namespace Shared
			{
				[Purview.ValueObjects.Serialization.Scalar(GenerateEFConverter = false, GenerateEFComparer = false)]
				public readonly partial record struct InternalCode
				{
					public string Value { get; }
				}
			}
			""";

		var sharedReference = await EmitSharedReferenceAsync(sharedSource, cancellationToken);

		const string consumerSource = """
			namespace Consumer
			{
				[Purview.ValueObjects.Serialization.Scalar]
				public readonly partial record struct LocalId
				{
					public System.Guid Value { get; }
				}
			}
			""";

		var result = await GenerateAsync(consumerSource, WithSharedReference(sharedReference), cancellationToken);

		var registry = Normalize(
			result.Generated().GetClass("ValueObjectEFExtensions", "Microsoft.EntityFrameworkCore").Node.ToString()
		);
		await Assert.That(registry).DoesNotContain("Shared.InternalCode");
	}

	[Test]
	public async Task EFRegistry_DisabledViaMSBuildProperty_StillEmitsPerTypeEFMembers(
		CancellationToken cancellationToken
	)
	{
		ValueObjectsEFGeneratorTestOptions options = new()
		{
			AnalyzerConfigOptions = ImmutableDictionary<string, string>.Empty.Add(
				"build_property.DisableValueObjectsEFRegistry",
				"true"
			),
		};

		var result = await GenerateAsync(ScalarSource, options, cancellationToken);

		var query = result.Generated();
		await Assert.That(query.HasClass("EF")).IsTrue();
		await Assert.That(query.HasClass("ValueObjectEFExtensions", "Microsoft.EntityFrameworkCore")).IsFalse();
		await Assert.That(query.HasClass("ValueObjectModelCustomizer", "Microsoft.EntityFrameworkCore")).IsFalse();
	}

	async Task<MetadataReference> EmitSharedReferenceAsync(string source, CancellationToken cancellationToken)
	{
		// A value object provider assembly should not emit its own registry; consumers map its types.
		ValueObjectsEFGeneratorTestOptions sharedOptions = new()
		{
			AnalyzerConfigOptions = ImmutableDictionary<string, string>.Empty.Add(
				"build_property.DisableValueObjectsEFRegistry",
				"true"
			),
		};

		using var sharedResult = await GenerateAsync(source, sharedOptions.Compile(), cancellationToken);
		using MemoryStream stream = new();
		var emitResult = sharedResult.CompilationResult.Compilation.Emit(stream, cancellationToken: cancellationToken);
		await Assert.That(emitResult.Success).IsTrue();

		return MetadataReference.CreateFromImage(stream.ToArray());
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Design",
		"CA1506:Avoid excessive class coupling",
		Justification = "Emitting a value object provider assembly without Entity Framework requires Roslyn types."
	)]
	async Task<MetadataReference> EmitSharedReferenceWithoutEFAsync(string source, CancellationToken cancellationToken)
	{
		// Reuse the framework to obtain the full reference set, then drop Entity Framework so the value
		// object provider assembly is generated without any EF members or marker interfaces.
		var probe = await GenerateAsync(source, ValueObjectsEFGeneratorTestOptions.Default, cancellationToken);
		var references = probe
			.CompilationResult.Compilation.References.Where(static reference =>
				!IsEntityFrameworkReference(reference.Display)
			)
			.ToImmutableArray();

		var tree = CSharpSyntaxTree.ParseText(
			source,
			new CSharpParseOptions(LanguageVersion.Latest),
			cancellationToken: cancellationToken
		);
		var compilation = CSharpCompilation.Create(
			"SharedModelsWithoutEF",
			[tree],
			references,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
		);

		var generator = new ValueObjectSourceGenerator().AsSourceGenerator();
		GeneratorDriver driver = CSharpGeneratorDriver.Create(
			generators: [generator],
			parseOptions: new CSharpParseOptions(LanguageVersion.Latest)
		);
		driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _, cancellationToken);

		using MemoryStream stream = new();
		var emitResult = outputCompilation.Emit(stream, cancellationToken: cancellationToken);
		await Assert.That(emitResult.Success).IsTrue();

		return MetadataReference.CreateFromImage(stream.ToArray());
	}

	static bool IsEntityFrameworkReference(string? display) =>
		display?.Contains("Microsoft.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase) is true;

	static ValueObjectsEFGeneratorTestOptions WithSharedReference(MetadataReference reference) =>
		ValueObjectsEFGeneratorTestOptions.Default with
		{
			AdditionalReferences = [.. ValueObjectsEFGeneratorTestOptions.Default.AdditionalReferences, reference],
		};

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
