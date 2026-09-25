namespace Purview.ValueObjects.SourceGenerator.ValueObject;

/// <summary>
/// Emits the provider-tolerant Entity Framework Core value converter class for one value object conversion.
/// </summary>
/// <remarks>
/// <para>
/// Entity Framework Core applies a converted property's value converter to the raw provider value when a
/// query compares that property to the underlying primitive (<c>m.Id == guid</c>, <c>c.Email == "..."</c>).
/// Its built-in <c>ValueConverter&lt;TModel, TProvider&gt;</c> coerces that value with
/// <c>Convert.ChangeType</c>, which throws <c>InvalidCastException</c> ("Object must implement
/// IConvertible") for provider types that do not implement <c>IConvertible</c> — <c>Guid</c>,
/// <c>DateTimeOffset</c>, <c>TimeSpan</c>, <c>DateOnly</c> and <c>TimeOnly</c> among them.
/// See https://github.com/dotnet/efcore/issues/32030.
/// </para>
/// <para>
/// The generated converter accepts the value object, an already provider-shaped value, and a raw enum value
/// whose underlying type is the provider type; every other shape is deferred to the base implementation, so
/// Entity Framework Core's own coercions keep working unchanged. A converter is emitted per value object
/// rather than once per assembly because each one carries its own conversion expressions, a superseding
/// <c>ComposeWith</c>, and the members Entity Framework Core's design-time generator probes so compiled
/// models rebuild this converter type instead of a plain <c>ValueConverter</c>.
/// </para>
/// </remarks>
static class ValueObjectEFConverterEmitter
{
	/// <summary>The nested converter class name used by the per-value-object <c>EF</c> class.</summary>
	public const string DefaultClassName = "ValueObjectConverter";

	/// <summary>
	/// Builds the model-to-provider lambda. <paramref name="providerCastTypeName"/> is the provider type when
	/// a cast is required (an enum-backed scalar converts through its underlying integral type), or
	/// <see langword="null"/> to read the scalar property directly.
	/// </summary>
	public static string ToProviderExpression(string scalarPropertyName, string? providerCastTypeName) =>
		providerCastTypeName is null
			? $"vo => vo.{scalarPropertyName}"
			: $"vo => ({providerCastTypeName})vo.{scalarPropertyName}";

	/// <summary>
	/// Builds the provider-to-model lambda. <paramref name="hydrateCastTypeName"/> is the value object's
	/// underlying type when a cast is required (an enum-backed scalar hydrates from a cast of its integral
	/// provider value), or <see langword="null"/> to pass the provider value straight to <c>Hydrate</c>.
	/// </summary>
	public static string FromProviderExpression(string valueObjectTypeName, string? hydrateCastTypeName) =>
		hydrateCastTypeName is null
			? $"v => {valueObjectTypeName}.Hydrate(v)"
			: $"v => {valueObjectTypeName}.Hydrate(({hydrateCastTypeName})v)";

	public static void EmitConverterClass(CodeWriter writer, EFConverterDefinition definition)
	{
		var nullableObject = PurviewTypeLibrary.System.Object.MakeNullable(writer);
		var objectConversion = PurviewTypeLibrary
			.System.Func.WithArity(2)
			.MakeGeneric(nullableObject, nullableObject)
			.AsTypeReference();
		var valueConverterIdentity = TypeLibrary.Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter;
		var valueConverterType = valueConverterIdentity.AsTypeReference();
		var jsonValueReaderWriter =
			TypeLibrary.Microsoft.EntityFrameworkCore.Storage.Json.JsonValueReaderWriter.AsTypeReference();

		TypeReference baseType = new(new TypeIdentity(definition.BaseTypeName, null));
		writer
			.XmlSummary(
				"Converts this value object to and from its provider value for Entity Framework Core.",
				"Accepts either the value object or an already provider-shaped value, so queries that compare the",
				"converted property to the underlying primitive (m.Id == guid, c.Email == \"...\") translate.",
				"Entity Framework Core applies the property's converter to the raw provider value in that case",
				"(https://github.com/dotnet/efcore/issues/32030), and this converter tolerates both shapes."
			)
			.Class(
				new TypeDeclarationOptions(definition.ClassName)
				{
					Accessibility = definition.Accessibility,
					IsSealed = true,
					IsPartial = false,
					BaseType = baseType,
				},
				body =>
				{
					EmitConstructors(body, definition, jsonValueReaderWriter);
					EmitCompiledModelMembers(body, definition, jsonValueReaderWriter);
					EmitConversions(
						body,
						definition,
						objectConversion,
						valueConverterType,
						valueConverterIdentity.MakeNullable(writer),
						nullableObject
					);
				}
			);
	}

