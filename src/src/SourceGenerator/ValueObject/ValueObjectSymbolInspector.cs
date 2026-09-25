using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.ValueObjects.SourceGenerator.ValueObject;

static class ValueObjectSymbolInspector
{
	public static readonly string[] RelationalOperatorNames =
	[
		OperatorNames.EqualityAndRelational.LessThanOperatorName,
		OperatorNames.EqualityAndRelational.GreaterThanOperatorName,
		OperatorNames.EqualityAndRelational.LessThanOrEqualOperatorName,
		OperatorNames.EqualityAndRelational.GreaterThanOrEqualOperatorName,
	];

	public static List<ReportableDiagnostic> ValidateValueObjectType(
		INamedTypeSymbol typeSymbol,
		string attributeName,
		Location location
	)
	{
		List<ReportableDiagnostic> diagnostics = [];

		var isPartial = typeSymbol
			.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax())
			.OfType<TypeDeclarationSyntax>()
			.Any(syntax => syntax.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PartialKeyword)));
		if (!isPartial)
		{
			diagnostics.Add(
				ReportableDiagnostic.Create(
					DiagnosticLibrary.ValueObjectMustBePartial,
					isBlocking: true,
					location,
					typeSymbol.Name,
					attributeName
				)
			);
		}

		if (typeSymbol.ContainingType is not null)
		{
			diagnostics.Add(
				ReportableDiagnostic.Create(
					DiagnosticLibrary.NestedValueObjectsAreNotSupported,
					isBlocking: true,
					location,
					typeSymbol.Name,
					attributeName
				)
			);
		}

		if (typeSymbol.TypeParameters.Length > 0)
		{
			diagnostics.Add(
				ReportableDiagnostic.Create(
					DiagnosticLibrary.GenericValueObjectsAreNotSupported,
					isBlocking: true,
					location,
					typeSymbol.Name,
					attributeName
				)
			);
		}

		return diagnostics;
	}

	public static bool HasAttribute(INamedTypeSymbol typeSymbol, TypeIdentity attributeType) =>
		typeSymbol.GetAttributes().Any(attribute => attributeType.Equals(attribute.AttributeClass));

	public static bool HasAttribute(ImmutableArray<AttributeData> attributes, TypeIdentity attributeType) =>
		attributes.Any(attribute => attributeType.Equals(attribute.AttributeClass));

	public static bool HasAttribute(INamedTypeSymbol typeSymbol, string metadataName) =>
		typeSymbol.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == metadataName);

	public static bool HasAttribute(ImmutableArray<AttributeData> attributes, string metadataName) =>
		attributes.Any(attribute => attribute.AttributeClass?.ToDisplayString() == metadataName);

	public static bool HasStaticFactory(INamedTypeSymbol typeSymbol, string name, ITypeSymbol[] parameterTypes)
	{
		return typeSymbol
			.GetMembers(name)
			.OfType<IMethodSymbol>()
			.Any(method =>
				method.IsStatic
				&& method.DeclaredAccessibility == Accessibility.Public
				&& method.Parameters.Length == parameterTypes.Length
				&& SymbolEqualityComparer.Default.Equals(method.ReturnType, typeSymbol)
				&& ParametersMatch(method.Parameters, parameterTypes)
			);
	}

	public static bool HasTryCreate(INamedTypeSymbol typeSymbol, ITypeSymbol scalarType)
	{
		return typeSymbol
			.GetMembers("TryCreate")
			.OfType<IMethodSymbol>()
			.Any(method =>
				method.IsStatic
				&& method.Parameters.Length == 2
				&& method.ReturnType.SpecialType == SpecialType.System_Boolean
				&& method.Parameters[1].RefKind == RefKind.Out
				&& SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, scalarType)
				&& SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, typeSymbol)
			);
	}

	public static bool HasInstanceMethod(
		INamedTypeSymbol typeSymbol,
		string name,
		IReadOnlyList<ITypeSymbol> parameterTypes
	)
	{
		return typeSymbol
			.GetMembers(name)
			.OfType<IMethodSymbol>()
			.Any(method =>
				!method.IsStatic
				&& method.Parameters.Length == parameterTypes.Count
				&& ParametersMatch(method.Parameters, parameterTypes)
			);
	}

	public static bool HasCompareToObject(INamedTypeSymbol typeSymbol) =>
		typeSymbol
			.GetMembers("CompareTo")
			.OfType<IMethodSymbol>()
			.Any(method =>
				!method.IsStatic
				&& method.Parameters.Length == 1
				&& method.Parameters[0].Type.SpecialType == SpecialType.System_Object
			);

	public static bool HasEqualsObject(INamedTypeSymbol typeSymbol) =>
		typeSymbol
			.GetMembers("Equals")
			.OfType<IMethodSymbol>()
			.Any(method =>
				!method.IsStatic
				&& method.Parameters.Length == 1
				&& method.Parameters[0].Type.SpecialType == SpecialType.System_Object
			);

	public static bool HasBinaryOperator(
		INamedTypeSymbol typeSymbol,
		string operatorMethodName,
		IReadOnlyList<ITypeSymbol> parameterTypes
	)
	{
		return typeSymbol
			.GetMembers(operatorMethodName)
			.OfType<IMethodSymbol>()
			.Any(method =>
				method.IsStatic
				&& method.Parameters.Length == parameterTypes.Count
				&& method.ReturnType.SpecialType == SpecialType.System_Boolean
				&& ParametersMatch(method.Parameters, parameterTypes)
			);
	}

	public static bool HasParameterlessMethod(INamedTypeSymbol typeSymbol, string name) =>
		typeSymbol
			.GetMembers(name)
			.OfType<IMethodSymbol>()
			.Any(method => !method.IsStatic && !method.IsImplicitlyDeclared && method.Parameters.Length == 0);

	public static bool ParametersMatch(ImmutableArray<IParameterSymbol> parameters, IReadOnlyList<ITypeSymbol> expected)
	{
		for (var i = 0; i < expected.Count; i++)
		{
			if (!SymbolEqualityComparer.Default.Equals(parameters[i].Type, expected[i]))
				return false;
		}

		return true;
	}

	public static bool HasConversionOperator(INamedTypeSymbol typeSymbol, ITypeSymbol primitiveType, bool fromPrimitive)
	{
		return typeSymbol
			.GetMembers()
			.OfType<IMethodSymbol>()
			.Any(method =>
				method.MethodKind == MethodKind.Conversion
				&& method.Name == "op_Implicit"
				&& (
					fromPrimitive
						? method.Parameters.Length == 1
							&& SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, primitiveType)
							&& SymbolEqualityComparer.Default.Equals(method.ReturnType, typeSymbol)
						: method.Parameters.Length == 1
							&& SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, typeSymbol)
							&& SymbolEqualityComparer.Default.Equals(method.ReturnType, primitiveType)
				)
			);
	}

	public static bool HasRelationalOperator(
		INamedTypeSymbol typeSymbol,
		string operatorMethodName,
		string leftTypeName,
		string rightTypeName
	)
	{
		return typeSymbol
			.GetMembers(operatorMethodName)
			.OfType<IMethodSymbol>()
			.Any(method =>
				method.IsStatic
				&& method.Parameters.Length == 2
				&& method.ReturnType.SpecialType == SpecialType.System_Boolean
				&& method
					.Parameters[0]
					.Type.ToDisplayString(
						SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
							SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
								| SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
						)
					) == leftTypeName
				&& method
					.Parameters[1]
					.Type.ToDisplayString(
						SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
							SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
								| SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
						)
					) == rightTypeName
			);
	}

	public static bool HasContextualCreateOverload(INamedTypeSymbol typeSymbol, ITypeSymbol primitiveType)
	{
		return typeSymbol
			.GetMembers("Create")
			.OfType<IMethodSymbol>()
			.Any(method =>
				method.IsStatic
				&& method.Parameters.Length == 2
				&& SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, primitiveType)
				&& method.Parameters[1].RefKind == RefKind.In
				&& method.Parameters[1].Type.Name == "ValueObjectContext"
			);
	}

	public static bool ShouldEmitScalarHookDeclaration(
		INamedTypeSymbol typeSymbol,
		string methodName,
		int parameterCount,
		bool includeRef
	)
	{
		var declarations = typeSymbol
			.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax())
			.OfType<TypeDeclarationSyntax>()
			.SelectMany(declaration => declaration.Members.OfType<MethodDeclarationSyntax>())
			.Where(method =>
				method.Identifier.Text == methodName && method.ParameterList.Parameters.Count == parameterCount
			)
			.ToArray();

		var hasDefinition = declarations.Any(method => method.Body is null && method.ExpressionBody is null);
		if (hasDefinition)
			return false;

		if (!includeRef)
			return true;

		var hasRefImplementation = declarations.Any(method =>
			method.ParameterList.Parameters[0].Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.RefKeyword))
		);
		return hasRefImplementation || declarations.Length == 0;
	}

	public static bool ShouldEmitComplexHookDeclaration(
		INamedTypeSymbol typeSymbol,
		string methodName,
		int parameterCount,
		bool includeRef = false
	)
	{
		var declarations = GetComplexHookDeclarations(typeSymbol, methodName, parameterCount);

		var hasDefinition = declarations.Any(method =>
			(
				!includeRef
				|| method.ParameterList.Parameters.All(static parameter =>
					parameter.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.RefKeyword))
				)
			)
			&& method.Body is null
			&& method.ExpressionBody is null
		);
		if (hasDefinition)
			return false;

		if (!includeRef)
			return true;

		var hasRefImplementation = declarations.Any(method =>
			method.ParameterList.Parameters.All(static parameter =>
				parameter.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.RefKeyword))
			)
		);
		return hasRefImplementation || declarations.Length == 0;
	}

	public static bool IsComplexHookReadOnly(INamedTypeSymbol typeSymbol, string methodName, int parameterCount)
	{
		return GetComplexHookDeclarations(typeSymbol, methodName, parameterCount)
			.Any(method =>
				(method.Body is not null || method.ExpressionBody is not null)
				&& method.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.ReadOnlyKeyword))
			);
	}

	public static MethodDeclarationSyntax[] GetComplexHookDeclarations(
		INamedTypeSymbol typeSymbol,
		string methodName,
		int parameterCount
	)
	{
		return
		[
			.. typeSymbol
				.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax())
				.OfType<TypeDeclarationSyntax>()
				.SelectMany(declaration => declaration.Members.OfType<MethodDeclarationSyntax>())
				.Where(method =>
					method.Identifier.Text == methodName && method.ParameterList.Parameters.Count == parameterCount
				),
		];
	}

	public static bool ConstructorMatches(IMethodSymbol constructor, IPropertySymbol[] properties)
	{
		if (constructor.Parameters.Length != properties.Length)
			return false;

		for (var i = 0; i < properties.Length; i++)
		{
			if (!SymbolEqualityComparer.Default.Equals(constructor.Parameters[i].Type, properties[i].Type))
				return false;
		}

		return true;
	}

	public static bool TryGetEFConstructorArguments(
		INamedTypeSymbol typeSymbol,
		IPropertySymbol[] properties,
		out string arguments
	)
	{
		if (properties.Length > 0)
		{
			arguments = string.Join(
				", ",
				properties.Select(static property => GetConstructorArgumentExpression(property.Type))
			);
			return true;
		}

		var parameterizedConstructors = typeSymbol
			.Constructors.Where(static ctor => !ctor.IsStatic && ctor.Parameters.Length > 0)
			.ToArray();

		if (parameterizedConstructors.Length == 1)
		{
			arguments = string.Join(
				", ",
				parameterizedConstructors[0]
					.Parameters.Select(static parameter => GetConstructorArgumentExpression(parameter.Type))
			);
			return true;
		}

		arguments = string.Empty;
		return false;
	}

	public static bool IsValueObjectPropertyCandidate(INamedTypeSymbol typeSymbol, IPropertySymbol property) =>
		!property.IsImplicitlyDeclared
		|| (
			typeSymbol.IsRecord
			&& typeSymbol
				.InstanceConstructors.Where(static ctor => !ctor.IsStatic)
				.SelectMany(static ctor => ctor.Parameters)
				.Any(parameter =>
					SymbolEqualityComparer.Default.Equals(parameter.Type, property.Type)
					&& string.Equals(parameter.Name, property.Name, StringComparison.OrdinalIgnoreCase)
				)
		);

	public static bool ImplementsSelfEquatable(INamedTypeSymbol typeSymbol) =>
		typeSymbol.AllInterfaces.Any(interfaceSymbol =>
			interfaceSymbol is INamedTypeSymbol namedTypeSymbol
			&& namedTypeSymbol.OriginalDefinition.Name == nameof(IEquatable<>)
			&& namedTypeSymbol.OriginalDefinition.ContainingNamespace.ToDisplayString() == "System"
			&& namedTypeSymbol.TypeArguments.Length == 1
			&& SymbolEqualityComparer.Default.Equals(namedTypeSymbol.TypeArguments[0], typeSymbol)
		);

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0072:Add missing cases")]
	public static GeneratedTypeModel? BuildTypeModel(INamedTypeSymbol typeSymbol)
	{
		if (typeSymbol.ContainingType is not null || typeSymbol.TypeParameters.Length > 0)
			return null;

		var access = typeSymbol.DeclaredAccessibility switch
		{
			Accessibility.Public => "public",
			Accessibility.Internal => "internal",
			Accessibility.Private => "private",
			Accessibility.Protected => "protected",
			Accessibility.ProtectedOrInternal => "protected internal",
			Accessibility.ProtectedAndInternal => "private protected",
			_ => "internal",
		};

		string declaration;
		if (typeSymbol.TypeKind == TypeKind.Struct)
		{
			var readonlyPrefix = typeSymbol.IsReadOnly ? "readonly " : string.Empty;
			declaration = typeSymbol.IsRecord
				? $"{access} {readonlyPrefix}partial record struct {typeSymbol.Name}"
				: $"{access} {readonlyPrefix}partial struct {typeSymbol.Name}";
		}
		else
		{
			declaration = typeSymbol.IsRecord
				? $"{access} partial record class {typeSymbol.Name}"
				: $"{access} partial class {typeSymbol.Name}";
		}

		var ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
			? null
			: typeSymbol.ContainingNamespace.ToDisplayString();
		var fullyQualifiedName = typeSymbol.ToDisplayString(
			SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
				SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
					| SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
			)
		);

		return new GeneratedTypeModel(typeSymbol.Name, ns, declaration, fullyQualifiedName);
	}

	public static string BuildHintName(INamedTypeSymbol typeSymbol, string suffix)
	{
		var fullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
		var hash = ComputeStableHash(fullName);
		return $"{typeSymbol.Name}_{suffix}_{hash:X16}.g.cs";
	}

	public static ulong ComputeStableHash(string value)
	{
		const ulong offsetBasis = 14695981039346656037;
		const ulong prime = 1099511628211;

		var hash = offsetBasis;
		foreach (var character in value)
		{
			hash ^= character;
			hash *= prime;
		}

		return hash;
	}

	public static string ToTypeName(ITypeSymbol typeSymbol) =>
		typeSymbol.ToDisplayString(
			SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
				SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
					| SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
			)
		);

	public static string ToCamelCase(string value) =>
		string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value.Substring(1);

	public static string GetEmptyValueExpression(ITypeSymbol typeSymbol)
	{
		return typeSymbol.IsReferenceType
			? typeSymbol.NullableAnnotation == NullableAnnotation.Annotated
				? "null"
				: "null!"
			: typeSymbol is INamedTypeSymbol namedTypeSymbol
			&& namedTypeSymbol.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
				? "null"
				: TypeLibrary.System.Guid.Equals(typeSymbol) switch
				{
					true => $"{TypeLibrary.System.Guid}.Empty",
					false => "default",
				};
	}

	public static string GetConstructorArgumentExpression(ITypeSymbol typeSymbol)
	{
		if (typeSymbol.IsReferenceType)
			return $"({ToTypeName(typeSymbol)})null!";

		if (
			typeSymbol is INamedTypeSymbol namedTypeSymbol
			&& namedTypeSymbol.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
		)
		{
			return "null";
		}

		// For value types, we can use the default literal, but for Guid, we want to use Guid.Empty
		return TypeLibrary.System.Guid.Equals(typeSymbol) ? $"{TypeLibrary.System.Guid}.Empty" : "default";
	}

	public static IFieldSymbol[] GetEnumFields(ITypeSymbol enumTypeSymbol) =>
		[
			.. enumTypeSymbol
				.GetMembers()
				.OfType<IFieldSymbol>()
				.Where(field => field.HasConstantValue && field.DeclaredAccessibility == Accessibility.Public)
				.OrderBy(field => field.Locations.FirstOrDefault()?.SourceSpan.Start ?? int.MaxValue),
		];

	public static bool HasMemberWithName(INamedTypeSymbol typeSymbol, string name) =>
		typeSymbol.GetMembers(name).Any(member => !member.IsImplicitlyDeclared);

	public const string InsteadOfHooksModeName =
		"global::" + TypeLibrary.Purview.ValueObjects.Serialization.ZodSchemaModeFullName + ".InsteadOfHooks";

	public const string StrictModeName =
		"global::" + TypeLibrary.Purview.ValueObjects.Serialization.ValueObjectDeserializationModeFullName + ".Strict";

	/// <summary>
	/// True when the value object is also annotated with ZodSharp's <c>[ZodSchema]</c> attribute.
	/// The attribute type is generated into the <c>ZodSharp</c> namespace by the ZodSharp source
	/// generator, so detection is by name rather than a compile-time reference.
	/// </summary>
	public static bool HasZodSchemaAttribute(INamedTypeSymbol typeSymbol) =>
		typeSymbol
			.GetAttributes()
			.Any(attribute =>
				attribute.AttributeClass?.Name == "ZodSchemaAttribute"
				&& attribute.AttributeClass.ContainingNamespace.ToDisplayString() == "ZodSharp"
			);

	/// <summary>
	/// Resolves the source-generated schema class name for a <c>[ZodSchema]</c>-annotated type,
	/// honoring <c>[ZodSchema(SchemaName = "...")]</c>. Returns the default
	/// <c>{TypeName}Schema</c> when no schema name is specified.
	/// </summary>
	public static string? GetZodSchemaClassName(INamedTypeSymbol typeSymbol) =>
		HasZodSchemaAttribute(typeSymbol)
			? GetZodSchemaClassName(
				typeSymbol,
				typeSymbol
					.GetAttributes()
					.First(attribute =>
						attribute.AttributeClass?.Name == "ZodSchemaAttribute"
						&& attribute.AttributeClass.ContainingNamespace.ToDisplayString() == "ZodSharp"
					)
			)
			: null;

	static string GetZodSchemaClassName(INamedTypeSymbol typeSymbol, AttributeData zodSchemaAttribute)
	{
		var schemaName = zodSchemaAttribute
			.NamedArguments.Where(argument => string.Equals(argument.Key, "SchemaName", StringComparison.Ordinal))
			.Select(static argument => argument.Value.Value as string)
			.FirstOrDefault();
		return string.IsNullOrWhiteSpace(schemaName) ? typeSymbol.Name + "Schema" : schemaName!;
	}

	const string EntityFrameworkMappingTypeName = TypeLibrary
		.Purview
		.ValueObjects
		.Serialization
		.EntityFrameworkMappingFullName;

	public static bool IsEFMappingComplexType(string value) => MatchesEFMapping(value, "ComplexType");

	public static bool IsEFMappingJson(string value) => MatchesEFMapping(value, "Json");

	public static bool IsEFMappingNone(string value) => MatchesEFMapping(value, "None");

	static bool MatchesEFMapping(string value, string member) =>
		value == $"{EntityFrameworkMappingTypeName}.{member}"
		|| value == $"global::{EntityFrameworkMappingTypeName}.{member}";

	/// <summary>
	/// True when the compilation references Entity Framework Core's value conversion types. This gates all
	/// Entity Framework member generation: without the reference, no <c>EF</c> members or mapping
	/// extensions are emitted, keeping the runtime package free of Entity Framework dependencies.
	/// </summary>
	public static bool IsEFReferenced(Compilation compilation) =>
		compilation.GetTypeByMetadataName(
			TypeLibrary.Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverterFullName
		)
			is not null;

	/// <summary>
	/// True when the compilation references EF Core 8+, which introduced complex types
	/// (<c>EntityTypeBuilder.ComplexProperty</c>).
	/// </summary>
	public static bool IsEF8Referenced(Compilation compilation) =>
		compilation.GetTypeByMetadataName(TypeLibrary.Microsoft.EntityFrameworkCore.Metadata.IComplexTypeFullName)
			is not null;

	/// <summary>
	/// True when <paramref name="typeSymbol"/> is a provider type Entity Framework Core can map natively
	/// (primitives, enums, <see cref="Guid"/>, dates, <c>TimeSpan</c>, <c>byte[]</c>, and nullable forms).
	/// Used to decide whether a scalar value object can be automatically converted to a primitive column.
	/// </summary>
	public static bool IsEFMappableProviderType(ITypeSymbol typeSymbol)
	{
		if (typeSymbol.TypeKind == TypeKind.Enum)
			return true;

		if (typeSymbol is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte })
			return true;

		if (typeSymbol is not INamedTypeSymbol named)
			return false;

		if (named.IsGenericType && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
			return IsEFMappableProviderType(named.TypeArguments[0]);

		// EF Core 8+ supports mapping of complex types, but we only want to treat the well-known provider types as mappable for now.
#pragma warning disable IDE0072 // Add missing cases
		return named.SpecialType switch
		{
			SpecialType.System_Boolean
			or SpecialType.System_Char
			or SpecialType.System_SByte
			or SpecialType.System_Byte
			or SpecialType.System_Int16
			or SpecialType.System_UInt16
			or SpecialType.System_Int32
			or SpecialType.System_UInt32
			or SpecialType.System_Int64
			or SpecialType.System_UInt64
			or SpecialType.System_Single
			or SpecialType.System_Double
			or SpecialType.System_Decimal
			or SpecialType.System_String
			or SpecialType.System_DateTime => true,
			_ => IsWellKnownEFMappableType(named),
		};
#pragma warning restore IDE0072 // Add missing cases
	}

	static bool IsWellKnownEFMappableType(INamedTypeSymbol named) =>
		TypeLibrary.System.Guid.Equals(named)
		|| TypeLibrary.System.DateTimeOffset.Equals(named)
		|| TypeLibrary.System.DateOnly.Equals(named)
		|| TypeLibrary.System.TimeOnly.Equals(named)
		|| TypeLibrary.System.TimeSpan.Equals(named);

	/// <summary>
	/// Resolves the provider type an Entity Framework Core converter should use for a scalar property, plus
	/// the cast applied when hydrating from that provider value (or <see langword="null"/> when the provider
	/// type is the scalar property type itself).
	/// </summary>
	/// <remarks>
	/// An enum-backed scalar converts through the enum's underlying integral type. Leaving the enum as the
	/// provider type makes Entity Framework Core compose its own enum-to-number converter with the generated
	/// converter, and the composed converter loses the generated converter's provider tolerance — so a query
	/// comparing the property to a raw enum value would still throw.
	/// See https://github.com/dotnet/efcore/issues/32030.
	/// </remarks>
	public static (ITypeSymbol ProviderType, string? HydrateCastTypeName) ResolveEFProviderType(ITypeSymbol scalarType)
	{
		INamedTypeSymbol? nullableScalar = null;
		if (
			scalarType is INamedTypeSymbol namedType
			&& namedType.IsGenericType
			&& namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
		)
			nullableScalar = namedType;

		var underlyingType = nullableScalar is null ? scalarType : nullableScalar.TypeArguments[0];
		if (underlyingType.TypeKind != TypeKind.Enum)
			return (scalarType, null);

		var integralType = ((INamedTypeSymbol)underlyingType).EnumUnderlyingType!;

		return (
			nullableScalar is not null ? nullableScalar.OriginalDefinition.Construct(integralType) : integralType,
			ToTypeName(scalarType)
		);
	}
}
