namespace Purview.ValueObjects.SourceGenerator.ValueObject;

static partial class ScalarValueObjectEmitter
{
	static void EmitEF(CodeWriter writer, ScalarValueObjectModel model, bool emitEF)
	{
		if (!emitEF || !model.IsEFReferenced)
			return;

		var emitConverter = model.Options.GenerateEFConverter;
		var emitComparer = model.Options.GenerateEFComparer;
		if (!emitConverter && !emitComparer)
			return;

		var valueObjectType = ValueObjectType(model);
		var factoryName =
			model.Options.DeserializationMode == ValueObjectSymbolInspector.StrictModeName ? "Create" : "Hydrate";

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
					if (emitConverter)
					{
						body.XmlSummary(
								"Converts between the value object and its underlying value for Entity Framework Core.",
								$"Persists {model.TypeModel.Name} as a native {model.ScalarTypeName} column."
							)
							.Field(
								new FieldDeclarationOptions(
									"Converter",
									TypeLibrary.Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter.MakeGeneric(
										valueObjectType,
										model.ScalarTypeReference
									),
									TypeDeclarationAccessibility.Public
								)
								{
									IsStatic = true,
									IsReadOnly = true,
									Initializer =
										$"new(vo => vo.{model.ScalarPropertyName}, v => {model.TypeModel.FullyQualifiedName}.{factoryName}(v))",
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