	static void EmitConstructors(CodeWriter body, EFConverterDefinition definition, TypeReference jsonValueReaderWriter)
	{
		var baseCall = $"base({definition.ToProviderExpression}, {definition.FromProviderExpression})";

		body.XmlSummary("Creates the converter using this value object's conversion expressions.")
			.Constructor(
				new ConstructorDeclarationOptions(definition.ClassName, TypeDeclarationAccessibility.Public)
				{
					Initializer = baseCall,
				},
				_ => { }
			);

		if (definition.JsonReaderWriterTypeName is null)
			return;

		body.XmlSummary(
				"Creates the converter for an Entity Framework Core compiled model.",
				"A compiled model is rebuilt from this converter's own type, so it needs a constructor that does",
				"not depend on the original expressions; the base call is identical to the default constructor's."
			)
			.Constructor(
				new ConstructorDeclarationOptions(definition.ClassName, TypeDeclarationAccessibility.Public)
				{
					Parameters = [new("jsonValueReaderWriter", jsonValueReaderWriter)],
					Initializer = baseCall,
				},
				ctor => ctor.Assignment("_", "jsonValueReaderWriter")
			);
	}

	static void EmitCompiledModelMembers(
		CodeWriter body,
		EFConverterDefinition definition,
		TypeReference jsonValueReaderWriter
	)
	{
		if (definition.JsonReaderWriterTypeName is null)
			return;

		body.XmlSummary(
				"Gets a JSON value reader/writer matching this converter's provider type.",
				"Entity Framework Core's design-time model generator reads this member by name, through",
				"reflection, as the signal that a compiled model should rebuild this converter type rather than a",
				"plain ValueConverter, which would lose the provider tolerance. Nothing else consumes it."
			)
			.Property(
				new PropertyDeclarationOptions(
					"JsonReaderWriter",
					jsonValueReaderWriter,
					TypeDeclarationAccessibility.Public
				)
				{
					ExpressionBody = $"{definition.JsonReaderWriterTypeName}.Instance",
				}
			);
	}

