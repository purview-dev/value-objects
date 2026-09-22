namespace Purview.ValueObjects.SourceGenerator.ValueObject.Models;

/// <summary>
/// Compile-time description of an Entity Framework Core-enabled scalar value object, used to emit the
/// assembly-level <c>ValueObjectEFExtensions</c> registry.
/// </summary>
readonly record struct EFScalarDescriptor(string TypeName, bool HasConverter, bool HasComparer, bool ProviderMappable);

/// <summary>
/// Compile-time description of an Entity Framework Core-enabled complex value object, used to emit the
/// assembly-level <c>ValueObjectEFExtensions</c> registry.
/// </summary>
readonly record struct EFComplexDescriptor(
	string TypeName,
	string? EFMapping,
	bool IsEF8Referenced,
	bool HasComparer,
	bool HasJsonConverter
);
