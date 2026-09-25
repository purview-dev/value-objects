namespace Purview.ValueObjects.SourceGenerator.Common;

/// <summary>
/// The generated library is kept in this namespace (rather than the global namespace) so it does not
/// clash with the global-namespace <c>TypeLibrary</c> emitted by the referenced ZodSharp generator.
/// </summary>
[GenerateTypeLibrary(ClassName = "TypeLibrary", Namespace = "Purview.ValueObjects.SourceGenerator.Common")]
static partial class TypeLibrarySpec
{
	[TypeRef("Purview.ValueObjects")]
	static readonly TypeIdentity IValueObject = default;

	[TypeRef("Purview.ValueObjects")]
	static readonly TypeIdentity IScalarValueObject = default;

	[TypeRef("Purview.ValueObjects")]
	static readonly TypeIdentity IEFScalarValueObject = default;

	[TypeRef("Purview.ValueObjects")]
	static readonly TypeIdentity IEFComplexValueObject = default;

	[TypeRef("Purview.ValueObjects.Serialization", generateFullNameConst: true)]
	static readonly TypeIdentity ScalarAttribute = default;

	[TypeRef("Purview.ValueObjects.Serialization", generateFullNameConst: true)]
	static readonly TypeIdentity ValueObjectAttribute = default;

	[TypeRef("Purview.ValueObjects.Serialization", generateFullNameConst: true)]
	static readonly TypeIdentity ValueObjectDefaultsAttribute = default;

	[TypeRef("Purview.ValueObjects.Serialization", generateFullNameConst: true)]
	[EnumValue("Hydrate", 0)]
	[EnumValue("Strict", 1)]
	static readonly TypeIdentity ValueObjectDeserializationMode = default;

	[TypeRef("Purview.ValueObjects.Serialization", generateFullNameConst: true)]
	static readonly TypeIdentity EntityFrameworkMapping = default;

	[TypeRef("Purview.ValueObjects.Serialization", generateFullNameConst: true)]
	static readonly TypeIdentity ZodSchemaMode = default;

	[TypeRef("Microsoft.EntityFrameworkCore")]
	static readonly TypeIdentity ModelBuilder = default;

	[TypeRef("Microsoft.EntityFrameworkCore")]
	static readonly TypeIdentity DbContext = default;

	[TypeRef("Microsoft.EntityFrameworkCore")]
	static readonly TypeIdentity DbContextOptionsBuilder = default;

	[TypeRef("Microsoft.EntityFrameworkCore.ChangeTracking")]
	static readonly TypeIdentity ValueComparer = default;

	[TypeRef("Microsoft.EntityFrameworkCore.Infrastructure")]
	static readonly TypeIdentity IModelCustomizer = default;

	[TypeRef("Microsoft.EntityFrameworkCore.Infrastructure")]
	static readonly TypeIdentity ModelCustomizer = default;

	[TypeRef("Microsoft.EntityFrameworkCore.Infrastructure")]
	static readonly TypeIdentity ModelCustomizerDependencies = default;

	[TypeRef("Microsoft.EntityFrameworkCore.Storage.ValueConversion", generateFullNameConst: true)]
	static readonly TypeIdentity ValueConverter = default;

	[TypeRef("Microsoft.EntityFrameworkCore.Storage.Json")]
	static readonly TypeIdentity JsonValueReaderWriter = default;

	[TypeRef("System.Linq.Expressions", arity: 1)]
	static readonly TypeIdentity Expression = default;

	[TypeRef("Microsoft.EntityFrameworkCore.Metadata", generateFullNameConst: true)]
	static readonly TypeIdentity IComplexType = default;
}
