using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.ValueObjects.SourceGenerator.ValueObject;

static class ComplexValueObjectModelBuilder
{
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Design",
		"CA1506:Avoid excessive class coupling",
		Justification = "Value object model construction couples many value types."
	)]
	public static GeneratorResult<ComplexValueObjectModel> Build(
		INamedTypeSymbol typeSymbol,
		TypeDeclarationSyntax syntax,
		Compilation compilation,
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (typeSymbol is null || syntax is null)
			return GeneratorResult<ComplexValueObjectModel>.Empty;

		var location = syntax.GetLocation();
		List<ReportableDiagnostic> diagnosticsList =
		[
			.. ValueObjectSymbolInspector.ValidateValueObjectType(typeSymbol, "ValueObject", location),
		];

		var attributes = typeSymbol.GetAttributes();
		if (ScalarAttributeParser.Find(attributes) is not null)
		{
			diagnosticsList.Add(
				ReportableDiagnostic.Create(
					DiagnosticLibrary.ConflictingValueObjectAttributes,
					isBlocking: true,
					location,
					typeSymbol.Name
				)
			);
			return GeneratorResult<ComplexValueObjectModel>.Create([.. diagnosticsList]);
		}

		var assemblyDefaults = ValueObjectDefaultsAttributeData.FromAttributeData(compilation.Assembly.GetAttributes());
		var valueObjectOptions = ValueObjectDefaultsHelper.Apply(
			ValueObjectAttributeData.FromAttributeData(attributes),
			assemblyDefaults,
			attributes
		);

		var typeModel = ValueObjectSymbolInspector.BuildTypeModel(typeSymbol);
		if (typeModel is null)
			return GeneratorResult<ComplexValueObjectModel>.Create([.. diagnosticsList]);

		var properties = typeSymbol
			.GetMembers()
			.OfType<IPropertySymbol>()
			.Where(property =>
				!property.IsStatic
				&& !property.IsIndexer
				&& property.GetMethod is not null
				&& ValueObjectSymbolInspector.IsValueObjectPropertyCandidate(typeSymbol, property)
				&& SymbolEqualityComparer.Default.Equals(property.ContainingType, typeSymbol)
			)
			.OrderBy(property => property.Locations.FirstOrDefault()?.SourceSpan.Start ?? int.MaxValue)
			.ToArray();

		var ctorExists = typeSymbol
			.Constructors.Where(static ctor => !ctor.IsStatic)
			.Any(ctor => ValueObjectSymbolInspector.ConstructorMatches(ctor, properties));

		var createExists = ValueObjectSymbolInspector.HasStaticFactory(
			typeSymbol,
			"Create",
			[.. properties.Select(property => property.Type)]
		);

		var propertyModels = BuildPropertyModels(
			typeSymbol,
			properties,
			ValueObjectSymbolInspector.HasDeclaredStaticFactory(
				typeSymbol,
				"Create",
				[.. properties.Select(property => property.Type)]
			),
			assemblyDefaults,
			location,
			diagnosticsList
		);

		var hydrateExists = ValueObjectSymbolInspector.HasStaticFactory(
			typeSymbol,
			"Hydrate",
			[.. properties.Select(property => property.Type)]
		);
		var compareToSelfExists = ValueObjectSymbolInspector.HasInstanceMethod(typeSymbol, "CompareTo", [typeSymbol]);
		var compareToObjectExists = ValueObjectSymbolInspector.HasCompareToObject(typeSymbol);
		var isReferenceType = typeSymbol.TypeKind == TypeKind.Class;
		var equalsSelfExists =
			typeSymbol.IsRecord || ValueObjectSymbolInspector.HasInstanceMethod(typeSymbol, "Equals", [typeSymbol]);
		// IEquatable<TSelf> is added for every shape that does not already implement it (records synthesize
		// it). When the author declared Equals(TSelf), the interface is only added if their parameter is
		// nullability-compatible; a non-nullable reference parameter would report CS8767.
		var implementsSelfEquatable = ValueObjectSymbolInspector.ImplementsSelfEquatable(typeSymbol);
		var mirrorIEquatableSelf =
			!implementsSelfEquatable
			&& (!equalsSelfExists || ValueObjectSymbolInspector.HasNullabilityCompatibleEquals(typeSymbol, typeSymbol));
		var equalsObjectExists = ValueObjectSymbolInspector.HasEqualsObject(typeSymbol);
		var getHashCodeExists = ValueObjectSymbolInspector.HasParameterlessMethod(typeSymbol, "GetHashCode");
		var equalityOperatorExists =
			typeSymbol.IsRecord
			|| ValueObjectSymbolInspector.HasBinaryOperator(typeSymbol, "op_Equality", [typeSymbol, typeSymbol]);
		var inequalityOperatorExists =
			typeSymbol.IsRecord
			|| ValueObjectSymbolInspector.HasBinaryOperator(typeSymbol, "op_Inequality", [typeSymbol, typeSymbol]);
		var hasJsonConverterAttribute = ValueObjectSymbolInspector.HasAttribute(
			typeSymbol,
			TypeLibrary.System.Text.Json.Serialization.JsonConverterAttribute
		);
		var declareOnNormalize = ValueObjectSymbolInspector.ShouldEmitComplexHookDeclaration(
			typeSymbol,
			"OnNormalize",
			properties.Length,
			includeRef: true
		);
		var declareOnValidate = ValueObjectSymbolInspector.ShouldEmitComplexHookDeclaration(
			typeSymbol,
			"OnValidate",
			properties.Length
		);
		var validateHookIsReadOnly =
			typeSymbol.TypeKind == TypeKind.Struct
			&& ValueObjectSymbolInspector.IsComplexHookReadOnly(typeSymbol, "OnValidate", properties.Length);

		var parameterlessCtorExists = typeSymbol
			.Constructors.Where(static ctor => !ctor.IsStatic)
			.Any(ctor => ctor.Parameters.Length == 0 && !ctor.IsImplicitlyDeclared);

		var hydrateFactoryName =
			valueObjectOptions.DeserializationMode == ValueObjectSymbolInspector.StrictModeName ? "Create" : "Hydrate";

		var efConstructorArguments = ValueObjectSymbolInspector.TryGetEFConstructorArguments(
			typeSymbol,
			properties,
			out var efCtorArgs
		)
			? efCtorArgs
			: null;

		var hintName = ValueObjectSymbolInspector.BuildHintName(typeSymbol, "ComplexValueObject");

		var zodSchema = ValueObjectSymbolInspector.ResolveZodSchemaIntegration(typeSymbol);
		ValueObjectSymbolInspector.CollectZodSchemaDiagnostics(
			typeSymbol,
			zodSchema,
			valueObjectOptions.ZodSchemaMode,
			onValidateImplemented: ValueObjectSymbolInspector.HasHookImplementation(
				typeSymbol,
				"OnValidate",
				properties.Length
			),
			diagnosticsList
		);
		ValueObjectSymbolInspector.CollectMutableMemberDiagnostics(typeSymbol, properties, diagnosticsList);

		var isEFReferenced = ValueObjectSymbolInspector.IsEFReferenced(compilation);
		CollectEFMappingDiagnostics(
			typeSymbol,
			properties,
			valueObjectOptions,
			compilation,
			isEFReferenced,
			diagnosticsList
		);
		if (
			!isEFReferenced
			&& (
				ValueObjectDefaultsHelper.IsPropertyExplicitlySet(
					attributes,
					TypeLibrary.Purview.ValueObjects.Serialization.ValueObjectAttribute,
					"EFMapping"
				)
				|| ValueObjectDefaultsHelper.IsPropertyExplicitlySet(
					attributes,
					TypeLibrary.Purview.ValueObjects.Serialization.ValueObjectAttribute,
					"GenerateEFComparer"
				)
			)
		)
		{
			diagnosticsList.Add(
				ReportableDiagnostic.Create(
					DiagnosticLibrary.EFMappingRequiresEntityFramework,
					isBlocking: false,
					location,
					typeSymbol.Name
				)
			);
		}

		var emptyArguments = ImmutableArray.CreateBuilder<string>(properties.Length);
		foreach (var property in properties)
			emptyArguments.Add(ValueObjectSymbolInspector.GetEmptyValueExpression(property.Type));

		ComplexValueObjectModel model = new(
			typeModel.Value,
			propertyModels,
			valueObjectOptions,
			ctorExists,
			hintName,
			typeModel.Value.FullyQualifiedName,
			isReferenceType,
			typeSymbol.TypeKind == TypeKind.Struct,
			typeSymbol.IsRecord,
			typeSymbol.IsReadOnly,
			typeSymbol.DeclaredAccessibility.ToTypeDeclarationAccessibility(),
			implementsSelfEquatable,
			mirrorIEquatableSelf,
			hydrateExists,
			createExists,
			compareToSelfExists,
			compareToObjectExists,
			equalsSelfExists,
			equalsObjectExists,
			getHashCodeExists,
			equalityOperatorExists,
			inequalityOperatorExists,
			hasJsonConverterAttribute,
			declareOnNormalize,
			declareOnValidate,
			validateHookIsReadOnly,
			ValueObjectSymbolInspector.HasMemberWithName(typeSymbol, "Empty"),
			emptyArguments.ToImmutable(),
			parameterlessCtorExists,
			efConstructorArguments,
			hydrateFactoryName,
			BuildExistingRelationalOperators(
				typeSymbol,
				typeModel.Value.FullyQualifiedName,
				typeModel.Value.FullyQualifiedName
			),
			zodSchema.HasSchema,
			zodSchema.SchemaClassName,
			isEFReferenced,
			ValueObjectSymbolInspector.IsEF8Referenced(compilation)
		);

		return GeneratorResult<ComplexValueObjectModel>.Create(model, diagnosticsList.ToImmutableArray());
	}

	/// <summary>
	/// Builds the complex member models and resolves their built-in string normalization. The option is
	/// only applied by the generated <c>Create</c>, so a hand-written <c>OnNormalize</c> hook or a
	/// hand-written <c>Create</c> takes precedence and the option is reported and dropped.
	/// </summary>
	static ImmutableArray<ComplexPropertyModel> BuildPropertyModels(
		INamedTypeSymbol typeSymbol,
		IPropertySymbol[] properties,
		bool createExists,
		ValueObjectDefaultsAttributeData assemblyDefaults,
		Location location,
		List<ReportableDiagnostic> diagnostics
	)
	{
		var propertyModels = ImmutableArray.CreateBuilder<ComplexPropertyModel>(properties.Length);
		foreach (var property in properties)
		{
			var propertyTypeName = ValueObjectSymbolInspector.ToTypeName(property.Type);
			var (normalizeTrim, normalizeCasing, normalizeExplicit) = ResolvePropertyNormalization(
				property,
				propertyTypeName,
				assemblyDefaults,
				diagnostics
			);
			propertyModels.Add(
				new ComplexPropertyModel(
					property.Name,
					propertyTypeName,
					TypeReference.Create(property.Type),
					normalizeTrim,
					normalizeCasing,
					normalizeExplicit
				)
			);
		}

		var normalizationConfigured = propertyModels.Any(static property =>
			property.NormalizeTrim
			|| ValueObjectEmitterHelpers.StringNormalization.IsCasingConfigured(property.NormalizeCasing)
		);
		if (normalizationConfigured)
		{
			var ignoredReason =
				ValueObjectSymbolInspector.HasHookImplementation(typeSymbol, "OnNormalize", properties.Length)
					? "implements 'OnNormalize'"
				: createExists ? "declares its own 'Create'"
				: null;
			if (ignoredReason is not null)
			{
				// A hook or a hand-written Create always wins. Only an explicit option is reported, so an
				// assembly-level default does not warn on every type that owns its own normalization.
				if (propertyModels.Any(static property => property.NormalizeExplicit))
				{
					diagnostics.Add(
						ReportableDiagnostic.Create(
							DiagnosticLibrary.StringNormalizationIgnored,
							isBlocking: false,
							location,
							typeSymbol.Name,
							ignoredReason
						)
					);
				}

				for (var index = 0; index < propertyModels.Count; index++)
				{
					propertyModels[index] = propertyModels[index] with
					{
						NormalizeTrim = false,
						NormalizeCasing = TypeLibrary.StringCasingFullTypeName + ".None",
					};
				}
			}
		}

		return propertyModels.ToImmutable();
	}

	/// <summary>
	/// Resolves the built-in string normalization for one complex member: an explicit
	/// <c>[StringNormalize]</c> overrides the assembly default, and the option is only meaningful for a
	/// string member. An explicit request on a non-string member is reported and dropped.
	/// </summary>
	static (bool Trim, string Casing, bool Explicit) ResolvePropertyNormalization(
		IPropertySymbol property,
		string propertyTypeName,
		ValueObjectDefaultsAttributeData assemblyDefaults,
		List<ReportableDiagnostic> diagnostics
	)
	{
		var attributeData = StringNormalizeAttributeData.FromAttributeData(property.GetAttributes());

		var trim = attributeData.Exists ? attributeData.Trim : assemblyDefaults.Trim;
		var casing = attributeData.Exists ? attributeData.Casing : assemblyDefaults.Casing;

		if (!trim && !ValueObjectEmitterHelpers.StringNormalization.IsCasingConfigured(casing))
			return (false, TypeLibrary.StringCasingFullTypeName + ".None", false);

		if (!ValueObjectEmitterHelpers.StringNormalization.IsStringTypeName(propertyTypeName))
		{
			if (attributeData.Exists)
			{
				diagnostics.Add(
					ReportableDiagnostic.Create(
						DiagnosticLibrary.StringNormalizationRequiresString,
						isBlocking: false,
						property.Locations.FirstOrDefault(static location => location.IsInSource)
							?? property.Locations.FirstOrDefault(),
						property.ContainingType.Name,
						property.Name
					)
				);
			}

			return (false, TypeLibrary.StringCasingFullTypeName + ".None", false);
		}

		return (trim, casing, attributeData.Exists);
	}

	/// <summary>
	/// Reports the Entity Framework Core mapping states for this complex value object that would otherwise
	/// be silent: a JSON column without the JSON converter, a complex-type mapping that the referenced
	/// Entity Framework Core version cannot honour, and members the generated complex mapping cannot
	/// convert (including collections).
	/// </summary>
	static void CollectEFMappingDiagnostics(
		INamedTypeSymbol typeSymbol,
		IPropertySymbol[] properties,
		ValueObjectAttributeData options,
		Compilation compilation,
		bool isEFReferenced,
		List<ReportableDiagnostic> diagnostics
	)
	{
		if (!isEFReferenced)
			return;

		var location = typeSymbol.Locations.FirstOrDefault(static candidate => candidate.IsInSource);
		var isJsonMapping =
			options.EFMapping is not null && ValueObjectSymbolInspector.IsEFMappingJson(options.EFMapping);
		if (isJsonMapping && !options.GenerateJsonConverter)
		{
			diagnostics.Add(
				ReportableDiagnostic.Create(
					DiagnosticLibrary.EFJsonMappingRequiresJsonConverter,
					isBlocking: false,
					location,
					typeSymbol.Name
				)
			);
		}

		var isComplexMapping =
			options.EFMapping is null || ValueObjectSymbolInspector.IsEFMappingComplexType(options.EFMapping);
		if (!isComplexMapping)
			return;

		if (!ValueObjectSymbolInspector.IsEF8Referenced(compilation))
		{
			diagnostics.Add(
				ReportableDiagnostic.Create(
					DiagnosticLibrary.EFComplexTypeRequiresEntityFramework8,
					isBlocking: false,
					location,
					typeSymbol.Name
				)
			);
			return;
		}

		foreach (var property in properties)
		{
			if (ValueObjectSymbolInspector.IsSupportedComplexMember(property.Type))
				continue;

			diagnostics.Add(
				ReportableDiagnostic.Create(
					DiagnosticLibrary.EFComplexMappingUnsupportedMember,
					isBlocking: false,
					property.Locations.FirstOrDefault(static candidate => candidate.IsInSource) ?? location,
					typeSymbol.Name,
					property.Name,
					property.Type.ToDisplayString(),
					ValueObjectSymbolInspector.IsCollectionMember(property.Type)
						? " because Entity Framework Core complex types do not map collections"
						: string.Empty
				)
			);
		}
	}

	static EquatableArray<string> BuildExistingRelationalOperators(
		INamedTypeSymbol typeSymbol,
		string leftTypeName,
		string rightTypeName
	)
	{
		var builder = ImmutableArray.CreateBuilder<string>();
		foreach (var operatorName in ValueObjectSymbolInspector.RelationalOperatorNames)
		{
			if (ValueObjectSymbolInspector.HasRelationalOperator(typeSymbol, operatorName, leftTypeName, rightTypeName))
				builder.Add(operatorName);
		}

		return builder.ToImmutable();
	}
}
