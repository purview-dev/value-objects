namespace Purview.ValueObjects.SourceGenerator.ValueObject.Models;

/// <summary>
/// Compile-time description of an Entity Framework Core-enabled scalar value object, used to emit the
/// assembly-level <c>ValueObjectEFExtensions</c> registry. When the declaring assembly emitted an
/// <c>EF</c> nested class (<see cref="HasEFMembers"/>), the registry references
/// <c>{TypeName}.EF.Converter</c>/<c>.EF.Comparer</c>; otherwise the converter and comparer are emitted
/// inline from <see cref="ProviderTypeName"/>, <see cref="ScalarPropertyName"/> and <see cref="FactoryName"/>.
/// </summary>
readonly record struct EFScalarDescriptor(
	string TypeName,
	bool HasConverter,
	bool HasComparer,
	bool ProviderMappable,
	string? ProviderTypeName,
	string? ScalarPropertyName,
	string? FactoryName,
	bool HasEFMembers
);

/// <summary>
/// Compile-time description of an Entity Framework Core-enabled complex value object, used to emit the
/// assembly-level <c>ValueObjectEFExtensions</c> registry. When the declaring assembly emitted an
/// <c>EF</c> nested class (<see cref="HasEFMembers"/>), the registry references <c>{TypeName}.EF.Converter</c>
/// for JSON columns; otherwise the JSON converter is emitted inline.
/// </summary>
readonly record struct EFComplexDescriptor(
	string TypeName,
	string? EFMapping,
	bool IsEF8Referenced,
	bool HasComparer,
	bool HasJsonConverter,
	bool HasEFMembers
);
