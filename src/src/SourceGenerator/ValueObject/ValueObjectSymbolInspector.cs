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

	/// <summary>
	/// True when <paramref name="typeSymbol"/> declares an instance <c>Equals</c> overload for
	/// <paramref name="parameterType"/> whose nullability satisfies <c>IEquatable&lt;T&gt;.Equals(T?)</c>.
	/// A non-nullable reference-type parameter does not, so adding the interface would report CS8767; the
	/// generator leaves the interface off in that case and lets the author own it.
	/// </summary>
	public static bool HasNullabilityCompatibleEquals(INamedTypeSymbol typeSymbol, ITypeSymbol parameterType)
	{
		foreach (var method in typeSymbol.GetMembers("Equals").OfType<IMethodSymbol>())
		{
			if (method.IsStatic || method.Parameters.Length != 1)
				continue;

			var parameter = method.Parameters[0];
			if (!SymbolEqualityComparer.Default.Equals(parameter.Type, parameterType))
				continue;

			if (parameter.Type.IsReferenceType && parameter.Type.NullableAnnotation != NullableAnnotation.Annotated)
				continue;

			return true;
		}

		return false;
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

	/// <summary>
	/// True when the scalar's underlying value is <see cref="Guid"/>. Entity Framework Core value
	/// generators are emitted for these value objects only: a time-ordered identifier is meaningful for a
	/// <see cref="Guid"/> key and nothing else.
	/// </summary>
	public static bool IsGuidProviderType(ITypeSymbol type) => TypeLibrary.System.Guid.Equals(type);

	/// <summary>
	/// True when the member has a setter that is not <c>init</c>. Value objects must be immutable: a
	/// mutable member can change the value after it has taken part in equality, hashing, or change tracking.
	/// </summary>
	public static bool IsMutableMember(IPropertySymbol member) => member.SetMethod is { IsInitOnly: false };

	/// <summary>Reports every mutable member of a value object.</summary>
	public static void CollectMutableMemberDiagnostics(
		INamedTypeSymbol typeSymbol,
		IEnumerable<IPropertySymbol> members,
		List<ReportableDiagnostic> diagnostics
	)
	{
		foreach (var member in members)
		{
			if (!IsMutableMember(member))
				continue;

			diagnostics.Add(
				ReportableDiagnostic.Create(
					DiagnosticLibrary.ValueObjectMemberIsMutable,
					isBlocking: false,
					member.Locations.FirstOrDefault(static location => location.IsInSource)
						?? typeSymbol.Locations.FirstOrDefault(static location => location.IsInSource),
					typeSymbol.Name,
					member.Name
				)
			);
		}
	}

	/// <summary>
	/// True when the generated Entity Framework Core complex-type mapping can convert the member: a
	/// provider-mappable primitive or string, an enum, or a value object with Entity Framework support of
	/// its own (its own converter, or a non-<c>None</c> complex/JSON mapping).
	/// </summary>
	public static bool IsSupportedComplexMember(ITypeSymbol memberType)
	{
		var type = memberType is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T }
			? ((INamedTypeSymbol)memberType).TypeArguments[0]
			: memberType;

		if (type.SpecialType == SpecialType.System_String || type.TypeKind == TypeKind.Enum)
			return true;

		if (IsEFMappableProviderType(type))
			return true;

		var attributes = type.GetAttributes();
		var assemblyDefaults = ValueObjectDefaultsAttributeData.FromAttributeData(
			type.ContainingAssembly?.GetAttributes() ?? []
		);

		if (ScalarAttributeParser.Find(attributes) is { } scalarAttribute)
		{
			var scalarOptions = ValueObjectDefaultsHelper.Apply(
				ScalarAttributeParser.Parse(scalarAttribute),
				assemblyDefaults,
				attributes
			);

			return scalarOptions.GenerateEFConverter && scalarOptions.GenerateEFComparer;
		}

		if (HasAttribute(attributes, TypeLibrary.Purview.ValueObjects.Serialization.ValueObjectAttribute))
		{
			var complexOptions = ValueObjectDefaultsHelper.Apply(
				ValueObjectAttributeData.FromAttributeData(attributes),
				assemblyDefaults,
				attributes
			);

			return complexOptions.GenerateEFComparer
				&& (complexOptions.EFMapping is null || !IsEFMappingNone(complexOptions.EFMapping));
		}

		return false;
	}

	/// <summary>
	/// True when the member is a collection. Entity Framework Core complex types do not map collections, so
	/// they cannot be converted by the generated complex-type mapping.
	/// </summary>
	public static bool IsCollectionMember(ITypeSymbol memberType)
	{
		if (memberType.SpecialType == SpecialType.System_String)
			return false;

		if (memberType is IArrayTypeSymbol)
			return true;

		// The member is a collection if it implements IEnumerable<T> (or a derived interface). This is the same
		return memberType is INamedTypeSymbol named
			&& named.AllInterfaces.Any(static iface =>
				iface.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T
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

	/// <summary>
	/// True when the caller supplies a body for the hook the generator declares for
	/// <paramref name="methodName"/> with <paramref name="parameterCount"/> parameters. A body paired with
	/// a generated declaration is the supported way to opt in to a hook, so this is what tells the
	/// emitters - and the diagnostics - that the hook is actually implemented.
	/// </summary>
	public static bool HasHookImplementation(INamedTypeSymbol typeSymbol, string methodName, int parameterCount) =>
		GetComplexHookDeclarations(typeSymbol, methodName, parameterCount)
			.Any(static method => method.Body is not null || method.ExpressionBody is not null);

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

	/// <summary>
	/// True when the underlying scalar value supports .NET formatting, so the generated value object can
	/// forward <c>ToString(string?, IFormatProvider?)</c> to it. A nullable value type is unwrapped first,
	/// because <see cref="Nullable{T}"/> itself implements no interfaces even when <c>T</c> does; a nullable
	/// reference annotation does not change the implemented set.
	/// </summary>
	public static bool ImplementsIFormattable(ITypeSymbol type)
	{
		var effectiveType = UnwrapNullable(type);

		return effectiveType.AllInterfaces.Any(interfaceSymbol =>
			interfaceSymbol.Name == nameof(IFormattable)
			&& interfaceSymbol.ContainingNamespace.ToDisplayString() == "System"
		);
	}

	/// <summary>
	/// The underlying type behind a nullable value type, or the type itself. A nullable value type is not
	/// the CLR type whose members matter — <see cref="Nullable{T}"/> declares only its own parameterless
	/// members — so overloads and interfaces must be resolved against <c>T</c>.
	/// </summary>
	public static ITypeSymbol UnwrapNullable(ITypeSymbol type) =>
		type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
			? nullable.TypeArguments[0]
			: type;

	/// <summary>
	/// True when <paramref name="type"/>, or a base type, declares an instance <c>ToString</c> overload whose
	/// parameters match <paramref name="parameterTypes"/> exactly. The base chain is walked so an enum's
	/// inherited <c>Enum.ToString(string)</c> and <c>Enum.ToString(string, IFormatProvider)</c> are seen;
	/// the parameterless <c>object.ToString</c> is not matched by either.
	/// </summary>
	public static bool DeclaresToString(ITypeSymbol type, IReadOnlyList<ITypeSymbol> parameterTypes)
	{
		for (var current = type as INamedTypeSymbol; current is not null; current = current.BaseType)
		{
			foreach (var member in current.GetMembers("ToString"))
			{
				if (
					member is IMethodSymbol { IsStatic: false } method
					&& method.Parameters.Length == parameterTypes.Count
					&& ParametersMatch(method.Parameters, parameterTypes)
				)
					return true;
			}
		}

		return false;
	}

	/// <summary>
	/// True when <paramref name="type"/> implements the named <c>System</c> generic interface closed over
	/// <paramref name="typeArgument"/> (for example <c>IEquatable&lt;T&gt;</c> or <c>IParsable&lt;T&gt;</c>).
	/// Nullable annotations are ignored, so a nullable reference scalar still matches its non-nullable form.
	/// </summary>
	public static bool ImplementsGenericInterface(ITypeSymbol type, string interfaceName, ITypeSymbol typeArgument) =>
		type.AllInterfaces.Any(interfaceSymbol =>
			interfaceSymbol is INamedTypeSymbol named
			&& named.OriginalDefinition.Name == interfaceName
			&& named.OriginalDefinition.ContainingNamespace.ToDisplayString() == "System"
			&& named.TypeArguments.Length == 1
			&& SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], typeArgument)
		);

	/// <summary>
	/// True when <paramref name="type"/> implements the named non-generic <c>System</c> interface (for
	/// example <c>ISpanFormattable</c>).
	/// </summary>
	public static bool ImplementsInterface(ITypeSymbol type, string interfaceName) =>
		type.AllInterfaces.Any(interfaceSymbol =>
			interfaceSymbol.Name == interfaceName && interfaceSymbol.ContainingNamespace.ToDisplayString() == "System"
		);

	/// <summary>
	/// True when <paramref name="typeSymbol"/> declares a static method with the given name whose parameters
	/// match <paramref name="parameterTypes"/> exactly. Used to avoid emitting a parsing member the author
	/// already declared (which would be a duplicate member, CS0111, in generated code).
	/// </summary>
	public static bool HasStaticMethod(
		INamedTypeSymbol typeSymbol,
		string name,
		IReadOnlyList<ITypeSymbol> parameterTypes
	) =>
		typeSymbol
			.GetMembers(name)
			.OfType<IMethodSymbol>()
			.Any(method =>
				method.IsStatic
				&& method.Parameters.Length == parameterTypes.Count
				&& ParametersMatch(method.Parameters, parameterTypes)
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
	/// The ZodSharp <c>[ZodSchema]</c> attribute applied to the type, or <see langword="null"/> when absent.
	/// The attribute type is generated into the <c>ZodSharp</c> namespace by the ZodSharp source
	/// generator, so detection is by name rather than a compile-time reference.
	/// </summary>
	public static AttributeData? GetZodSchemaAttribute(INamedTypeSymbol typeSymbol) =>
		typeSymbol
			.GetAttributes()
			.FirstOrDefault(attribute =>
				attribute.AttributeClass?.Name == TypeLibrary.ZodSchemaAttributeName
				&& attribute.AttributeClass.ContainingNamespace.ToDisplayString() == TypeLibrary.ZodSharpNamespace
			);

	/// <summary>
	/// Resolves everything the emitters need about a type's ZodSharp integration: whether <c>[ZodSchema]</c> is
	/// present and the generated schema class name (<c>[ZodSchema(SchemaName = "..." )]</c> aware).
	/// </summary>
	/// <remarks>
	/// Refinements — including the <c>OnZodValidate</c> hook — belong to the ZodSharp generator, which declares
	/// and invokes them inside the generated schema. The <c>Create</c> path only has to validate through that
	/// schema, so nothing here declares or invokes a refinement.
	/// </remarks>
	public static ZodSchemaIntegration ResolveZodSchemaIntegration(INamedTypeSymbol typeSymbol)
	{
		if (GetZodSchemaAttribute(typeSymbol) is not { } zodSchemaAttribute)
			return default;

		var schemaName = GetZodSchemaStringArgument(zodSchemaAttribute, "SchemaName");

		return new ZodSchemaIntegration(
			HasSchema: true,
			SchemaClassName: schemaName ?? (typeSymbol.Name + "Schema"),
			SchemaName: schemaName
		);
	}

	/// <summary>
	/// Collects the diagnostics for the ZodSharp integration states that are otherwise silent: an
	/// <c>OnValidate</c> implementation made unreachable by <c>ZodSchemaMode.InsteadOfHooks</c>, and a
	/// configured schema name the two generators would not resolve to the same identifier.
	/// </summary>
	/// <param name="typeSymbol">The annotated value object.</param>
	/// <param name="zodSchema">The resolved ZodSharp integration; nothing is reported without a schema.</param>
	/// <param name="zodSchemaMode">The effective <c>ZodSchemaMode</c> option of the value object.</param>
	/// <param name="onValidateImplemented">True when the caller supplies an <c>OnValidate</c> body that pairs with the generated declaration.</param>
	/// <param name="diagnostics">The diagnostic collection to append to.</param>
	public static void CollectZodSchemaDiagnostics(
		INamedTypeSymbol typeSymbol,
		ZodSchemaIntegration zodSchema,
		string? zodSchemaMode,
		bool onValidateImplemented,
		List<ReportableDiagnostic> diagnostics
	)
	{
		if (!zodSchema.HasSchema)
			return;

		var typeLocation = typeSymbol.Locations.FirstOrDefault(static location => location.IsInSource);

		// ZodSharp applies any non-empty SchemaName, so a value that is not a valid identifier (for
		// example whitespace) produces a schema class this generator cannot reference. An empty value
		// means "unset" to both generators and is therefore valid.
		if (zodSchema.SchemaName is { Length: > 0 } schemaName && !SyntaxFacts.IsValidIdentifier(schemaName))
		{
			diagnostics.Add(
				ReportableDiagnostic.Create(
					DiagnosticLibrary.ZodSchemaNameInvalid,
					isBlocking: true,
					GetZodSchemaAttributeLocation(typeSymbol) ?? typeLocation,
					typeSymbol.Name,
					schemaName
				)
			);
		}

		// InsteadOfHooks is a deliberate configuration, but it makes an implemented OnValidate unreachable.
		if (onValidateImplemented && string.Equals(zodSchemaMode, InsteadOfHooksModeName, StringComparison.Ordinal))
		{
			diagnostics.Add(
				ReportableDiagnostic.Create(
					DiagnosticLibrary.OnValidateSkippedByInsteadOfHooks,
					isBlocking: false,
					GetHookImplementationLocation(typeSymbol, "OnValidate")
						?? GetMemberLocation(typeSymbol, "OnValidate")
						?? typeLocation,
					typeSymbol.Name
				)
			);
		}
	}

	static Location? GetMemberLocation(INamedTypeSymbol typeSymbol, string name) =>
		typeSymbol
			.GetMembers(name)
			.FirstOrDefault(static member => !member.IsImplicitlyDeclared)
			?.Locations.FirstOrDefault(static location => location.IsInSource);

	/// <summary>
	/// Locates the caller's hook implementation by looking for a declaration with a body. The generated
	/// declaration never has one, so the location always lands in user code - which is what lets a
	/// <c>#pragma warning disable</c> suppress the diagnostic.
	/// </summary>
	static Location? GetHookImplementationLocation(INamedTypeSymbol typeSymbol, string methodName) =>
		typeSymbol
			.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax())
			.OfType<TypeDeclarationSyntax>()
			.SelectMany(declaration => declaration.Members.OfType<MethodDeclarationSyntax>())
			.Where(method => method.Identifier.Text == methodName)
			.FirstOrDefault(static method => method.Body is not null || method.ExpressionBody is not null)
			?.GetLocation();

	static Location? GetZodSchemaAttributeLocation(INamedTypeSymbol typeSymbol) =>
		GetZodSchemaAttribute(typeSymbol)?.ApplicationSyntaxReference?.GetSyntax().GetLocation();

	static string? GetZodSchemaStringArgument(AttributeData zodSchemaAttribute, string argumentName)
	{
		var value = zodSchemaAttribute
			.NamedArguments.Where(argument => string.Equals(argument.Key, argumentName, StringComparison.Ordinal))
			.Select(static argument => argument.Value.Value as string)
			.FirstOrDefault();
		return string.IsNullOrWhiteSpace(value) ? null : value;
	}

	/// <summary>
	/// Everything the emitters need to know about a type's ZodSharp integration. <see langword="default"/>
	/// means <c>[ZodSchema]</c> is not applied.
	/// </summary>
	/// <param name="HasSchema">True when <c>[ZodSchema]</c> is applied.</param>
	/// <param name="SchemaClassName">The generated schema class the <c>Create</c> path validates through.</param>
	/// <param name="SchemaName">The configured <c>SchemaName</c>, or <see langword="null"/> when unset.</param>
	public readonly record struct ZodSchemaIntegration(bool HasSchema, string? SchemaClassName, string? SchemaName);

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
	/// True when the compilation references the Purview.ZodSharp runtime. This is the only state in which the
	/// scalar rule adapter can compile: it forwards a rule written against a value object's underlying value,
	/// which is the <c>ZodSharp.Core.IValidationRule&lt;TValue&gt;</c> contract.
	/// </summary>
	public static bool IsZodSharpReferenced(Compilation compilation) =>
		compilation.GetTypeByMetadataName(TypeLibrary.ZodValidationRuleMetadataName) is not null;

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
		{
			// A nullable reference annotation is not representable in a typeof() expression, and it carries
			// no meaning for the provider CLR type, so strip it before it reaches the generated converter.
			return (
				scalarType.IsReferenceType
					? scalarType.WithNullableAnnotation(NullableAnnotation.NotAnnotated)
					: scalarType,
				null
			);
		}

		var integralType = ((INamedTypeSymbol)underlyingType).EnumUnderlyingType!;

		return (
			nullableScalar is not null ? nullableScalar.OriginalDefinition.Construct(integralType) : integralType,
			ToTypeName(scalarType)
		);
	}
}
