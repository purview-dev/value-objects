namespace Purview.ValueObjects.SourceGenerator.ValueObject.Models;

/// <summary>
/// Compile-time description of an Entity Framework Core-enabled scalar value object, used to emit the
/// assembly-level <c>ValueObjectEfExtensions</c> registry.
/// </summary>
readonly record struct EfScalarDescriptor(string TypeName, bool HasConverter, bool HasComparer, bool ProviderMappable);

/// <summary>
/// Compile-time description of an Entity Framework Core-enabled complex value object, used to emit the
/// assembly-level <c>ValueObjectEfExtensions</c> registry.
/// </summary>
readonly record struct EfComplexDescriptor(
	string TypeName,
	string? EfMapping,
	bool IsEf8Referenced,
	bool HasComparer,
	bool HasJsonConverter
);
