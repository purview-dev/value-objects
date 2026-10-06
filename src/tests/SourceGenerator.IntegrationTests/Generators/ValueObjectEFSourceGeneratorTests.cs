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
		await Assert.That(converterInitializer).Contains("= new ValueObjectConverter()");

		// The conversion expressions live inside the generated converter class.
		var generatedRecord = Normalize(emailAddress.Node.ToString());
		await Assert.That(generatedRecord).Contains("Testing.EmailAddress.Hydrate(v)");
	}

	[Test]
	public async Task ScalarEFGeneration_ConverterAcceptsProviderShapedValues(CancellationToken cancellationToken)
	{
		// Arrange
		var result = await GenerateAsync(ScalarSource, ValueObjectsEFGeneratorTestOptions.Default, cancellationToken);

		// Act
		var emailAddress = Normalize(result.Generated().GetRecord("EmailAddress", "Testing").Node.ToString());

		// Assert — Entity Framework Core hands the raw provider value to the property's converter when a
		// query compares the converted property to the underlying primitive (dotnet/efcore#32030), so the
		// generated converter accepts either shape. It also exposes the members Entity Framework Core's
		// design-time generator probes, so a compiled model rebuilds this converter type.
		await Assert
			.That(emailAddress)
			.Contains(
				"classValueObjectConverter:global::Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<global::Testing.EmailAddress,string>"
			);
		await Assert.That(emailAddress).Contains("ConvertToProvider=>ConvertToProviderValue");
		await Assert.That(emailAddress).Contains("ConvertFromProvider=>ConvertFromProviderValue");
		await Assert.That(emailAddress).Contains("valueisglobal::Testing.EmailAddressmodel");
		await Assert
			.That(emailAddress)
			.Contains("global::System.Enum.GetUnderlyingType(value.GetType())==providerType");
		await Assert.That(emailAddress).Contains("ComposeWith");
		await Assert
			.That(emailAddress)
			.Contains(
				"JsonReaderWriter=>global::Microsoft.EntityFrameworkCore.Storage.Json.JsonStringReaderWriter.Instance"
			);
		await Assert.That(emailAddress).Contains("newValueObjectConverter()");
	}

	[Test]
	public async Task ScalarEFGeneration_EnumBackedScalar_ConvertsThroughItsIntegralProvider(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
				public enum OrderStatusKind
				{
					Pending,
					Shipped,
				}

				[Purview.ValueObjects.Serialization.Scalar]
				public readonly partial record struct OrderStatus
				{
					public OrderStatusKind Value { get; }
				}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsEFGeneratorTestOptions.Default, cancellationToken);

		var record = Normalize(result.Generated().GetRecord("OrderStatus", "Testing").Node.ToString());

		// The enum's underlying integral type is used as the provider type: keeping the enum makes Entity
		// Framework Core compose its own enum-to-number converter with the generated one, and the composite
		// loses the provider tolerance.
		await Assert.That(record).Contains("ValueConverter<global::Testing.OrderStatus,int>");
		await Assert.That(record).Contains("vo=>(int)vo.Value");
		await Assert.That(record).Contains("Hydrate((global::Testing.OrderStatusKind)v)");
		await Assert
			.That(record)
			.Contains(
				"JsonReaderWriter=>global::Microsoft.EntityFrameworkCore.Storage.Json.JsonInt32ReaderWriter.Instance"
			);
	}

	[Test]
	public async Task EFRegistry_PlainEnumProperties_AreNotConverted(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
				public enum TenantKind
				{
					Organisation,
					Project,
				}

				[Purview.ValueObjects.Serialization.Scalar]
				public readonly partial record struct EmailAddress
				{
					public string Value { get; }
				}

				public sealed class Customer
				{
					public EmailAddress Email { get; set; }

					public TenantKind Kind { get; set; }
				}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsEFGeneratorTestOptions.Default, cancellationToken);

		var query = result.Generated();
		var registry = Normalize(
			query.GetClass("ValueObjectEFExtensions", "Microsoft.EntityFrameworkCore").Node.ToString()
		);

		// Only [Scalar]/[ValueObject] types receive a conversion. A plain enum keeps Entity Framework Core's
		// own enum mapping, so the registry neither converts nor even mentions the enum type.
		await Assert.That(registry).Contains("Testing.EmailAddress");
		await Assert.That(registry).DoesNotContain("TenantKind");

		var compilation = result.CompilationResult.Compilation;
		var tenantKind = compilation.GetTypeByMetadataName("Testing.TenantKind")!;
		await Assert.That(tenantKind.GetTypeMembers("EF")).IsEmpty();
		await Assert
			.That(tenantKind.AllInterfaces.Any(static i => i.Name is "IEFScalarValueObject" or "IEFComplexValueObject"))
			.IsFalse();
	}

	[Test]
	public async Task ScalarEFGeneration_GuidBackedInitOnlyProperty_UsesGuidProvider(
		CancellationToken cancellationToken
	)
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
		var result = await GenerateAsync(
			source,
			ValueObjectsEFGeneratorTestOptions.Default.Compile(),
			cancellationToken
		);

		// Act
		var query = result.Generated();
		var emailAddress = query.GetRecord("EmailAddress", "Testing");
		var customerId = query.GetRecord("CustomerId", "Testing");
		var emailGeneratedText = Normalize(emailAddress.Node.ToString());
		var customerGeneratedText = Normalize(customerId.Node.ToString());

		// Assert
		await Assert.That(emailAddress.Node.BaseList?.ToString()).Contains("IEFScalarValueObject");
		await Assert.That(customerId.Node.BaseList?.ToString()).Contains("IEFScalarValueObject");
		await Assert.That(emailGeneratedText).Contains("ValueConverter<global::Testing.EmailAddress,string>");
		await Assert.That(emailGeneratedText).Contains("Testing.EmailAddress.Hydrate(v)");
		await Assert
			.That(customerGeneratedText)
			.Contains("ValueConverter<global::Testing.CustomerId,global::System.Guid>");
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

		var emailAddress = Normalize(result.Generated().GetRecord("EmailAddress", "Testing").Node.ToString());

		// The provider-to-model path hydrates: Entity Framework Core uses it for persisted rows and query
		// parameters, which may not satisfy a strict Create(...) factory.
		await Assert.That(emailAddress).Contains("Testing.EmailAddress.Hydrate(v)");
		await Assert.That(emailAddress).DoesNotContain("Testing.EmailAddress.Create(v)");
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

		var moneyRecord = Normalize(result.Generated().GetRecord("Money", "Testing").Node.ToString());
		await Assert.That(moneyRecord).Contains("JsonSerializer.Serialize(vo)");
		await Assert.That(moneyRecord).Contains("JsonStringReaderWriter.Instance");
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

	/// <summary>
	/// The registry configures a property through the entity type that owns it rather than the property's
	/// declaring type. An inherited property is declared by a base type that is usually not an entity type at
	/// all, and asking the model builder for that base type adds an entity type to the model while the
	/// registry is still enumerating it, which throws <c>InvalidOperationException</c> at model creation.
	/// </summary>
	[Test]
	public async Task EFRegistry_GivenInheritedScalarValueObjectProperty_MapsThroughTheOwningEntityType(
		CancellationToken cancellationToken
	)
	{
		// Arrange — the value object column is declared by a base type that is not part of the model.
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar]
				public readonly partial record struct UserId
				{
					public System.Guid Value { get; }
				}

				public abstract class AuditedEntity
				{
					public UserId CreatedById { get; set; }
				}

				public sealed class Order : AuditedEntity
				{
					public System.Guid Id { get; set; }
				}
			}
			""";

		// Act
		var result = await GenerateAsync(
			source,
			ValueObjectsEFGeneratorTestOptions.Default.Compile(),
			cancellationToken
		);

		var registryText = Normalize(
			result.Generated().GetClass("ValueObjectEFExtensions", "Microsoft.EntityFrameworkCore").Node.ToString()
		);

		// Assert — the conversion is declared on the entity type being configured, and the entity types are
		// snapshotted so configuring a property cannot invalidate the enumeration.
		await Assert.That(registryText).Contains("modelBuilder.Entity(entityType.ClrType!).Property(");
		await Assert.That(registryText).Contains("GetEntityTypes().ToList()");

		// The inherited property must not be configured against its declaring type: the base type is not
		// part of the model. Asserted against the configuration call specifically — the mappability
		// predicate also consults DeclaringType, to locate a compiler-generated backing field, and that is
		// unrelated to which entity type the conversion is declared on.
		await Assert.That(registryText).DoesNotContain("modelBuilder.Entity(property.DeclaringType");
		await Assert.That(registryText).DoesNotContain("Entity(property.DeclaringType!)");
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
		var registry = Normalize(
			query.GetClass("ValueObjectEFExtensions", "Microsoft.EntityFrameworkCore").Node.ToString()
		);

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
		await Assert.That(query.HasClass("ValueObjectConverter", "Microsoft.EntityFrameworkCore")).IsFalse();
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
		await Assert.That(registry).Contains("ValueConverter<global::Shared.EmailAddress,string>");
		await Assert.That(registry).Contains("_Shared_CustomerIdConverter");
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
		await Assert.That(registry).Contains("JsonStringReaderWriter.Instance");
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

		// The per-type converters are still built from the provider-tolerant converter even though the
		// assembly-level registry is not emitted.
		await Assert.That(query.HasClass("ValueObjectConverter", "Testing")).IsTrue();
	}

	async Task<MetadataReference> EmitStubAsync(string source, CancellationToken cancellationToken)
	{
		// Compiles a stub assembly against the same reference set the test framework uses, so the stub only
		// adds the types it declares.
		var probe = await GenerateAsync(
			"namespace Probe { }",
			ValueObjectsEFGeneratorTestOptions.Default,
			cancellationToken
		);
		var tree = CSharpSyntaxTree.ParseText(
			source,
			new CSharpParseOptions(LanguageVersion.Latest),
			cancellationToken: cancellationToken
		);
		var compilation = CSharpCompilation.Create(
			"EntityFrameworkCore7Stub",
			[tree],
			probe.CompilationResult.Compilation.References,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
		);

		using MemoryStream stream = new();
		var emitResult = compilation.Emit(stream, cancellationToken: cancellationToken);
		await Assert.That(emitResult.Success).IsTrue();

		return MetadataReference.CreateFromImage(stream.ToArray());
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

	[Test]
	public async Task ScalarGeneration_GivenGenerateEFValueGenerator_EmitsGeneratorAndRegistersConvention(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar(GenerateEFValueGenerator = true)]
				public readonly partial record struct TenantId
				{
					public System.Guid Value { get; }
				}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsEFGeneratorTestOptions.Default, cancellationToken);
		var generated = result.Generated();

		// The value object's EF class carries the generator pair.
		var tenantIdText = generated.GetRecord("TenantId", "Testing").Node.ToString();
		await Assert.That(tenantIdText).Contains("ValueGeneratorFactory");
		await Assert.That(tenantIdText).Contains("ValueGenerator");
		await Assert
			.That(tenantIdText)
			.Contains("Hydrate(global::Microsoft.EntityFrameworkCore.ValueObjectSequentialGuid.NewGuid())");

		// The registry registers the convention, which maps the value object to its factory.
		var registryText = generated
			.GetClass("ValueObjectEFExtensions", "Microsoft.EntityFrameworkCore")
			.Node.ToString();
		await Assert.That(registryText).Contains("UseValueObjectKeyGenerators");

		var conventionText = generated
			.GetClass("ValueObjectKeyValueGeneratorConvention", "Microsoft.EntityFrameworkCore")
			.Node.ToString();
		await Assert
			.That(Normalize(conventionText))
			.Contains("typeof(global::Testing.TenantId.EF.ValueGeneratorFactory)");
		await Assert
			.That(generated.GetClass("ValueObjectSequentialGuid", "Microsoft.EntityFrameworkCore").Node)
			.IsNotNull();
	}

	[Test]
	public async Task ScalarGeneration_GivenGenerateEFValueGenerator_EmitsBothOrderingStrategies(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar(GenerateEFValueGenerator = true)]
				public readonly partial record struct TenantId
				{
					public System.Guid Value { get; }
				}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsEFGeneratorTestOptions.Default, cancellationToken);
		var generated = result.Generated();

		// The value object's EF class carries a generator pair per ordering strategy.
		var tenantIdText = generated.GetRecord("TenantId", "Testing").Node.ToString();
		await Assert.That(tenantIdText).Contains("ValueGeneratorFactory");
		await Assert.That(tenantIdText).Contains("SqlServerValueGeneratorFactory");
		await Assert
			.That(tenantIdText)
			.Contains("Hydrate(global::Microsoft.EntityFrameworkCore.ValueObjectSequentialGuid.NewGuid())");
		await Assert
			.That(tenantIdText)
			.Contains("Hydrate(global::Microsoft.EntityFrameworkCore.ValueObjectSequentialGuid.NewSqlServerGuid())");

		// The registry selects a strategy through the emitted enum, and the default overloads onto it.
		await Assert.That(generated.HasEnum("ValueObjectKeyOrdering", "Microsoft.EntityFrameworkCore")).IsTrue();

		var enumText = Normalize(
			generated.GetEnum("ValueObjectKeyOrdering", "Microsoft.EntityFrameworkCore").Node.ToString()
		);
		await Assert.That(enumText).Contains("UuidV7=0,");
		await Assert.That(enumText).Contains("SqlServer=1,");

		var registry = Normalize(
			generated.GetClass("ValueObjectEFExtensions", "Microsoft.EntityFrameworkCore").Node.ToString()
		);
		await Assert.That(registry).Contains("UseValueObjectKeyGenerators(ValueObjectKeyOrdering.UuidV7)");

		// The helper exposes the SQL Server strategy, its readers, and its range bounds.
		var helper = Normalize(
			generated.GetClass("ValueObjectSequentialGuid", "Microsoft.EntityFrameworkCore").Node.ToString()
		);
		await Assert.That(helper).Contains("NewSqlServerGuid()=>NewSqlServerGuid(");
		await Assert.That(helper).Contains("TryGetSqlServerTimestamp");
		await Assert.That(helper).Contains("MinSqlServerGuidFor");
		await Assert.That(helper).Contains("MaxSqlServerGuidFor");

		// The convention chooses its factory table when it is constructed.
		var convention = Normalize(
			generated
				.GetClass("ValueObjectKeyValueGeneratorConvention", "Microsoft.EntityFrameworkCore")
				.Node.ToString()
		);
		await Assert.That(convention).Contains("typeof(global::Testing.TenantId.EF.SqlServerValueGeneratorFactory)");
		await Assert.That(convention).Contains("?SqlServerFactories:Factories");
	}

	[Test]
	public async Task ScalarGeneration_GivenGenerateEFValueGenerator_ExposesTheIdentifierHelperPublicly(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar(GenerateEFValueGenerator = true)]
				public readonly partial record struct TenantId
				{
					public System.Guid Value { get; }
				}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsEFGeneratorTestOptions.Default, cancellationToken);

		// The helper is public so application code in another assembly can mint the key a value object should
		// be saved with, before the round trip that would otherwise have generated it.
		var helper = result.CompilationResult.Compilation.GetTypeByMetadataName(
			"Microsoft.EntityFrameworkCore.ValueObjectSequentialGuid"
		);

		await Assert.That(helper).IsNotNull();
		await Assert.That(helper!.DeclaredAccessibility).IsEqualTo(Accessibility.Public);

		string[] methodNames =
		[
			"NewGuid",
			"NewSqlServerGuid",
			"TryGetTimestamp",
			"TryGetSqlServerTimestamp",
			"MinSqlServerGuidFor",
			"MaxSqlServerGuidFor",
		];
		foreach (var methodName in methodNames)
		{
			var method = helper.GetMembers(methodName).OfType<IMethodSymbol>().FirstOrDefault();

			await Assert.That(method).IsNotNull();
			await Assert.That(method!.DeclaredAccessibility).IsEqualTo(Accessibility.Public);
			await Assert.That(method.IsStatic).IsTrue();
		}
	}

	[Test]
	public async Task ScalarGeneration_GivenAssemblyDefaultGenerateEFValueGenerator_EmitsGenerator(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			[assembly: Purview.ValueObjects.Serialization.ValueObjectDefaults(GenerateEFValueGenerator = true)]

			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar]
				public readonly partial record struct TenantId
				{
					public System.Guid Value { get; }
				}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsEFGeneratorTestOptions.Default, cancellationToken);

		await Assert
			.That(result.Generated().GetRecord("TenantId", "Testing").Node.ToString())
			.Contains("ValueGeneratorFactory");
	}

	[Test]
	public async Task ScalarGeneration_GivenGenerateEFValueGeneratorOnNonGuidScalar_ReportsValueGenerationUnavailable(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar(GenerateEFValueGenerator = true)]
				public readonly partial record struct CurrencyCode
				{
					public string Value { get; }
				}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsEFGeneratorTestOptions.Default, cancellationToken);

		await Assert.That(result).HasDiagnostic("VO1021");
		await Assert
			.That(result.Generated().GetRecord("CurrencyCode", "Testing").Node.ToString())
			.DoesNotContain("ValueGeneratorFactory");
	}

	[Test]
	public async Task ScalarGeneration_GivenGenerateEFValueGeneratorWithoutConverter_ReportsValueGenerationUnavailable(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar(GenerateEFValueGenerator = true, GenerateEFConverter = false)]
				public readonly partial record struct TenantId
				{
					public System.Guid Value { get; }
				}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsEFGeneratorTestOptions.Default, cancellationToken);

		await Assert.That(result).HasDiagnostic("VO1021");
	}

	[Test]
	public async Task ComplexGeneration_GivenJsonMappingWithoutJsonConverter_ReportsJsonMappingRequiresJsonConverter(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.ValueObject(
					EFMapping = Purview.ValueObjects.Serialization.EntityFrameworkMapping.Json,
					GenerateJsonConverter = false
				)]
				public readonly partial record struct Audit
				{
					public System.DateTimeOffset OccurredAt { get; }
				}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsEFGeneratorTestOptions.Default, cancellationToken);

		await Assert.That(result).HasDiagnostic("VO1017");
	}

	[Test]
	public async Task ComplexGeneration_GivenCollectionMember_ReportsUnsupportedComplexMappingMember(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.ValueObject]
				public readonly partial record struct Audit
				{
					public System.Collections.Generic.IReadOnlyList<string> Entries { get; }
				}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsEFGeneratorTestOptions.Default, cancellationToken);

		await Assert.That(result).HasDiagnostic("VO1018");
	}

	[Test]
	public async Task ComplexGeneration_GivenMemberEntityFrameworkCannotConvert_ReportsUnsupportedComplexMappingMember(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
				public readonly record struct PartialDate(int Year, int Month);

				[Purview.ValueObjects.Serialization.ValueObject]
				public readonly partial record struct Audit
				{
					public PartialDate When { get; }
				}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsEFGeneratorTestOptions.Default, cancellationToken);

		await Assert.That(result).HasDiagnostic("VO1018");
	}

	[Test]
	public async Task ComplexGeneration_GivenSupportedMembers_DoesNotReportUnsupportedComplexMappingMember(
		CancellationToken cancellationToken
	)
	{
		// Entity Framework Core 8+ is referenced by the test project, so the complex-type mapping is
		// honoured and every member converts: a value object, a primitive, a string, and an enum.
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar]
				public readonly partial record struct CurrencyCode
				{
					public string Value { get; }
				}

				public enum Kind
				{
					One,
				}

				[Purview.ValueObjects.Serialization.ValueObject]
				public readonly partial record struct Money
				{
					public decimal Amount { get; }

					public CurrencyCode Currency { get; }

					public Kind Kind { get; }

					public System.DateTimeOffset? RecordedAt { get; }
				}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsEFGeneratorTestOptions.Default, cancellationToken);

		await Assert.That(result).DoesNotHaveDiagnostic("VO1018");
		await Assert.That(result).HasNoErrorDiagnostics();
	}

	[Test]
	public async Task ComplexGeneration_GivenEntityFrameworkBelow8_ReportsEntityFramework8Requirement(
		CancellationToken cancellationToken
	)
	{
		// Declaring Microsoft.EntityFrameworkCore.Metadata.IComplexType in a second assembly makes the
		// EF Core 8 type ambiguous, which is how the compilation resolves an EF Core 7 reference set. The
		// complex-type mapping needs the EF Core 8 API, so the generator reports VO1019 and leaves it out.
		var ef7Stub = await EmitStubAsync(
			"""
			namespace Microsoft.EntityFrameworkCore.Metadata
			{
				public interface IComplexType
				{
				}
			}
			""",
			cancellationToken
		);

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

				[Purview.ValueObjects.Serialization.ValueObject(
					EFMapping = Purview.ValueObjects.Serialization.EntityFrameworkMapping.Json
				)]
				public readonly partial record struct AuditStamp
				{
					public System.DateTimeOffset RecordedAt { get; }
				}
			}
			""";

		var result = await GenerateAsync(source, WithSharedReference(ef7Stub), cancellationToken);

		// The complex-type mapping needs the Entity Framework Core 8 API, so the generator reports VO1019
		// and the reference set describes what is missing.
		await Assert.That(result).HasDiagnostic("VO1019");

		// The generated registry must not reach for EF Core 8 APIs the reference set lacks: the mapping
		// list, the complex-property block, and the compiled-model JSON reader/writer.
		var generated = result.Generated();
		var registry = Normalize(
			generated.GetClass("ValueObjectEFExtensions", "Microsoft.EntityFrameworkCore").Node.ToString()
		);
		await Assert.That(registry).DoesNotContain("complexTypeMappings");
		await Assert.That(registry).DoesNotContain("ComplexProperty");
		await Assert.That(registry).DoesNotContain("JsonReaderWriter");

		// A JSON-mapped value object still receives its converter, minus the compiled-model member.
		var auditStamp = Normalize(generated.GetRecord("AuditStamp", "Testing").Node.ToString());
		await Assert.That(auditStamp).Contains("ValueConverter<global::Testing.AuditStamp,global::System.String>");
		await Assert.That(auditStamp).DoesNotContain("JsonReaderWriter");

		await Assert.That(result).HasNoErrorDiagnostics();
	}

	[Test]
	public async Task ComplexGeneration_GivenJsonMappingAndEntityFramework8_ExposesCompiledModelReaderWriter(
		CancellationToken cancellationToken
	)
	{
		// The counterpart of the Entity Framework Core 7 case: with EF Core 8 or later referenced the
		// generated converter exposes the reader/writer a compiled model rebuilds it from.
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.ValueObject(
					EFMapping = Purview.ValueObjects.Serialization.EntityFrameworkMapping.Json
				)]
				public readonly partial record struct AuditStamp
				{
					public System.DateTimeOffset RecordedAt { get; }
				}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsEFGeneratorTestOptions.Default, cancellationToken);

		var auditStamp = Normalize(result.Generated().GetRecord("AuditStamp", "Testing").Node.ToString());
		await Assert.That(auditStamp).Contains("JsonReaderWriter");
		await Assert.That(auditStamp).Contains("JsonValueReaderWriter");
		await Assert.That(result).HasNoErrorDiagnostics();
	}

	static string Normalize(string source) => string.Concat(source.Where(static c => !char.IsWhiteSpace(c)));

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
