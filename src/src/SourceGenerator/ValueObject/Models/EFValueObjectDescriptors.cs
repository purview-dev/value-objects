namespace Purview.ValueObjects.SourceGenerator.ValueObject.Models;

/// <summary>
/// Compile-time description of an Entity Framework Core-enabled scalar value object, used to emit the
/// assembly-level <c>ValueObjectEFExtensions</c> registry. When the declaring assembly emitted an
/// <c>EF</c> nested class (<see cref="HasEFMembers"/>), the registry references
/// <c>{TypeName}.EF.Converter</c>/<c>.EF.Comparer</c>; otherwise the converter and comparer are emitted
/// inline from <see cref="ProviderTypeName"/> and <see cref="ScalarPropertyName"/>. Both paths convert from
/// the provider value with <c>Hydrate</c>, which is replay-safe even when the value object's <c>Create</c>
/// factory is strict. <see cref="EFProviderTypeName"/> and <see cref="EFHydrateCastTypeName"/> are set for
/// inline conversions only; they carry the enum-backed scalar's integral provider type and the cast used
/// when hydrating from it.
/// </summary>
readonly record struct EFScalarDescriptor(
	string TypeName,
	bool HasConverter,
	bool HasComparer,
	bool ProviderMappable,
	string? ProviderTypeName,
	string? ScalarPropertyName,
	string? EFProviderTypeName,
	string? EFHydrateCastTypeName,
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
