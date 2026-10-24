using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Purview.ValueObjects.SourceGenerator.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ValueObjectDiagnosticAnalyzer : DiagnosticAnalyzer
{
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
		[
			DiagnosticLibrary.ValueObjectMustBePartial,
			DiagnosticLibrary.NestedValueObjectsAreNotSupported,
			DiagnosticLibrary.GenericValueObjectsAreNotSupported,
			DiagnosticLibrary.ConflictingValueObjectAttributes,
			DiagnosticLibrary.ScalarPropertyMissing,
			DiagnosticLibrary.ScalarPropertyIsGenerated,
			DiagnosticLibrary.ScalarShouldBeRecordStruct,
			DiagnosticLibrary.StrictDeserializationRequiresCreate,
			DiagnosticLibrary.EFMappingRequiresEntityFramework,
			DiagnosticLibrary.EFAutoConversionSkipped,
			DiagnosticLibrary.OnValidateSkippedByInsteadOfHooks,
			DiagnosticLibrary.ZodSchemaNameInvalid,
			DiagnosticLibrary.EFValueGenerationUnavailable,
			DiagnosticLibrary.ValueObjectMemberIsMutable,
			DiagnosticLibrary.EFJsonMappingRequiresJsonConverter,
			DiagnosticLibrary.EFComplexMappingUnsupportedMember,
			DiagnosticLibrary.EFComplexTypeRequiresEntityFramework8,
			DiagnosticLibrary.StringNormalizationIgnored,
			DiagnosticLibrary.StringNormalizationRequiresString,
			DiagnosticLibrary.StringNormalizeRequiresValueObject,
			DiagnosticLibrary.AssemblyDefaultIsRedundant,
			DiagnosticLibrary.ValueObjectOptionIsRedundant,
			DiagnosticLibrary.ScalarNullabilityDeclaredTwice,
			DiagnosticLibrary.ScalarNullabilityContradictsProperty,
			DiagnosticLibrary.ScalarPropertyTypeMismatch,
			DiagnosticLibrary.NullableScalarPropertyShouldBeNullable,
		];

	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
			throw new ArgumentNullException(nameof(context));

		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterCompilationStartAction(compilationContext =>
		{
			var compilation = compilationContext.Compilation;
			var scalarAttribute = compilation.GetTypeByMetadataName(
				TypeLibrary.Purview.ValueObjects.Serialization.ScalarAttributeFullName
			);
			var genericScalarAttribute = compilation.GetTypeByMetadataName(
				TypeLibrary.ScalarAttributeGenericMetadataName
			);
			var valueObjectAttribute = compilation.GetTypeByMetadataName(
				TypeLibrary.Purview.ValueObjects.Serialization.ValueObjectAttributeFullName
			);
			if (scalarAttribute is null && genericScalarAttribute is null && valueObjectAttribute is null)
				return;

			var defaultsAttribute = compilation.GetTypeByMetadataName(
				TypeLibrary.ValueObjectDefaultsAttributeFullTypeName
			);
			if (defaultsAttribute is not null)
			{
				compilationContext.RegisterCompilationEndAction(context =>
					ValidateAssemblyDefaults(context, defaultsAttribute)
				);
			}

			compilationContext.RegisterCompilationEndAction(context =>
				ValidateNullableScalarProperties(context, scalarAttribute, genericScalarAttribute, valueObjectAttribute)
			);

			compilationContext.RegisterSymbolAction(
				context => ValidateValueObject(context, scalarAttribute, genericScalarAttribute, valueObjectAttribute),
				SymbolKind.NamedType
			);

			var stringNormalizeAttribute = compilation.GetTypeByMetadataName(
				TypeLibrary.StringNormalizeAttributeFullTypeName
			);
			if (stringNormalizeAttribute is not null)
			{
				compilationContext.RegisterSymbolAction(
					context =>
						ValidateStringNormalizePlacement(
							context,
							scalarAttribute,
							genericScalarAttribute,
							valueObjectAttribute,
							stringNormalizeAttribute
						),
					SymbolKind.Property
				);
			}
		});
	}

	static void ValidateValueObject(
		SymbolAnalysisContext context,
		INamedTypeSymbol? scalarAttribute,
		INamedTypeSymbol? genericScalarAttribute,
		INamedTypeSymbol? valueObjectAttribute
	)
	{
		var typeSymbol = (INamedTypeSymbol)context.Symbol;

		var hasScalarAttribute = HasScalarAttribute(typeSymbol, scalarAttribute, genericScalarAttribute);
		var hasValueObjectAttribute =
			valueObjectAttribute is not null && HasAttribute(typeSymbol, valueObjectAttribute);
		if (!hasScalarAttribute && !hasValueObjectAttribute)
			return;

		if (
			typeSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(context.CancellationToken)
			is not TypeDeclarationSyntax syntax
		)
			return;

		if (hasScalarAttribute)
		{
			var result = ScalarValueObjectModelBuilder.Build(
				typeSymbol,
				syntax,
				context.Compilation,
				context.CancellationToken
			);

			foreach (var diagnostic in result.Diagnostics)
				context.ReportDiagnostic(diagnostic.ToDiagnostic());
		}
		else
		{
			var result = ComplexValueObjectModelBuilder.Build(
				typeSymbol,
				syntax,
				context.Compilation,
				context.CancellationToken
			);

			foreach (var diagnostic in result.Diagnostics)
				context.ReportDiagnostic(diagnostic.ToDiagnostic());
		}

		ValidateTypeDefaults(context, typeSymbol, scalarAttribute, genericScalarAttribute, valueObjectAttribute);
	}

	/// <summary>
	/// Reports a nullable scalar used as a non-nullable property on a type Entity Framework Core maps.
	/// Entity Framework Core derives a column's nullability from the CLR property type, and it refuses to
	/// mark a non-nullable struct property optional (<c>CannotBeNullable</c>), so a nullable scalar
	/// declared without <c>?</c> is a required column that cannot persist its own <c>Null()</c> value.
	/// </summary>
	/// <remarks>
	/// Scoped to types the compilation can see being mapped — entity types discovered from
	/// <c>DbSet&lt;T&gt;</c> properties and <c>IEntityTypeConfiguration&lt;T&gt;</c> implementations, plus
	/// complex-type-mapped <c>[ValueObject]</c> types (whose members become columns). Plain DTOs are
	/// deliberately excluded: a DTO property of a nullable scalar is the supported way to represent the
	/// scalar's <c>Null()</c> value. Entity types configured only in a referenced assembly, or built
	/// entirely at runtime, are not discovered; the rule stays a warning for that reason.
	/// </remarks>
	static void ValidateNullableScalarProperties(
		CompilationAnalysisContext context,
		INamedTypeSymbol? scalarAttribute,
		INamedTypeSymbol? genericScalarAttribute,
		INamedTypeSymbol? valueObjectAttribute
	)
	{
		if (scalarAttribute is null && genericScalarAttribute is null)
			return;

		var compilation = context.Compilation;
		if (!ValueObjectSymbolInspector.IsEFReferenced(compilation))
			return;

		var notMappedAttribute = compilation.GetTypeByMetadataName(
			"System.ComponentModel.DataAnnotations.Schema.NotMappedAttribute"
		);
		var keyAttribute = compilation.GetTypeByMetadataName("System.ComponentModel.DataAnnotations.KeyAttribute");

		var mappedTypes = CollectMappedTypes(context, valueObjectAttribute);

		HashSet<IPropertySymbol> reported = new(SymbolEqualityComparer.Default);
		foreach (var type in mappedTypes)
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			for (var current = type; current is not null; current = current.BaseType)
			{
				foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
				{
					if (!reported.Add(property))
						continue;

					if (property.IsStatic || property.IsIndexer)
						continue;

					if (notMappedAttribute is not null && HasAttribute(property.GetAttributes(), notMappedAttribute))
						continue;

					if (IsKeyProperty(property, type, keyAttribute))
						continue;

					if (property.Type is not INamedTypeSymbol { TypeKind: TypeKind.Struct } propertyType)
						continue;

					if (propertyType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
						continue;

					if (
						!ValueObjectSymbolInspector.TryGetScalarIsNullable(
							propertyType,
							compilation,
							out var isNullable
						) || !isNullable
					)
						continue;

					var location = property.Locations.FirstOrDefault(static location => location.IsInSource);
					if (location is null)
						continue;

					context.ReportDiagnostic(
						Diagnostic.Create(
							DiagnosticLibrary.NullableScalarPropertyShouldBeNullable,
							location,
							property.ContainingType.Name,
							property.Name,
							ValueObjectSymbolInspector.ToTypeName(property.Type)
						)
					);
				}
			}
		}
	}

	/// <summary>
	/// True when the property is a key: explicitly with <c>[Key]</c>, or by the <c>Id</c> /
	/// <c>{EntityTypeName}Id</c> convention Entity Framework Core uses. A key must be non-nullable, so the
	/// "declare the property as nullable" advice would be wrong for it.
	/// </summary>
	static bool IsKeyProperty(IPropertySymbol property, INamedTypeSymbol entityType, INamedTypeSymbol? keyAttribute) =>
		(keyAttribute is not null && HasAttribute(property.GetAttributes(), keyAttribute))
		|| string.Equals(property.Name, "Id", StringComparison.Ordinal)
		|| string.Equals(property.Name, entityType.Name + "Id", StringComparison.Ordinal);

	static bool DerivesFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
	{
		for (var current = type.BaseType; current is not null; current = current.BaseType)
		{
			if (SymbolEqualityComparer.Default.Equals(current, baseType))
				return true;
		}

		return false;
	}

	/// <summary>
	/// The types whose members Entity Framework Core maps as columns in this compilation: entities
	/// discovered from <c>DbSet&lt;T&gt;</c> properties and <c>IEntityTypeConfiguration&lt;T&gt;</c>
	/// implementations, plus <c>[ValueObject]</c> types mapped as complex types. A JSON-mapped complex
	/// type is a single column, so its members' nullability does not affect the schema; complex-type
	/// mapping needs Entity Framework Core 8 and later. The two scans are combined so the compilation's
	/// source types are walked once.
	/// </summary>
	static HashSet<INamedTypeSymbol> CollectMappedTypes(
		CompilationAnalysisContext context,
		INamedTypeSymbol? valueObjectAttribute
	)
	{
		var compilation = context.Compilation;
		var cancellationToken = context.CancellationToken;
		HashSet<INamedTypeSymbol> mappedTypes = new(SymbolEqualityComparer.Default);

		var dbContext = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbContext");
		var dbSet = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbSet`1");
		var entityConfiguration = compilation.GetTypeByMetadataName(
			"Microsoft.EntityFrameworkCore.IEntityTypeConfiguration`1"
		);
		var complexMappingEnabled =
			valueObjectAttribute is not null && ValueObjectSymbolInspector.IsEF8Referenced(compilation);

		foreach (
			var type in compilation
				.GetSymbolsWithName(static _ => true, SymbolFilter.Type, cancellationToken)
				.OfType<INamedTypeSymbol>()
		)
		{
			cancellationToken.ThrowIfCancellationRequested();

			// Entity discovered from the context's DbSet<T> properties.
			if (dbContext is not null && dbSet is not null && DerivesFrom(type, dbContext))
			{
				foreach (var property in type.GetMembers().OfType<IPropertySymbol>())
				{
					if (
						property.Type is INamedTypeSymbol { IsGenericType: true } propertyType
						&& SymbolEqualityComparer.Default.Equals(propertyType.OriginalDefinition, dbSet)
						&& propertyType.TypeArguments.Length == 1
						&& propertyType.TypeArguments[0] is INamedTypeSymbol entityType
					)
						mappedTypes.Add(entityType);
				}
			}

			// Entity discovered from an IEntityTypeConfiguration<T> implementation.
			if (entityConfiguration is not null)
			{
				foreach (var @interface in type.AllInterfaces)
				{
					if (
						@interface is INamedTypeSymbol { IsGenericType: true } configured
						&& SymbolEqualityComparer.Default.Equals(configured.OriginalDefinition, entityConfiguration)
						&& configured.TypeArguments.Length == 1
						&& configured.TypeArguments[0] is INamedTypeSymbol entityType
					)
						mappedTypes.Add(entityType);
				}
			}

			if (!complexMappingEnabled)
				continue;

			// A complex-type-mapped [ValueObject]: its members become columns.
			var attributes = type.GetAttributes();
			if (!HasAttribute(attributes, valueObjectAttribute!))
				continue;

			var options = ValueObjectDefaultsHelper.Apply(
				ValueObjectAttributeData.FromAttributeData(attributes),
				ValueObjectDefaultsHelper.ResolveAssemblyDefaults(type.ContainingAssembly.GetAttributes()),
				attributes
			);

			if (options.EFMapping is not null && ValueObjectSymbolInspector.IsEFMappingNone(options.EFMapping))
				continue;

			if (options.EFMapping is not null && ValueObjectSymbolInspector.IsEFMappingJson(options.EFMapping))
				continue;

			mappedTypes.Add(type);
		}

		return mappedTypes;
	}

	/// <summary>
	/// Reports <c>[StringNormalize]</c> on a member of a type that is neither a <c>[Scalar]</c> nor a
	/// <c>[ValueObject]</c>. Those two shapes are validated by the model builders, which report the same
	/// rule with a context-specific reason; reporting here too would duplicate the diagnostic.
	/// </summary>
	static void ValidateStringNormalizePlacement(
		SymbolAnalysisContext context,
		INamedTypeSymbol? scalarAttribute,
		INamedTypeSymbol? genericScalarAttribute,
		INamedTypeSymbol? valueObjectAttribute,
		INamedTypeSymbol stringNormalizeAttribute
	)
	{
		if (context.Symbol is not IPropertySymbol property)
			return;

		var attribute = property
			.GetAttributes()
			.FirstOrDefault(candidate =>
				SymbolEqualityComparer.Default.Equals(candidate.AttributeClass, stringNormalizeAttribute)
			);
		if (attribute?.ApplicationSyntaxReference is not { } application)
			return;

		var containingType = property.ContainingType;
		if (containingType is null)
			return;

		if (
			(scalarAttribute is not null && HasAttribute(containingType, scalarAttribute))
			|| (genericScalarAttribute is not null && HasAttribute(containingType, genericScalarAttribute))
			|| (valueObjectAttribute is not null && HasAttribute(containingType, valueObjectAttribute))
		)
			return;

		context.ReportDiagnostic(
			Diagnostic.Create(
				DiagnosticLibrary.StringNormalizeRequiresValueObject,
				application.GetSyntax(context.CancellationToken).GetLocation(),
				containingType.Name,
				property.Name,
				"is neither a [Scalar] nor a [ValueObject] type"
			)
		);
	}

	/// <summary>
	/// Reports an author-declared assembly default that repeats a value the packaged targets already
	/// supplied from a <c>ValueObjectsDefault*</c> MSBuild property.
	/// </summary>
	static void ValidateAssemblyDefaults(CompilationAnalysisContext context, INamedTypeSymbol defaultsAttribute)
	{
		var attributes = context
			.Compilation.Assembly.GetAttributes()
			.Where(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, defaultsAttribute))
			.ToArray();

		var buildAttribute = attributes.FirstOrDefault(ValueObjectDefaultsHelper.IsBuildGenerated);
		if (buildAttribute is null)
			return;

		var projectDefaults = ValueObjectDefaultsAttributeData.FromAttributeData(buildAttribute);
		foreach (
			var authorAttribute in attributes.Where(attribute => !ValueObjectDefaultsHelper.IsBuildGenerated(attribute))
		)
		{
			ReportRedundantOptions(
				context.ReportDiagnostic,
				authorAttribute,
				projectDefaults,
				typeName: null,
				DiagnosticLibrary.AssemblyDefaultIsRedundant,
				configuredOptions: buildAttribute,
				context.CancellationToken
			);
		}
	}

	/// <summary>
	/// Reports a <c>[Scalar]</c> or <c>[ValueObject]</c> option that repeats the effective assembly
	/// default. The option is redundant: removing it produces exactly the same configuration.
	/// </summary>
	static void ValidateTypeDefaults(
		SymbolAnalysisContext context,
		INamedTypeSymbol typeSymbol,
		INamedTypeSymbol? scalarAttribute,
		INamedTypeSymbol? genericScalarAttribute,
		INamedTypeSymbol? valueObjectAttribute
	)
	{
		var assemblyDefaults = ValueObjectDefaultsHelper.ResolveAssemblyDefaults(
			context.Compilation.Assembly.GetAttributes()
		);
		if (!assemblyDefaults.Exists)
			return;

		foreach (var attribute in typeSymbol.GetAttributes())
		{
			if (
				(
					scalarAttribute is not null
					&& SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, scalarAttribute)
				)
				|| (
					genericScalarAttribute is not null
					&& SymbolEqualityComparer.Default.Equals(
						attribute.AttributeClass?.OriginalDefinition,
						genericScalarAttribute
					)
				)
				|| (
					valueObjectAttribute is not null
					&& SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, valueObjectAttribute)
				)
			)
			{
				ReportRedundantOptions(
					context.ReportDiagnostic,
					attribute,
					assemblyDefaults,
					GetAttributeDisplayName(attribute),
					DiagnosticLibrary.ValueObjectOptionIsRedundant,
					configuredOptions: null,
					context.CancellationToken
				);
			}
		}
	}

	/// <summary>
	/// Compares the explicit named arguments on <paramref name="attribute"/> with the inherited defaults
	/// and reports each argument that carries the same value. A positional constructor argument is not an
	/// option and is never compared.
	/// </summary>
	static void ReportRedundantOptions(
		Action<Diagnostic> report,
		AttributeData attribute,
		ValueObjectDefaultsAttributeData defaults,
		string? typeName,
		DiagnosticDescriptor descriptor,
		AttributeData? configuredOptions,
		CancellationToken cancellationToken
	)
	{
		foreach (var argument in attribute.NamedArguments)
		{
			var optionName = argument.Key;
			var constant = argument.Value;
			string? value;

			// For the assembly layer, only an option the project actually configured can be repeated. The
			// baseline data carries built-in defaults for everything else, and those are not "project"
			// configuration.
			if (
				configuredOptions is not null
				&& !configuredOptions.NamedArguments.Any(candidate =>
					string.Equals(candidate.Key, optionName, StringComparison.Ordinal)
				)
			)
			{
				continue;
			}

			if (TryGetBooleanDefault(defaults, optionName, out var expectedBoolean))
			{
				if (constant.Value is not bool actualBoolean || actualBoolean != expectedBoolean)
					continue;

				value = actualBoolean ? "true" : "false";
			}
			else if (TryGetEnumDefault(defaults, optionName, out var expectedEnum))
			{
				var actualEnum = GetEnumMemberName(constant);
				if (actualEnum is null || !IsSameEnumMember(expectedEnum, actualEnum))
					continue;

				value = actualEnum;
			}
			else
			{
				continue;
			}

			var location =
				GetArgumentLocation(attribute, optionName, cancellationToken)
				?? attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation();
			if (location is null)
				continue;

			object[] messageArguments = typeName is null ? [optionName, value] : [typeName, optionName, value];
			report(Diagnostic.Create(descriptor, location, messageArguments));
		}
	}

	static bool TryGetBooleanDefault(ValueObjectDefaultsAttributeData defaults, string optionName, out bool value)
	{
		switch (optionName)
		{
			case "GenerateJsonConverter":
				value = defaults.GenerateJsonConverter;
				return true;
			case "GenerateComparable":
				value = defaults.GenerateComparable;
				return true;
			case "GenerateComparisonOperators":
				value = defaults.GenerateComparisonOperators;
				return true;
			case "GenerateEnumProperties":
				value = defaults.GenerateEnumProperties;
				return true;
			case "GenerateImplicitFromPrimitive":
				value = defaults.GenerateImplicitFromPrimitive;
				return true;
			case "GenerateImplicitToPrimitive":
				value = defaults.GenerateImplicitToPrimitive;
				return true;
			case "GenerateEmpty":
				value = defaults.GenerateEmpty;
				return true;
			case "GenerateConstructor":
				value = defaults.GenerateConstructor;
				return true;
			case "GenerateEFConverter":
				value = defaults.GenerateEFConverter;
				return true;
			case "GenerateEFComparer":
				value = defaults.GenerateEFComparer;
				return true;
			case "GenerateEFValueGenerator":
				value = defaults.GenerateEFValueGenerator;
				return true;
			case "Trim":
				value = defaults.Trim;
				return true;
			default:
				value = false;
				return false;
		}
	}

	static bool TryGetEnumDefault(ValueObjectDefaultsAttributeData defaults, string optionName, out string value)
	{
		switch (optionName)
		{
			case "EFMapping":
				value = defaults.EFMapping;
				return true;
			case "Casing":
				value = defaults.Casing;
				return true;
			case "DeserializationMode":
				value = defaults.DeserializationMode;
				return true;
			case "ZodSchemaMode":
				value = defaults.ZodSchemaMode;
				return true;
			default:
				value = string.Empty;
				return false;
		}
	}

	static string? GetEnumMemberName(TypedConstant constant)
	{
		if (constant.Type is not INamedTypeSymbol enumType)
			return null;

		foreach (var member in enumType.GetMembers().OfType<IFieldSymbol>())
		{
			if (member.HasConstantValue && Equals(member.ConstantValue, constant.Value))
				return member.Name;
		}

		return null;
	}

	static bool IsSameEnumMember(string expectedCanonicalValue, string actualMemberName)
	{
		var separator = expectedCanonicalValue.LastIndexOf('.');
		var expectedMemberName =
			separator < 0 ? expectedCanonicalValue : expectedCanonicalValue.Substring(separator + 1);
		return string.Equals(expectedMemberName, actualMemberName, StringComparison.Ordinal);
	}

	static Location? GetArgumentLocation(
		AttributeData attribute,
		string optionName,
		CancellationToken cancellationToken
	)
	{
		if (attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken) is not AttributeSyntax syntax)
			return null;

		var argument = syntax.ArgumentList?.Arguments.FirstOrDefault(candidate =>
			string.Equals(candidate.NameEquals?.Name.Identifier.ValueText, optionName, StringComparison.Ordinal)
		);
		return argument?.GetLocation();
	}

	static string GetAttributeDisplayName(AttributeData attribute)
	{
		var name = attribute.AttributeClass?.Name ?? "ValueObject";
		const string suffix = "Attribute";
		return name.EndsWith(suffix, StringComparison.Ordinal) ? name.Substring(0, name.Length - suffix.Length) : name;
	}

	static bool HasScalarAttribute(
		INamedTypeSymbol typeSymbol,
		INamedTypeSymbol? scalarAttribute,
		INamedTypeSymbol? genericScalarAttribute
	) =>
		(scalarAttribute is not null && HasAttribute(typeSymbol, scalarAttribute))
		|| (genericScalarAttribute is not null && HasAttribute(typeSymbol, genericScalarAttribute));

	static bool HasAttribute(INamedTypeSymbol typeSymbol, INamedTypeSymbol attributeType) =>
		typeSymbol
			.GetAttributes()
			.Any(attribute =>
				SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType)
				// A generic attribute is applied as a constructed type (ScalarAttribute<Guid>), while the
				// definition resolved from metadata is the open generic (ScalarAttribute<T>).
				|| SymbolEqualityComparer.Default.Equals(attribute.AttributeClass?.OriginalDefinition, attributeType)
			);

	static bool HasAttribute(ImmutableArray<AttributeData> attributes, INamedTypeSymbol attributeType) =>
		attributes.Any(attribute =>
			SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType)
			|| SymbolEqualityComparer.Default.Equals(attribute.AttributeClass?.OriginalDefinition, attributeType)
		);
}
