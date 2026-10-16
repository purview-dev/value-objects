namespace Purview.ValueObjects.SourceGenerator.ValueObject;

/// <summary>
/// Locates and parses the <c>[Scalar]</c> and <c>[Scalar&lt;T&gt;]</c> attributes. The two attribute
/// types share their options through a common base (<c>ScalarOptionsAttribute</c>) but are distinct
/// attribute classes, so the generated attribute-data model — which matches a single attribute type —
/// cannot be used for both. Detection here is by name and namespace.
/// </summary>
static class ScalarAttributeParser
{
	/// <summary>
	/// True when <paramref name="attributeClass"/> is the scalar attribute, generic or not.
	/// </summary>
	public static bool IsScalarAttribute(INamedTypeSymbol? attributeClass) =>
		attributeClass is { Arity: <= 1, Name: "ScalarAttribute" }
		&& attributeClass.ContainingNamespace.ToDisplayString() == TypeLibrary.SerializationNamespace;

	/// <summary>The scalar attribute applied to the type, or <see langword="null"/> when absent.</summary>
	public static AttributeData? Find(ImmutableArray<AttributeData> attributes) =>
		attributes.FirstOrDefault(static attribute => IsScalarAttribute(attribute.AttributeClass));

	/// <summary>True when the attribute is the generic <c>[Scalar&lt;T&gt;]</c> form.</summary>
	public static bool IsGeneric(AttributeData attribute) =>
		attribute.AttributeClass is { IsGenericType: true, TypeArguments.Length: 1 };

	/// <summary>
	/// The underlying value type that selects the automatic form, from either the generic argument
	/// (<c>[Scalar&lt;T&gt;]</c>) or the <c>typeof(...)</c> constructor argument
	/// (<c>[Scalar(typeof(T))]</c>). <see langword="null"/> selects the manual form.
	/// </summary>
	public static ITypeSymbol? GetValueType(AttributeData attribute)
	{
		if (IsGeneric(attribute))
			return attribute.AttributeClass!.TypeArguments[0];

		foreach (var argument in attribute.ConstructorArguments)
		{
			if (argument.Kind == TypedConstantKind.Type && argument.Value is ITypeSymbol type)
				return type;
		}

		return null;
	}

	/// <summary>True when the attribute declares the underlying type and the generator owns the property.</summary>
	public static bool IsAutomatic(AttributeData attribute) => GetValueType(attribute) is not null;

	/// <summary>Whether the automatic form declares the property as a nullable reference type.</summary>
	public static bool GetNullable(AttributeData attribute) => attribute.GetNamedArgument("Nullable", false);

	/// <summary>
	/// Reads the shared option set from either attribute form into the same model.
	/// </summary>
	public static ScalarAttributeData Parse(AttributeData attribute) =>
		new(
			true,
			attribute.GetConstructorArgument("propertyName", "Value")!,
			attribute.GetNamedArgument("GenerateJsonConverter", true),
			attribute.GetNamedArgument("GenerateComparable", true),
			attribute.GetNamedArgument("GenerateComparisonOperators", true),
			attribute.GetNamedArgument("GenerateEnumProperties", true),
			attribute.GetNamedArgument("GenerateImplicitFromPrimitive", true),
			attribute.GetNamedArgument("GenerateImplicitToPrimitive", true),
			attribute.GetNamedArgument("GenerateEmpty", true),
			attribute.GetNamedArgument("GenerateEFConverter", true),
			attribute.GetNamedArgument("GenerateEFComparer", true),
			attribute.GetNamedArgument("GenerateEFValueGenerator", false),
			attribute.GetNamedArgument("Trim", false),
			attribute.GetEnumNamedArgument("Casing", TypeLibrary.StringCasingFullTypeName + ".None")!,
			attribute.GetEnumNamedArgument(
				"DeserializationMode",
				TypeLibrary.ValueObjectDeserializationModeFullTypeName + ".Hydrate"
			)!,
			attribute.GetEnumNamedArgument(
				"ZodSchemaMode",
				TypeLibrary.ZodSchemaModeFullTypeName + ".InAdditionToHooks"
			)!
		);
}
