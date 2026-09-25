namespace Purview.ValueObjects.SourceGenerator.ValueObject;

/// <summary>
/// Discovers Entity Framework Core-enabled value objects declared in referenced assemblies. When a
/// referenced assembly references <c>Microsoft.EntityFrameworkCore</c> its value objects carry the
/// <c>IEFScalarValueObject</c>/<c>IEFComplexValueObject</c> marker interfaces and are discovered from
/// those; otherwise (the declaring assembly stays free of Entity Framework dependencies) they are
/// discovered from their <c>[Scalar]</c>/<c>[ValueObject]</c> attributes and the consumer's generated
/// <c>ValueObjectEFExtensions</c> registry maps them with inline conversions.
/// </summary>
static class ReferencedEFValueObjectDiscovery
{
	const string MarkerNamespace = "Purview.ValueObjects";

	const string RuntimeAssemblyName = "Purview.ValueObjects";

	const string ScalarMarkerName = "IEFScalarValueObject";

	const string ComplexMarkerName = "IEFComplexValueObject";

	public static ReferencedEFValueObjectDiscoveryResult Scan(
		Compilation compilation,
		CancellationToken cancellationToken
	)
	{
		if (!ValueObjectSymbolInspector.IsEFReferenced(compilation))
			return ReferencedEFValueObjectDiscoveryResult.Empty;

		var scalarBuilder = ImmutableArray.CreateBuilder<EFScalarDescriptor>();
		var complexBuilder = ImmutableArray.CreateBuilder<EFComplexDescriptor>();
		var isEF8Referenced = ValueObjectSymbolInspector.IsEF8Referenced(compilation);

		foreach (var reference in compilation.References)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly)
				continue;

			// A value object provider assembly implements the markers rather than declaring them, so
			// gate the scan on whether it references the runtime that defines them.
			if (!ReferencesValueObjectsRuntime(assembly))
				continue;

