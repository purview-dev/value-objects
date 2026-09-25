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
		var providerType = model.EFProviderTypeReference;
		EFConverterDefinition converterDefinition = new(
			ValueObjectEFConverterEmitter.DefaultClassName,
			TypeDeclarationAccessibility.Public,
			ValueObjectEmitterHelpers.EFConverterBaseType(model.TypeName, model.EFProviderTypeName),
			model.TypeName,
			model.EFProviderTypeName,
			ValueObjectEFConverterEmitter.ToProviderExpression(
				model.ScalarPropertyName,
				model.EFHydrateCastTypeName is null ? null : model.EFProviderTypeName
			),
			ValueObjectEFConverterEmitter.FromProviderExpression(model.TypeName, model.EFHydrateCastTypeName),
			ValueObjectEmitterHelpers.EFJsonReaderWriterType(model.EFProviderTypeName)
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
					if (emitConverter)
					{
						body.XmlSummary(
								"Converts between the value object and its underlying value for Entity Framework Core.",
								$"Persists {model.TypeModel.Name} as a native {model.EFProviderTypeName} column."
							)
							.Field(
								new FieldDeclarationOptions(
									"Converter",
									TypeLibrary.Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter.MakeGeneric(
										valueObjectType,
										providerType
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
