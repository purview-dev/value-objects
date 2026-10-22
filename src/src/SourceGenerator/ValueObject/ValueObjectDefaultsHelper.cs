namespace Purview.ValueObjects.SourceGenerator.ValueObject;

/// <summary>
/// Applies assembly-level <c>[ValueObjectDefaults]</c> to per-type <c>[Scalar]</c>/<c>[ValueObject]</c>
/// options. Precedence is: an option explicitly set on the type wins, then the assembly default, then
/// the built-in default carried by the type data model. The assembly default itself is the MSBuild
/// <c>ValueObjectsDefault*</c> property overlaid by an explicit named argument on the attribute, so
/// checked-in source wins over project configuration.
/// </summary>
static class ValueObjectDefaultsHelper
{
	/// <summary>
	/// Resolves the effective assembly defaults for the compilation being generated: the built-in
	/// defaults, overlaid by the build-generated <c>[ValueObjectDefaults]</c> the packaged targets emit
	/// from the <c>ValueObjectsDefault*</c> MSBuild properties, overlaid by the named arguments
	/// explicitly set on an author-declared <c>[ValueObjectDefaults]</c> attribute.
	/// </summary>
	public static ValueObjectDefaultsAttributeData ResolveAssemblyDefaults(
		ImmutableArray<AttributeData> assemblyAttributes
	)
	{
		AttributeData? buildAttribute = null;
		AttributeData? authorAttribute = null;
		foreach (var attribute in assemblyAttributes)
		{
			if (attribute.AttributeClass?.ToDisplayString() != TypeLibrary.ValueObjectDefaultsAttributeFullTypeName)
				continue;

			if (IsBuildGenerated(attribute))
				buildAttribute ??= attribute;
			else
				authorAttribute ??= attribute;
		}

		if (buildAttribute is null && authorAttribute is null)
			return ValueObjectDefaultsAttributeData.Empty;

		// With only one layer present, its data already carries the attribute's defaults for every option
		// it does not name. The empty instance from the generated model holds nulls for enum strings, so it
		// must not be used as a stand-in layer.
		if (buildAttribute is null)
			return Canonicalize(ValueObjectDefaultsAttributeData.FromAttributeData(authorAttribute!));
		if (authorAttribute is null)
			return Canonicalize(ValueObjectDefaultsAttributeData.FromAttributeData(buildAttribute));

		var build = ValueObjectDefaultsAttributeData.FromAttributeData(buildAttribute);
		var author = ValueObjectDefaultsAttributeData.FromAttributeData(authorAttribute);

		return Canonicalize(
			new(
				exists: true,
				GenerateJsonConverter: PickBool(
					authorAttribute,
					"GenerateJsonConverter",
					author.GenerateJsonConverter,
					build.GenerateJsonConverter
				),
				GenerateComparable: PickBool(
					authorAttribute,
					"GenerateComparable",
					author.GenerateComparable,
					build.GenerateComparable
				),
				GenerateComparisonOperators: PickBool(
					authorAttribute,
					"GenerateComparisonOperators",
					author.GenerateComparisonOperators,
					build.GenerateComparisonOperators
				),
				GenerateEnumProperties: PickBool(
					authorAttribute,
					"GenerateEnumProperties",
					author.GenerateEnumProperties,
					build.GenerateEnumProperties
				),
				GenerateImplicitFromPrimitive: PickBool(
					authorAttribute,
					"GenerateImplicitFromPrimitive",
					author.GenerateImplicitFromPrimitive,
					build.GenerateImplicitFromPrimitive
				),
				GenerateImplicitToPrimitive: PickBool(
					authorAttribute,
					"GenerateImplicitToPrimitive",
					author.GenerateImplicitToPrimitive,
					build.GenerateImplicitToPrimitive
				),
				GenerateEmpty: PickBool(authorAttribute, "GenerateEmpty", author.GenerateEmpty, build.GenerateEmpty),
				GenerateConstructor: PickBool(
					authorAttribute,
					"GenerateConstructor",
					author.GenerateConstructor,
					build.GenerateConstructor
				),
				EFMapping: PickString(authorAttribute, "EFMapping", author.EFMapping, build.EFMapping),
				GenerateEFConverter: PickBool(
					authorAttribute,
					"GenerateEFConverter",
					author.GenerateEFConverter,
					build.GenerateEFConverter
				),
				GenerateEFComparer: PickBool(
					authorAttribute,
					"GenerateEFComparer",
					author.GenerateEFComparer,
					build.GenerateEFComparer
				),
				GenerateEFValueGenerator: PickBool(
					authorAttribute,
					"GenerateEFValueGenerator",
					author.GenerateEFValueGenerator,
					build.GenerateEFValueGenerator
				),
				Trim: PickBool(authorAttribute, "Trim", author.Trim, build.Trim),
				Casing: PickString(authorAttribute, "Casing", author.Casing, build.Casing),
				DeserializationMode: PickString(
					authorAttribute,
					"DeserializationMode",
					author.DeserializationMode,
					build.DeserializationMode
				),
				ZodSchemaMode: PickString(authorAttribute, "ZodSchemaMode", author.ZodSchemaMode, build.ZodSchemaMode)
			)
		);

		bool PickBool(AttributeData? attribute, string propertyName, bool authorValue, bool buildValue) =>
			IsExplicitlySet(attribute, propertyName) ? authorValue : buildValue;

		string PickString(AttributeData? attribute, string propertyName, string authorValue, string buildValue) =>
			IsExplicitlySet(attribute, propertyName) ? authorValue : buildValue;

		static bool IsExplicitlySet(AttributeData? attribute, string propertyName) =>
			attribute?.NamedArguments.Any(argument => argument.Key == propertyName) ?? false;
	}

