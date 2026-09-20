namespace Purview.ValueObjects.SourceGenerator.Common;

/// <summary>
/// Declares the value-object-specific type identities for the generated <see cref="TypeLibrary"/>.
/// The generator mirrors the framework <c>PurviewTypeLibrary</c> shape, so common system types are
/// inherited and only the value-object types and framework-absent system types are declared here.
/// </summary>
[GenerateTypeLibrary(ClassName = "TypeLibrary", Namespace = "Purview.ValueObjects.SourceGenerator.Common")]
static partial class TypeLibrarySpec
{
	[TypeRef("Purview.ValueObjects")]
	static readonly TypeIdentity IValueObject = default;

	[TypeRef("Purview.ValueObjects")]
	static readonly TypeIdentity IScalarValueObject = default;

	[TypeRef("Purview.ValueObjects.Serialization")]
	static readonly TypeIdentity ScalarAttribute = default;

	[TypeRef("Purview.ValueObjects.Serialization")]
	static readonly TypeIdentity ValueObjectAttribute = default;

	[TypeRef("Purview.ValueObjects.Serialization")]
	static readonly TypeIdentity ValueObjectDefaultsAttribute = default;

	[TypeRef("Purview.ValueObjects.Serialization")]
	static readonly TypeIdentity ValueObjectDeserializationMode = default;

	[TypeRef("Purview.ValueObjects.Serialization")]
	static readonly TypeIdentity ZodSchemaMode = default;

	[TypeRef("System")]
	static readonly TypeIdentity Guid = default;

	[TypeRef("System")]
	static readonly TypeIdentity IEquatable = default;

	[TypeRef("System")]
	static readonly TypeIdentity IComparable = default;

	[TypeRef("System.Text.Json")]
	static readonly TypeIdentity JsonSerializer = default;

	[TypeRef("System.Text.Json")]
	static readonly TypeIdentity JsonException = default;

	[TypeRef("System.Text.Json")]
	static readonly TypeIdentity JsonSerializerOptions = default;

	[TypeRef("System.Text.Json")]
	static readonly TypeIdentity Utf8JsonReader = default;

	[TypeRef("System.Text.Json")]
	static readonly TypeIdentity Utf8JsonWriter = default;

	[TypeRef("System.Text.Json.Serialization")]
	static readonly TypeIdentity JsonConverter = default;

	[TypeRef("System.Text.Json.Serialization")]
	static readonly TypeIdentity JsonConverterAttribute = default;
}