			ScanNamespace(assembly.GlobalNamespace, scalarBuilder, complexBuilder, isEF8Referenced, cancellationToken);
		}

		return new(scalarBuilder.ToImmutable(), complexBuilder.ToImmutable());
	}

	static bool ReferencesValueObjectsRuntime(IAssemblySymbol assembly)
	{
		foreach (var module in assembly.Modules)
		{
			foreach (var referencedAssembly in module.ReferencedAssemblySymbols)
			{
				if (referencedAssembly.Name == RuntimeAssemblyName)
					return true;
			}
		}

		return false;
	}

	static void ScanNamespace(
		INamespaceSymbol namespaceSymbol,
		ImmutableArray<EFScalarDescriptor>.Builder scalarBuilder,
		ImmutableArray<EFComplexDescriptor>.Builder complexBuilder,
		bool isEF8Referenced,
		CancellationToken cancellationToken
	)
	{
		foreach (var member in namespaceSymbol.GetMembers())
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (member is INamespaceSymbol childNamespace)
				ScanNamespace(childNamespace, scalarBuilder, complexBuilder, isEF8Referenced, cancellationToken);
			else if (member is INamedTypeSymbol type)
				ScanType(type, scalarBuilder, complexBuilder, isEF8Referenced, cancellationToken);
		}
	}

	static void ScanType(
		INamedTypeSymbol type,
		ImmutableArray<EFScalarDescriptor>.Builder scalarBuilder,
		ImmutableArray<EFComplexDescriptor>.Builder complexBuilder,
		bool isEF8Referenced,
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (type.TypeKind is not (TypeKind.Struct or TypeKind.Class))
			return;

		// The consumer's generated registry can only reference public types from another assembly.
		if (type.DeclaredAccessibility != Accessibility.Public)
			return;

		if (type.ContainingType is not null || type.TypeParameters.Length > 0)
			return;

		var scalarMarker = FindMarkerInterface(type, ScalarMarkerName);
		if (scalarMarker is not null)
		{
			scalarBuilder.Add(
				new EFScalarDescriptor(
					ValueObjectSymbolInspector.ToTypeName(type),
					HasEFMember(type, "Converter"),
					HasEFMember(type, "Comparer"),
					ValueObjectSymbolInspector.IsEFMappableProviderType(scalarMarker.TypeArguments[1]),
					ProviderTypeName: null,
					ScalarPropertyName: null,
					EFProviderTypeName: null,
					EFHydrateCastTypeName: null,
					HasEFMembers: true
				)
			);
			return;
		}

		if (FindMarkerInterface(type, ComplexMarkerName) is not null)
		{
			var efMapping = ResolveEFMapping(type);
			complexBuilder.Add(
				new EFComplexDescriptor(
					ValueObjectSymbolInspector.ToTypeName(type),
					efMapping,
					isEF8Referenced,
					HasEFMember(type, "Comparer"),
					ValueObjectSymbolInspector.IsEFMappingJson(efMapping),
					HasEFMembers: true
				)
			);
			return;
		}

		// Value objects declared in an assembly that does not reference Entity Framework Core emit
		// neither the marker interfaces nor an EF nested class. The consumer still discovers them from
		// their [Scalar]/[ValueObject] attributes and emits the conversions inline.
		var attributes = type.GetAttributes();
		if (
			ValueObjectSymbolInspector.HasAttribute(
				attributes,
				TypeLibrary.Purview.ValueObjects.Serialization.ScalarAttribute
			)
		)
		{
			var assemblyDefaults = ValueObjectDefaultsAttributeData.FromAttributeData(
				type.ContainingAssembly.GetAttributes()
			);
			var options = ValueObjectDefaultsHelper.Apply(
				ScalarAttributeData.FromAttributeData(attributes),
				assemblyDefaults,
				attributes
			);

			if (!options.GenerateEFConverter && !options.GenerateEFComparer)
				return;

			var scalarProperty = type.GetMembers(options.PropertyName)
				.OfType<IPropertySymbol>()
				.FirstOrDefault(property => !property.IsStatic && property.GetMethod is not null);
			if (scalarProperty is null)
				return;

			var (efProviderType, efHydrateCastTypeName) = ValueObjectSymbolInspector.ResolveEFProviderType(
				scalarProperty.Type
			);

			scalarBuilder.Add(
				new EFScalarDescriptor(
					ValueObjectSymbolInspector.ToTypeName(type),
					options.GenerateEFConverter,
					options.GenerateEFComparer,
					ValueObjectSymbolInspector.IsEFMappableProviderType(scalarProperty.Type),
					ValueObjectSymbolInspector.ToTypeName(scalarProperty.Type),
					scalarProperty.Name,
					ValueObjectSymbolInspector.ToTypeName(efProviderType),
					efHydrateCastTypeName,
					HasEFMembers: false
				)
			);
			return;
		}

		if (
			ValueObjectSymbolInspector.HasAttribute(
				attributes,
				TypeLibrary.Purview.ValueObjects.Serialization.ValueObjectAttribute
			)
		)
		{
			var assemblyDefaults = ValueObjectDefaultsAttributeData.FromAttributeData(
				type.ContainingAssembly.GetAttributes()
			);
			var options = ValueObjectDefaultsHelper.Apply(
				ValueObjectAttributeData.FromAttributeData(attributes),
				assemblyDefaults,
				attributes
			);

			var efMapping = options.EFMapping;
			if (
				efMapping is not null
				&& ValueObjectSymbolInspector.IsEFMappingNone(efMapping)
				&& !options.GenerateEFComparer
			)
				return;

			complexBuilder.Add(
				new EFComplexDescriptor(
					ValueObjectSymbolInspector.ToTypeName(type),
					efMapping,
					isEF8Referenced,
					options.GenerateEFComparer,
					efMapping is not null && ValueObjectSymbolInspector.IsEFMappingJson(efMapping),
					HasEFMembers: false
				)
			);
			return;
		}
	}

	static INamedTypeSymbol? FindMarkerInterface(INamedTypeSymbol type, string markerName)
	{
		foreach (var iface in type.AllInterfaces)
		{
			if (iface.Name == markerName && iface.ContainingNamespace.ToDisplayString() == MarkerNamespace)
				return iface;
		}

		return null;
	}

	static bool HasEFMember(INamedTypeSymbol type, string memberName)
	{
		foreach (var efType in type.GetTypeMembers("EF"))
		{
			if (efType.GetMembers(memberName).Any(static member => member is IFieldSymbol { IsStatic: true }))
				return true;
		}

		return false;
	}

	static string ResolveEFMapping(INamedTypeSymbol type)
	{
		var attributes = type.GetAttributes();
		var assemblyDefaults = ValueObjectDefaultsAttributeData.FromAttributeData(
			type.ContainingAssembly.GetAttributes()
		);
		var options = ValueObjectDefaultsHelper.Apply(
			ValueObjectAttributeData.FromAttributeData(attributes),
			assemblyDefaults,
			attributes
		);
		return options.EFMapping;
	}
}

/// <summary>
/// The value objects an assembly-level registry must map that are declared outside the current
/// compilation. Holds only equatable data so the incremental pipeline can cache the scan.
/// </summary>
readonly record struct ReferencedEFValueObjectDiscoveryResult(
	EquatableArray<EFScalarDescriptor> Scalars,
	EquatableArray<EFComplexDescriptor> Complex
)
{
	public static readonly ReferencedEFValueObjectDiscoveryResult Empty = new(default, default);

	public bool IsEmpty => Scalars.Count == 0 && Complex.Count == 0;
}