	static void EmitConversions(
		CodeWriter body,
		EFConverterDefinition definition,
		TypeReference objectConversion,
		TypeReference valueConverter,
		TypeReference nullableValueConverter,
		TypeReference nullableObject
	)
	{
		var valueObjectTypeName = definition.ValueObjectTypeName;
		var providerTypeName = definition.ProviderTypeName;
		var providerTypeExpression =
			$"global::System.Nullable.GetUnderlyingType(typeof({providerTypeName})) ?? typeof({providerTypeName})";

		body.XmlSummary(
				"Gets the conversion used when writing to the store, accepting a provider-shaped value.",
				"Entity Framework Core hands the raw provider value here when a query compares the converted",
				"property to the underlying primitive."
			)
			.Property(
				new PropertyDeclarationOptions(
					"ConvertToProvider",
					objectConversion,
					TypeDeclarationAccessibility.Public
				)
				{
					IsOverride = true,
					ExpressionBody = "ConvertToProviderValue",
				}
			);

		body.XmlSummary(
				"Gets the conversion used when reading from the store,",
				"accepting a value object as well as a provider-shaped value."
			)
			.Property(
				new PropertyDeclarationOptions(
					"ConvertFromProvider",
					objectConversion,
					TypeDeclarationAccessibility.Public
				)
				{
					IsOverride = true,
					ExpressionBody = "ConvertFromProviderValue",
				}
			);

		body.XmlSummary(
				"Supersedes a converter that converts between exactly the same two types.",
				"Entity Framework Core composes a property's converter with its mapping's own converter, and a",
				"compiled model rebuilds this conversion as a plain ValueConverter; superseding that equivalent",
				"converter keeps the provider tolerance instead of losing it inside a composite converter.",
				"<param name=\"secondConverter\">The converter to compose with.</param>",
				"<returns>This converter when the other converter is equivalent, otherwise the composed converter.</returns>"
			)
			.MethodExpression(
				new MethodDeclarationOptions("ComposeWith", valueConverter, TypeDeclarationAccessibility.Public)
				{
					IsOverride = true,
					Parameters = [new("secondConverter", nullableValueConverter)],
					ExpressionBody =
						$"(secondConverter is null || (secondConverter.ModelClrType == typeof({valueObjectTypeName}) && secondConverter.ProviderClrType == typeof({providerTypeName}))) ? this : base.ComposeWith(secondConverter)",
				}
			);

		body.XmlSummary(
				"Converts to the provider value, passing through a value that is already provider-shaped.",
				"<param name=\"value\">The value object or the provider value.</param>",
				"<returns>The provider value.</returns>"
			)
			.Method(
				new MethodDeclarationOptions(
					"ConvertToProviderValue",
					nullableObject,
					TypeDeclarationAccessibility.Private
				)
				{
					Parameters = [new("value", nullableObject)],
				},
				method =>
				{
					method.IfBlock(
						$"value is {valueObjectTypeName} model",
						branch => branch.Return("ConvertToProviderTyped(model)")
					);
					method.IfBlock("value is null", branch => branch.Return("null"));
					method.Assignment("var", "providerType", providerTypeExpression);
					method.IfBlock("value.GetType() == providerType", branch => branch.Return("value"));
					method.IfBlock(
						"value.GetType().IsEnum && global::System.Enum.GetUnderlyingType(value.GetType()) == providerType",
						branch => branch.Return("global::System.Convert.ChangeType(value, providerType)")
					);
					method.Return("base.ConvertToProvider(value)");
				}
			);

		body.XmlSummary(
				"Converts from the provider value, passing through a value that is already the value object.",
				"<param name=\"value\">The provider value or the value object.</param>",
				"<returns>The value object.</returns>"
			)
			.Method(
				new MethodDeclarationOptions(
					"ConvertFromProviderValue",
					nullableObject,
					TypeDeclarationAccessibility.Private
				)
				{
					Parameters = [new("value", nullableObject)],
				},
				method =>
				{
					method.IfBlock("value is null", branch => branch.Return("null"));
					method.Assignment("var", "providerType", providerTypeExpression);
					method.IfBlock(
						"value.GetType() == providerType",
						branch => branch.Return($"ConvertFromProviderTyped(({providerTypeName})value)")
					);
					method.IfBlock($"value is {valueObjectTypeName} model", branch => branch.Return("model"));
					method.Return("base.ConvertFromProvider(value)");
				}
			);
	}
}

/// <summary>
/// Describes one generated provider-tolerant value converter class.
/// </summary>
/// <param name="ClassName">The converter class name.</param>
/// <param name="Accessibility">The accessibility of the generated converter class.</param>
/// <param name="BaseTypeName">The complete value converter base type, including its type arguments.</param>
/// <param name="ValueObjectTypeName">The fully qualified value object type name.</param>
/// <param name="ProviderTypeName">The fully qualified provider type name, including nullability.</param>
/// <param name="ToProviderExpression">The model-to-provider lambda.</param>
/// <param name="FromProviderExpression">The provider-to-model lambda.</param>
/// <param name="JsonReaderWriterTypeName">
/// The JSON value reader/writer type whose <c>Instance</c> is exposed for Entity Framework Core's
/// compiled-model generator, or <see langword="null"/> to omit the compiled-model members.
/// </param>
readonly record struct EFConverterDefinition(
	string ClassName,
	TypeDeclarationAccessibility Accessibility,
	string BaseTypeName,
	string ValueObjectTypeName,
	string ProviderTypeName,
	string ToProviderExpression,
	string FromProviderExpression,
	string? JsonReaderWriterTypeName
);
