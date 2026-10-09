using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.ValueObjects.SourceGenerator.ValueObject;

static class ScalarValueObjectModelBuilder
{
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Design",
		"CA1506:Avoid excessive class coupling",
		Justification = "Value object model construction couples many value types."
	)]
	public static GeneratorResult<ScalarValueObjectModel> Build(
		INamedTypeSymbol typeSymbol,
		TypeDeclarationSyntax syntax,
		Compilation compilation,
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (typeSymbol is null || syntax is null)
			return GeneratorResult<ScalarValueObjectModel>.Empty;

		var location = syntax.GetLocation();
		List<ReportableDiagnostic> diagnosticsList =
		[
			.. ValueObjectSymbolInspector.ValidateValueObjectType(typeSymbol, "Scalar", location),
		];

		var attributes = typeSymbol.GetAttributes();
		if (
			ValueObjectSymbolInspector.HasAttribute(
				attributes,
				TypeLibrary.Purview.ValueObjects.Serialization.ValueObjectAttribute
			)
		)
		{
			diagnosticsList.Add(
				ReportableDiagnostic.Create(
					DiagnosticLibrary.ConflictingValueObjectAttributes,
					isBlocking: true,
					location,
					typeSymbol.Name
				)
			);
			return GeneratorResult<ScalarValueObjectModel>.Create([.. diagnosticsList]);
		}

		var assemblyDefaults = ValueObjectDefaultsAttributeData.FromAttributeData(
			typeSymbol.ContainingAssembly.GetAttributes()
		);

		var scalarAttribute = ScalarAttributeParser.Find(attributes);
		if (scalarAttribute is null)
			return GeneratorResult<ScalarValueObjectModel>.Create([.. diagnosticsList]);

		var scalarOptions = ValueObjectDefaultsHelper.Apply(
			ScalarAttributeParser.Parse(scalarAttribute),
			assemblyDefaults,
			attributes
		);

		if (
			!TryResolveScalarTarget(
				typeSymbol,
				scalarOptions,
				scalarAttribute,
				location,
				diagnosticsList,
				out var scalarType,
				out var scalarPropertyName,
				out var generateScalarProperty,
				out var scalarProperty
			)
		)
			return GeneratorResult<ScalarValueObjectModel>.Create([.. diagnosticsList]);

		var ctorExists = typeSymbol
			.Constructors.Where(static ctor => !ctor.IsStatic)
			.Any(ctor =>
				ctor.Parameters.Length == 1
				&& SymbolEqualityComparer.Default.Equals(ctor.Parameters[0].Type, scalarType)
			);

		var typeModel = ValueObjectSymbolInspector.BuildTypeModel(typeSymbol);
		if (typeModel is null)
			return GeneratorResult<ScalarValueObjectModel>.Create([.. diagnosticsList]);

		if (typeSymbol.TypeKind == TypeKind.Struct && !typeSymbol.IsRecord)
		{
			diagnosticsList.Add(
				ReportableDiagnostic.Create(
					DiagnosticLibrary.ScalarShouldBeRecordStruct,
					isBlocking: false,
					location,
					typeSymbol.Name
				)
			);
		}

		var typeName = typeModel.Value.FullyQualifiedName;
		var scalarTypeName = ValueObjectSymbolInspector.ToTypeName(scalarType);
		var scalarCanBeNull =
			scalarType.IsReferenceType || scalarType.NullableAnnotation == NullableAnnotation.Annotated;
		var scalarTypeIsNullableReference =
			scalarType.IsReferenceType && scalarType.NullableAnnotation == NullableAnnotation.Annotated;
		// Whether null is a valid domain value: an annotated reference type (`string?`) or a nullable value
		// type (`int?`). Distinct from ScalarCanBeNull, which is also true for a non-nullable reference type.
		var scalarValueIsNullable =
			scalarType.NullableAnnotation == NullableAnnotation.Annotated
			|| scalarType is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T };
		var scalarIsReferenceType = scalarType.IsReferenceType;
		var isReferenceType = typeSymbol.TypeKind == TypeKind.Class;
		var createExists = ValueObjectSymbolInspector.HasStaticFactory(typeSymbol, "Create", [scalarType]);
		var hydrateExists = ValueObjectSymbolInspector.HasStaticFactory(typeSymbol, "Hydrate", [scalarType]);
		var tryCreateExists = ValueObjectSymbolInspector.HasTryCreate(typeSymbol, scalarType);
		var compareToSelfExists = ValueObjectSymbolInspector.HasInstanceMethod(typeSymbol, "CompareTo", [typeSymbol]);
		var compareToPrimitiveExists = ValueObjectSymbolInspector.HasInstanceMethod(
			typeSymbol,
			"CompareTo",
			[scalarType]
		);
		var compareToObjectExists = ValueObjectSymbolInspector.HasCompareToObject(typeSymbol);
		var equalsSelfExists =
			typeSymbol.IsRecord || ValueObjectSymbolInspector.HasInstanceMethod(typeSymbol, "Equals", [typeSymbol]);
		var equalsPrimitiveExists = ValueObjectSymbolInspector.HasInstanceMethod(typeSymbol, "Equals", [scalarType]);
		var equalsObjectExists = ValueObjectSymbolInspector.HasEqualsObject(typeSymbol);
		var getHashCodeExists = ValueObjectSymbolInspector.HasParameterlessMethod(typeSymbol, "GetHashCode");
		var sameTypeEqualityOperatorExists =
			typeSymbol.IsRecord
			|| ValueObjectSymbolInspector.HasBinaryOperator(typeSymbol, "op_Equality", [typeSymbol, typeSymbol]);
		var sameTypeInequalityOperatorExists =
			typeSymbol.IsRecord
			|| ValueObjectSymbolInspector.HasBinaryOperator(typeSymbol, "op_Inequality", [typeSymbol, typeSymbol]);
		var primitiveEqualityOperatorExists = ValueObjectSymbolInspector.HasBinaryOperator(
			typeSymbol,
			"op_Equality",
			[typeSymbol, scalarType]
		);
		var primitiveInequalityOperatorExists = ValueObjectSymbolInspector.HasBinaryOperator(
			typeSymbol,
			"op_Inequality",
			[typeSymbol, scalarType]
		);
		var reversePrimitiveEqualityOperatorExists = ValueObjectSymbolInspector.HasBinaryOperator(
			typeSymbol,
			"op_Equality",
			[scalarType, typeSymbol]
		);
		var reversePrimitiveInequalityOperatorExists = ValueObjectSymbolInspector.HasBinaryOperator(
			typeSymbol,
			"op_Inequality",
			[scalarType, typeSymbol]
		);
		var enumPropertiesEnabled = scalarOptions.GenerateEnumProperties && scalarType.TypeKind == TypeKind.Enum;
		var enumFieldNames = enumPropertiesEnabled ? BuildEnumFieldNames(typeSymbol, scalarType) : [];
		var toStringExists = ValueObjectSymbolInspector.HasParameterlessMethod(typeSymbol, "ToString");
		// Mirror the underlying value's formatting overloads per type. The scalar is unwrapped first so a
		// nullable value type (`int?`) resolves the overloads declared by its underlying type (`int`), not by
		// `Nullable<int>`. Each generated overload is suppressed when the author already declared it, because
		// a duplicate member would be CS0111 in generated code the consumer cannot edit.
		var effectiveScalarType = ValueObjectSymbolInspector.UnwrapNullable(scalarType);
		var stringType = compilation.GetSpecialType(SpecialType.System_String);
		var formatProviderType = compilation.GetTypeByMetadataName("System.IFormatProvider");
		var scalarImplementsIFormattable = ValueObjectSymbolInspector.ImplementsIFormattable(effectiveScalarType);
		var formattedToStringExists =
			formatProviderType is not null
			&& ValueObjectSymbolInspector.DeclaresToString(typeSymbol, [stringType, formatProviderType]);
		var scalarHasFormatToString = ValueObjectSymbolInspector.DeclaresToString(effectiveScalarType, [stringType]);
		var formatToStringExists = ValueObjectSymbolInspector.DeclaresToString(typeSymbol, [stringType]);
		// Mirror the underlying value's other standard interfaces (equatable, span formatting, and parsing)
		// so the value object behaves like the type it wraps in equality, sorting, formatting, and parsing
		// contexts. An interface is skipped when the author already declared one of its members, because a
		// duplicate member would be CS0111 in generated code the consumer cannot edit.
		var mirroring = ResolveInterfaceMirroring(
			typeSymbol,
			scalarType,
			effectiveScalarType,
			compilation,
			stringType,
			formatProviderType
		);
		var hasJsonConverterAttribute = ValueObjectSymbolInspector.HasAttribute(
			typeSymbol,
			TypeLibrary.System.Text.Json.Serialization.JsonConverterAttribute
		);
		var declareOnNormalize = ValueObjectSymbolInspector.ShouldEmitScalarHookDeclaration(
			typeSymbol,
			"OnNormalize",
			1,
			includeRef: true
		);
		var declareOnValidate = ValueObjectSymbolInspector.ShouldEmitScalarHookDeclaration(
			typeSymbol,
			"OnValidate",
			1,
			includeRef: false
		);

		if (scalarOptions.DeserializationMode == ValueObjectSymbolInspector.StrictModeName && !createExists)
		{
			diagnosticsList.Add(
				ReportableDiagnostic.Create(
					DiagnosticLibrary.StrictDeserializationRequiresCreate,
					isBlocking: false,
					typeSymbol.Locations.FirstOrDefault(),
					typeSymbol.Name
				)
			);
		}

		var hintName = ValueObjectSymbolInspector.BuildHintName(typeSymbol, "ScalarValueObject");

		var zodSchema = ValueObjectSymbolInspector.ResolveZodSchemaIntegration(typeSymbol);
		ValueObjectSymbolInspector.CollectZodSchemaDiagnostics(
			typeSymbol,
			zodSchema,
			scalarOptions.ZodSchemaMode,
			onValidateImplemented: ValueObjectSymbolInspector.HasHookImplementation(typeSymbol, "OnValidate", 1),
			diagnosticsList
		);

		var isEFReferenced = CollectEFDiagnostics(
			typeSymbol,
			scalarType,
			scalarTypeName,
			scalarOptions,
			attributes,
			compilation,
			location,
			diagnosticsList
		);

		var (efProviderType, efHydrateCastTypeName) = ValueObjectSymbolInspector.ResolveEFProviderType(scalarType);

		var efValueGeneratorEnabled = ResolveEFValueGeneration(
			typeSymbol,
			scalarType,
			scalarOptions,
			isEFReferenced,
			diagnosticsList
		);
		if (scalarProperty is not null)
			ValueObjectSymbolInspector.CollectMutableMemberDiagnostics(typeSymbol, [scalarProperty], diagnosticsList);

		ScalarValueObjectModel model = new(
			typeModel.Value,
			scalarOptions,
			ctorExists,
			hintName,
			typeName,
			scalarTypeName,
			scalarPropertyName,
			generateScalarProperty,
			scalarCanBeNull,
			scalarTypeIsNullableReference,
			scalarValueIsNullable,
			scalarIsReferenceType,
			isReferenceType,
			typeSymbol.IsRecord,
			typeSymbol.IsReadOnly,
			typeSymbol.DeclaredAccessibility.ToTypeDeclarationAccessibility(),
			TypeReference.Create(scalarType),
			// The nullable annotation is dropped: a static abstract parsing interface is closed over the
			// non-nullable type (`IParsable<string>`, not `IParsable<string?>`), so the helper's type argument
			// must be `string`, and a nullable value type's `int?` must unwrap to `int`.
			TypeReference.Create(effectiveScalarType.WithNullableAnnotation(NullableAnnotation.NotAnnotated)),
			createExists,
			hydrateExists,
			tryCreateExists,
			compareToSelfExists,
			compareToPrimitiveExists,
			compareToObjectExists,
			equalsSelfExists,
			equalsPrimitiveExists,
			equalsObjectExists,
			getHashCodeExists,
			sameTypeEqualityOperatorExists,
			sameTypeInequalityOperatorExists,
			primitiveEqualityOperatorExists,
			primitiveInequalityOperatorExists,
			reversePrimitiveEqualityOperatorExists,
			reversePrimitiveInequalityOperatorExists,
			enumPropertiesEnabled,
			enumFieldNames,
			toStringExists,
			scalarImplementsIFormattable,
			formattedToStringExists,
			scalarHasFormatToString,
			formatToStringExists,
			mirroring.IEquatableValue,
			mirroring.SpanFormattable,
			mirroring.Utf8SpanFormattable,
			mirroring.Parsable,
			mirroring.SpanParsable,
			mirroring.Utf8SpanParsable,
			hasJsonConverterAttribute,
			declareOnNormalize,
			declareOnValidate,
			ValueObjectSymbolInspector.ImplementsSelfEquatable(typeSymbol),
			ValueObjectSymbolInspector.HasMemberWithName(typeSymbol, "Empty"),
			ValueObjectSymbolInspector.GetEmptyValueExpression(scalarType),
			ValueObjectSymbolInspector.HasConversionOperator(typeSymbol, scalarType, fromPrimitive: true),
			ValueObjectSymbolInspector.HasConversionOperator(typeSymbol, scalarType, fromPrimitive: false),
			ValueObjectSymbolInspector.HasContextualCreateOverload(typeSymbol, scalarType),
			SymbolEqualityComparer.Default.Equals(scalarType, typeSymbol),
			BuildExistingRelationalOperators(typeSymbol, typeName, typeName),
			BuildExistingRelationalOperators(typeSymbol, typeName, scalarTypeName),
			zodSchema.HasSchema,
			zodSchema.SchemaClassName,
			isEFReferenced,
			ValueObjectSymbolInspector.IsEFMappableProviderType(scalarType),
			ValueObjectSymbolInspector.ToTypeName(efProviderType),
			TypeReference.Create(efProviderType),
			efHydrateCastTypeName,
			efValueGeneratorEnabled,
			// Complex-type mapping and the compiled-model converter members need Entity Framework Core 8.
			ValueObjectSymbolInspector.IsEF8Referenced(compilation)
		);

		return GeneratorResult<ScalarValueObjectModel>.Create(model, diagnosticsList.ToImmutableArray());
	}

	/// <summary>
	/// Reports the Entity Framework Core availability diagnostics for the scalar and returns whether the
	/// compilation references Entity Framework Core.
	/// </summary>
	static bool CollectEFDiagnostics(
		INamedTypeSymbol typeSymbol,
		ITypeSymbol scalarType,
		string scalarTypeName,
		ScalarAttributeData scalarOptions,
		ImmutableArray<AttributeData> attributes,
		Compilation compilation,
		Location location,
		List<ReportableDiagnostic> diagnostics
	)
	{
		var isEFReferenced = ValueObjectSymbolInspector.IsEFReferenced(compilation);
		if (
			isEFReferenced
			&& scalarOptions.GenerateEFConverter
			&& !ValueObjectSymbolInspector.IsEFMappableProviderType(scalarType)
		)
		{
			diagnostics.Add(
				ReportableDiagnostic.Create(
					DiagnosticLibrary.EFAutoConversionSkipped,
					isBlocking: false,
					typeSymbol.Locations.FirstOrDefault(),
					typeSymbol.Name,
					scalarTypeName
				)
			);
		}
		else if (
			!isEFReferenced
			&& (
				ValueObjectDefaultsHelper.IsScalarPropertyExplicitlySet(attributes, "GenerateEFConverter")
				|| ValueObjectDefaultsHelper.IsScalarPropertyExplicitlySet(attributes, "GenerateEFComparer")
			)
		)
		{
			diagnostics.Add(
				ReportableDiagnostic.Create(
					DiagnosticLibrary.EFMappingRequiresEntityFramework,
					isBlocking: false,
					location,
					typeSymbol.Name
				)
			);
		}

		return isEFReferenced;
	}

	/// <summary>
	/// Resolves the underlying value type and property name for either scalar form, reporting the
	/// declaration errors as blocking diagnostics.
	/// </summary>
	/// <remarks>
	/// The manual <c>[Scalar]</c> form requires the author to declare the named property (<c>VO1004</c>);
	/// the automatic <c>[Scalar&lt;T&gt;]</c> form derives the type from the attribute argument and forbids
	/// a declared member with that name (<c>VO1022</c>).
	/// </remarks>
	static bool TryResolveScalarTarget(
		INamedTypeSymbol typeSymbol,
		ScalarAttributeData scalarOptions,
		AttributeData scalarAttribute,
		Location location,
		List<ReportableDiagnostic> diagnostics,
		out ITypeSymbol scalarType,
		out string scalarPropertyName,
		out bool generateScalarProperty,
		out IPropertySymbol? scalarProperty
	)
	{
		scalarType = null!;
		scalarPropertyName = scalarOptions.PropertyName;
		generateScalarProperty = false;
		scalarProperty = null;

		if (ScalarAttributeParser.GetValueType(scalarAttribute) is { } declaredType)
		{
			scalarType =
				ScalarAttributeParser.GetNullable(scalarAttribute) && declaredType.IsReferenceType
					? declaredType.WithNullableAnnotation(NullableAnnotation.Annotated)
					: declaredType;
			generateScalarProperty = true;

			// The generator owns the named property, so a declared member with that name (or with the
			// interface's Value name, which is forwarded) is a conflict - never both.
			var conflictingName =
				HasDeclaredMember(typeSymbol, scalarPropertyName) ? scalarPropertyName
				: !string.Equals(scalarPropertyName, "Value", StringComparison.Ordinal)
				&& HasDeclaredMember(typeSymbol, "Value")
					? "Value"
				: null;

			if (conflictingName is not null)
			{
				diagnostics.Add(
					ReportableDiagnostic.Create(
						DiagnosticLibrary.ScalarPropertyIsGenerated,
						isBlocking: true,
						GetDeclaredMemberLocation(typeSymbol, conflictingName) ?? location,
						typeSymbol.Name,
						conflictingName
					)
				);
				return false;
			}

			return true;
		}

		scalarProperty = typeSymbol
			.GetMembers(scalarOptions.PropertyName)
			.OfType<IPropertySymbol>()
			.FirstOrDefault(property => !property.IsStatic && property.GetMethod is not null);

		if (scalarProperty is null)
		{
			diagnostics.Add(
				ReportableDiagnostic.Create(
					DiagnosticLibrary.ScalarPropertyMissing,
					isBlocking: true,
					location,
					typeSymbol.Name,
					scalarOptions.PropertyName
				)
			);
			return false;
		}

		scalarType = scalarProperty.Type;
		scalarPropertyName = scalarProperty.Name;
		return true;
	}

	/// <summary>
	/// True when the type already declares, in user-authored source, a property or field with the given
	/// name. Used by the automatic form, where the generator owns the named property: a declared member
	/// would be a duplicate (CS0102) in the merged partial, so it is reported as a blocking diagnostic
	/// instead. Members the generator emitted (in a <c>*.g.cs</c> tree) are ignored, otherwise the
	/// analyzer would see the property the generator just declared and flag its own output.
	/// </summary>
	static bool HasDeclaredMember(INamedTypeSymbol typeSymbol, string name) =>
		typeSymbol
			.GetMembers(name)
			.Where(static member => member is IPropertySymbol or IFieldSymbol)
			.Any(static member =>
				member.DeclaringSyntaxReferences.Any(static reference => !IsGeneratedSyntaxTree(reference.SyntaxTree))
			);

	static Location? GetDeclaredMemberLocation(INamedTypeSymbol typeSymbol, string name) =>
		typeSymbol
			.GetMembers(name)
			.Where(static member => member is IPropertySymbol or IFieldSymbol)
			.SelectMany(static member => member.DeclaringSyntaxReferences)
			.Where(static reference => !IsGeneratedSyntaxTree(reference.SyntaxTree))
			.Select(static reference => reference.GetSyntax().GetLocation())
			.FirstOrDefault(static location => location.IsInSource);

	static bool IsGeneratedSyntaxTree(SyntaxTree syntaxTree) =>
		syntaxTree.FilePath.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase);

	/// <summary>
	/// Resolves whether the value object gets an Entity Framework Core key value generator, reporting the
	/// cases where the option was requested but cannot be honoured: a scalar whose underlying value is not
	/// a <see cref="Guid"/>, or one whose Entity Framework converter is disabled.
	/// </summary>
	static bool ResolveEFValueGeneration(
		INamedTypeSymbol typeSymbol,
		ITypeSymbol scalarType,
		ScalarAttributeData options,
		bool isEFReferenced,
		List<ReportableDiagnostic> diagnostics
	)
	{
		if (!isEFReferenced || !options.GenerateEFValueGenerator)
			return false;

		var isGuidProvider = ValueObjectSymbolInspector.IsGuidProviderType(scalarType);
		var enabled = isGuidProvider && options.GenerateEFConverter;
		if (!enabled)
		{
			diagnostics.Add(
				ReportableDiagnostic.Create(
					DiagnosticLibrary.EFValueGenerationUnavailable,
					isBlocking: false,
					typeSymbol.Locations.FirstOrDefault(),
					typeSymbol.Name,
					isGuidProvider ? "the Entity Framework converter is disabled" : "its underlying value is not a Guid"
				)
			);
		}

		return enabled;
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

	static EquatableArray<string> BuildEnumFieldNames(INamedTypeSymbol typeSymbol, ITypeSymbol enumType)
	{
		var builder = ImmutableArray.CreateBuilder<string>();
		foreach (var enumField in ValueObjectSymbolInspector.GetEnumFields(enumType))
		{
			if (ValueObjectSymbolInspector.HasMemberWithName(typeSymbol, enumField.Name))
				continue;

			builder.Add(enumField.Name);
		}

		return builder.ToImmutable();
	}

	/// <summary>
	/// The set of standard interfaces the generated value object mirrors from its underlying scalar value.
	/// </summary>
	readonly record struct InterfaceMirroring(
		bool IEquatableValue,
		bool SpanFormattable,
		bool Utf8SpanFormattable,
		bool Parsable,
		bool SpanParsable,
		bool Utf8SpanParsable
	);

	/// <summary>
	/// The parsing interfaces the generated value object mirrors.
	/// </summary>
	readonly record struct ParsingMirroring(bool Parsable, bool SpanParsable, bool Utf8SpanParsable);

	/// <summary>
	/// Resolves which of the underlying scalar value's standard interfaces the generated value object should
	/// mirror. Detection is against the underlying type (unwrapped from <see cref="Nullable{T}"/>), so the
	/// value object behaves like the type it wraps; an interface is skipped when the author already declared
	/// one of its members.
	/// </summary>
	static InterfaceMirroring ResolveInterfaceMirroring(
		INamedTypeSymbol typeSymbol,
		ITypeSymbol scalarType,
		ITypeSymbol effectiveScalarType,
		Compilation compilation,
		ITypeSymbol stringType,
		ITypeSymbol? formatProviderType
	)
	{
		var intType = compilation.GetSpecialType(SpecialType.System_Int32);
		var charType = compilation.GetSpecialType(SpecialType.System_Char);
		var byteType = compilation.GetSpecialType(SpecialType.System_Byte);
		var spanOfChar = compilation.GetTypeByMetadataName("System.Span`1")?.Construct(charType);
		var spanOfByte = compilation.GetTypeByMetadataName("System.Span`1")?.Construct(byteType);
		var readOnlySpanOfChar = compilation.GetTypeByMetadataName("System.ReadOnlySpan`1")?.Construct(charType);
		var readOnlySpanOfByte = compilation.GetTypeByMetadataName("System.ReadOnlySpan`1")?.Construct(byteType);

		// IEquatable<TValue> is checked against the property type, not the unwrapped type: the generated
		// Equals(TValue) takes the property type, so a nullable value type (`int?`) cannot satisfy
		// IEquatable<int> and is left alone.
		var iEquatableValue = ValueObjectSymbolInspector.ImplementsGenericInterface(
			scalarType,
			"IEquatable",
			scalarType
		);

		var spanFormattable =
			ValueObjectSymbolInspector.ImplementsInterface(effectiveScalarType, "ISpanFormattable")
			&& !DeclaresTryFormat(typeSymbol, spanOfChar, intType, readOnlySpanOfChar, formatProviderType);

		var utf8SpanFormattable =
			ValueObjectSymbolInspector.ImplementsInterface(effectiveScalarType, "IUtf8SpanFormattable")
			&& !DeclaresTryFormat(typeSymbol, spanOfByte, intType, readOnlySpanOfChar, formatProviderType);

		var parsing = ResolveParsingMirroring(
			typeSymbol,
			effectiveScalarType,
			stringType,
			formatProviderType,
			readOnlySpanOfChar,
			readOnlySpanOfByte
		);

		return new InterfaceMirroring(
			iEquatableValue,
			spanFormattable,
			utf8SpanFormattable,
			parsing.Parsable,
			parsing.SpanParsable,
			parsing.Utf8SpanParsable
		);
	}

	static ParsingMirroring ResolveParsingMirroring(
		INamedTypeSymbol typeSymbol,
		ITypeSymbol effectiveScalarType,
		ITypeSymbol stringType,
		ITypeSymbol? formatProviderType,
		ITypeSymbol? readOnlySpanOfChar,
		ITypeSymbol? readOnlySpanOfByte
	)
	{
		var parsable =
			formatProviderType is not null
			&& ValueObjectSymbolInspector.ImplementsGenericInterface(
				effectiveScalarType,
				"IParsable",
				effectiveScalarType
			)
			&& !DeclaresParseMember(typeSymbol, stringType, formatProviderType);

		// ISpanParsable<T> extends IParsable<T>, so it can only be mirrored when the string members are
		// generated too; IUtf8SpanParsable<T> is standalone.
		var spanParsable =
			parsable
			&& readOnlySpanOfChar is not null
			&& ValueObjectSymbolInspector.ImplementsGenericInterface(
				effectiveScalarType,
				"ISpanParsable",
				effectiveScalarType
			)
			&& !DeclaresParseMember(typeSymbol, readOnlySpanOfChar, formatProviderType);

		var utf8SpanParsable =
			readOnlySpanOfByte is not null
			&& formatProviderType is not null
			&& ValueObjectSymbolInspector.ImplementsGenericInterface(
				effectiveScalarType,
				"IUtf8SpanParsable",
				effectiveScalarType
			)
			&& !DeclaresParseMember(typeSymbol, readOnlySpanOfByte, formatProviderType);

		return new ParsingMirroring(parsable, spanParsable, utf8SpanParsable);
	}

	static bool DeclaresTryFormat(
		INamedTypeSymbol typeSymbol,
		ITypeSymbol? destinationType,
		ITypeSymbol intType,
		ITypeSymbol? readOnlySpanOfChar,
		ITypeSymbol? formatProviderType
	) =>
		destinationType is not null
		&& readOnlySpanOfChar is not null
		&& formatProviderType is not null
		&& ValueObjectSymbolInspector.HasInstanceMethod(
			typeSymbol,
			"TryFormat",
			[destinationType, intType, readOnlySpanOfChar, formatProviderType]
		);

	static bool DeclaresParseMember(
		INamedTypeSymbol typeSymbol,
		ITypeSymbol inputType,
		ITypeSymbol? formatProviderType
	) =>
		formatProviderType is not null
		&& (
			ValueObjectSymbolInspector.HasStaticMethod(typeSymbol, "Parse", [inputType, formatProviderType])
			|| ValueObjectSymbolInspector.HasStaticMethod(
				typeSymbol,
				"TryParse",
				[inputType, formatProviderType, typeSymbol]
			)
		);
}