	/// <summary>
	/// Normalises an enum option to the <c>global::</c>-qualified form the generator uses for its mode
	/// constants, so every layer stores enum options in the same shape.
	/// </summary>
	static ValueObjectDefaultsAttributeData Canonicalize(ValueObjectDefaultsAttributeData defaults) =>
		defaults with
		{
			EFMapping = CanonicalizeEnum(defaults.EFMapping),
			Casing = CanonicalizeEnum(defaults.Casing),
			DeserializationMode = CanonicalizeEnum(defaults.DeserializationMode),
			ZodSchemaMode = CanonicalizeEnum(defaults.ZodSchemaMode),
		};

	static string CanonicalizeEnum(string value) =>
		value.StartsWith("global::", StringComparison.Ordinal) ? value : "global::" + value;

	/// <summary>
	/// Compares two enum-option strings from different attribute data models. An explicitly set value is
	/// rendered with a <c>global::</c> prefix, while the property default is not, so the prefix is ignored.
	/// </summary>
	public static bool AreEquivalentEnumValues(string? left, string? right)
	{
		if (left is null || right is null)
			return left is null && right is null;

		// The generator uses the global::-qualified form for its mode constants, so the comparison must ignore
		return string.Equals(TrimGlobalAlias(left), TrimGlobalAlias(right), StringComparison.Ordinal);
	}

	static string TrimGlobalAlias(string value) =>
		value.StartsWith("global::", StringComparison.Ordinal) ? value.Substring("global::".Length) : value;

	/// <summary>
	/// True when the attribute was emitted by the packaged targets from MSBuild properties. The targets
	/// write it to <see cref="TypeLibrary.BuildDefaultsFileName"/>; a file name match is stable across
	/// builds and does not depend on compile-item ordering. The generated marker is also honoured so an
	/// in-memory compilation (an analyzer test, for example) can model the generated file without
	/// reproducing its path.
	/// </summary>
	internal static bool IsBuildGenerated(AttributeData attribute)
	{
		if (attribute.ApplicationSyntaxReference?.SyntaxTree is not { } tree)
			return false;

		if (tree.FilePath.EndsWith(TypeLibrary.BuildDefaultsFileName, StringComparison.OrdinalIgnoreCase))
			return true;

		//
		return tree.GetText().ToString().IndexOf("// <auto-generated/>", StringComparison.Ordinal) >= 0;
	}

	public static ScalarAttributeData Apply(
		ScalarAttributeData typeOptions,
		ValueObjectDefaultsAttributeData assemblyDefaults,
		ImmutableArray<AttributeData> attributes
	)
	{
		if (!assemblyDefaults.Exists)
			return typeOptions;

		// Merge assembly defaults into the type options, but only for properties that are not explicitly set on the type.
		return typeOptions with
		{
			GenerateJsonConverter = MergeBool(
				typeOptions.GenerateJsonConverter,
				assemblyDefaults.GenerateJsonConverter,
				"GenerateJsonConverter"
			),
			GenerateComparable = MergeBool(
				typeOptions.GenerateComparable,
				assemblyDefaults.GenerateComparable,
				"GenerateComparable"
			),
			GenerateComparisonOperators = MergeBool(
				typeOptions.GenerateComparisonOperators,
				assemblyDefaults.GenerateComparisonOperators,
				"GenerateComparisonOperators"
			),
			GenerateEnumProperties = MergeBool(
				typeOptions.GenerateEnumProperties,
				assemblyDefaults.GenerateEnumProperties,
				"GenerateEnumProperties"
			),
			GenerateImplicitFromPrimitive = MergeBool(
				typeOptions.GenerateImplicitFromPrimitive,
				assemblyDefaults.GenerateImplicitFromPrimitive,
				"GenerateImplicitFromPrimitive"
			),
			GenerateImplicitToPrimitive = MergeBool(
				typeOptions.GenerateImplicitToPrimitive,
				assemblyDefaults.GenerateImplicitToPrimitive,
				"GenerateImplicitToPrimitive"
			),
			GenerateEmpty = MergeBool(typeOptions.GenerateEmpty, assemblyDefaults.GenerateEmpty, "GenerateEmpty"),
			GenerateEFConverter = MergeBool(
				typeOptions.GenerateEFConverter,
				assemblyDefaults.GenerateEFConverter,
				"GenerateEFConverter"
			),
			GenerateEFComparer = MergeBool(
				typeOptions.GenerateEFComparer,
				assemblyDefaults.GenerateEFComparer,
				"GenerateEFComparer"
			),
			GenerateEFValueGenerator = MergeBool(
				typeOptions.GenerateEFValueGenerator,
				assemblyDefaults.GenerateEFValueGenerator,
				"GenerateEFValueGenerator"
			),
			Trim = MergeBool(typeOptions.Trim, assemblyDefaults.Trim, "Trim"),
			Casing = MergeString(typeOptions.Casing, assemblyDefaults.Casing, "Casing"),
			DeserializationMode = MergeString(
				typeOptions.DeserializationMode,
				assemblyDefaults.DeserializationMode,
				"DeserializationMode"
			),
			ZodSchemaMode = MergeString(typeOptions.ZodSchemaMode, assemblyDefaults.ZodSchemaMode, "ZodSchemaMode"),
		};

		bool MergeBool(bool typeValue, bool assemblyValue, string propertyName) =>
			IsScalarPropertyExplicitlySet(attributes, propertyName) ? typeValue : assemblyValue;

		string MergeString(string typeValue, string assemblyValue, string propertyName) =>
			IsScalarPropertyExplicitlySet(attributes, propertyName) ? typeValue : assemblyValue;
	}

