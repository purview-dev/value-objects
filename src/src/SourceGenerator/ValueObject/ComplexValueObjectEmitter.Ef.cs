namespace Purview.ValueObjects.SourceGenerator.ValueObject;

static partial class ComplexValueObjectEmitter
{
	static void EmitEf(CodeWriter writer, ComplexValueObjectModel model, bool emitEf)
	{
		if (!emitEf || !model.IsEfReferenced)
			return;

		var emitComparer = model.Options.GenerateEfComparer;
		var emitJsonConverter =
			model.Options.EfMapping != null && ValueObjectSymbolInspector.IsEfMappingJson(model.Options.EfMapping);
		if (!emitComparer && !emitJsonConverter)
			return;

		var valueObjectType = ValueObjectType(model);

		writer
			.XmlSummary(
				"Entity Framework Core mapping members for this value object.",
				"Generated only when the consuming project references Microsoft.EntityFrameworkCore."
			)
			.Class(
				new TypeDeclarationOptions("Ef")
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
									Initializer =
										$"new(vo => global::System.Text.Json.JsonSerializer.Serialize(vo), v => global::System.Text.Json.JsonSerializer.Deserialize<{model.TypeModel.FullyQualifiedName}>(v)!)",
								}
							);
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
