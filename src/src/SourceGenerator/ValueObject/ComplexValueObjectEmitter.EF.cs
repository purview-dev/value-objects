namespace Purview.ValueObjects.SourceGenerator.ValueObject;

static partial class ComplexValueObjectEmitter
{
	static void EmitEF(CodeWriter writer, ComplexValueObjectModel model, bool emitEF)
	{
		if (!emitEF || !model.IsEFReferenced)
			return;

		var emitComparer = model.Options.GenerateEFComparer;
		var emitJsonConverter =
			model.Options.EFMapping != null && ValueObjectSymbolInspector.IsEFMappingJson(model.Options.EFMapping);
		if (!emitComparer && !emitJsonConverter)
			return;

		var valueObjectType = ValueObjectType(model);
		EFConverterDefinition converterDefinition = new(
			ValueObjectEFConverterEmitter.DefaultClassName,
			TypeDeclarationAccessibility.Public,
			ValueObjectEmitterHelpers.EFConverterBaseType(model.TypeModel.FullyQualifiedName, "global::System.String"),
			model.TypeModel.FullyQualifiedName,
			"global::System.String",
			"vo => global::System.Text.Json.JsonSerializer.Serialize(vo)",
			$"v => global::System.Text.Json.JsonSerializer.Deserialize<{model.TypeModel.FullyQualifiedName}>(v)!",
			ValueObjectEmitterHelpers.EFJsonReaderWriterType("global::System.String")
		);

		writer
			.XmlSummary(
				"Entity Framework Core mapping members for this value object.",
				"Generated only when the consuming project references Microsoft.EntityFrameworkCore."
			)
			.Class(
				new TypeDeclarationOptions("EF")
				{
					Accessibility = TypeDeclarationAccessibility.Public,
					IsStatic = true,
					IsPartial = false,
				},
				body =>
				{
					if (emitJsonConverter)
					{
						body.XmlSummary(
								"Converts the value object to and from a JSON string for Entity Framework Core JSON columns.",
								"Serialization uses the generated JSON converter."
							)
							.Field(
								new FieldDeclarationOptions(
									"Converter",
									TypeLibrary.Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter.MakeGeneric(
										valueObjectType,
										TypeLibrary.System.String.AsTypeReference()
									),
									TypeDeclarationAccessibility.Public
								)
								{
									IsStatic = true,
									IsReadOnly = true,
									Initializer = $"new {ValueObjectEFConverterEmitter.DefaultClassName}()",
								}
							);

						ValueObjectEFConverterEmitter.EmitConverterClass(body, converterDefinition);
					}

					if (emitComparer)
					{
						body.XmlSummary(
								"Compares value object instances for Entity Framework Core change tracking.",
								"Equality is derived from the generated equality members; snapshots copy by value."
							)
							.Field(
								new FieldDeclarationOptions(
									"Comparer",
									TypeLibrary.Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer.MakeGeneric(
										valueObjectType
									),
									TypeDeclarationAccessibility.Public
								)
								{
									IsStatic = true,
									IsReadOnly = true,
									Initializer = "new((a, b) => a == b, vo => vo.GetHashCode(), vo => vo)",
								}
							);
					}
				}
			);
	}
}