	/// <summary>
	/// True when <paramref name="propertyName"/> is set on the applied scalar attribute, generic or not.
	/// </summary>
	public static bool IsScalarPropertyExplicitlySet(ImmutableArray<AttributeData> attributes, string propertyName) =>
		ScalarAttributeParser.Find(attributes)?.NamedArguments.Any(argument => argument.Key == propertyName) ?? false;

	public static ValueObjectAttributeData Apply(
		ValueObjectAttributeData typeOptions,
		ValueObjectDefaultsAttributeData assemblyDefaults,
		ImmutableArray<AttributeData> attributes
	)
	{
		if (!assemblyDefaults.Exists)
			return typeOptions;

		// Merge assembly defaults into the type options, but only for properties that are not explicitly set on the type.
		return typeOptions with
		{
			GenerateJsonConverter = MergeBool(
				typeOptions.GenerateJsonConverter,
				assemblyDefaults.GenerateJsonConverter,
				"GenerateJsonConverter"
			),
			GenerateComparable = MergeBool(
				typeOptions.GenerateComparable,
				assemblyDefaults.GenerateComparable,
				"GenerateComparable"
			),
			GenerateComparisonOperators = MergeBool(
				typeOptions.GenerateComparisonOperators,
				assemblyDefaults.GenerateComparisonOperators,
				"GenerateComparisonOperators"
			),
			GenerateEmpty = MergeBool(typeOptions.GenerateEmpty, assemblyDefaults.GenerateEmpty, "GenerateEmpty"),
			GenerateConstructor = MergeBool(
				typeOptions.GenerateConstructor,
				assemblyDefaults.GenerateConstructor,
				"GenerateConstructor"
			),
			EFMapping = MergeString(typeOptions.EFMapping, assemblyDefaults.EFMapping, "EFMapping"),
			GenerateEFComparer = MergeBool(
				typeOptions.GenerateEFComparer,
				assemblyDefaults.GenerateEFComparer,
				"GenerateEFComparer"
			),
			DeserializationMode = MergeString(
				typeOptions.DeserializationMode,
				assemblyDefaults.DeserializationMode,
				"DeserializationMode"
			),
			ZodSchemaMode = MergeString(typeOptions.ZodSchemaMode, assemblyDefaults.ZodSchemaMode, "ZodSchemaMode"),
		};

		bool MergeBool(bool typeValue, bool assemblyValue, string propertyName) =>
			IsPropertyExplicitlySet(
				attributes,
				TypeLibrary.Purview.ValueObjects.Serialization.ValueObjectAttribute,
				propertyName
			)
				? typeValue
				: assemblyValue;

		string MergeString(string typeValue, string assemblyValue, string propertyName) =>
			IsPropertyExplicitlySet(
				attributes,
				TypeLibrary.Purview.ValueObjects.Serialization.ValueObjectAttribute,
				propertyName
			)
				? typeValue
				: assemblyValue;
	}

	public static bool IsPropertyExplicitlySet(
		ImmutableArray<AttributeData> attributes,
		TypeIdentity attributeType,
		string propertyName
	)
	{
		var attribute = attributes.FirstOrDefault(a => attributeType.Equals(a.AttributeClass));
		return attribute?.NamedArguments.Any(kvp => kvp.Key == propertyName) ?? false;
	}

	public static bool IsPropertyExplicitlySet(
		ImmutableArray<AttributeData> attributes,
		string attributeName,
		string propertyName
	)
	{
		var attribute = attributes.FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == attributeName);
		return attribute?.NamedArguments.Any(kvp => kvp.Key == propertyName) ?? false;
	}
}
