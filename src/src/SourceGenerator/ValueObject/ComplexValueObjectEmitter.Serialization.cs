namespace Purview.ValueObjects.SourceGenerator.ValueObject;

static partial class ComplexValueObjectEmitter
{
	/// <summary>
	/// Emits the complex value object's JSON converter. It reads and writes the object member by member through the
	/// runtime's <c>ValueObjectJson</c> helpers, with no reflection: the previous converter deserialized a generated
	/// <c>{Name}JsonModel</c> class through <c>JsonSerializer.Deserialize&lt;T&gt;(ref reader, options)</c>, which is
	/// <c>[RequiresUnreferencedCode]</c>/<c>[RequiresDynamicCode]</c> (IL2026/IL3050 in every trimmed or Native AOT
	/// consumer) and needs metadata for a type no source-generated context could list.
	/// </summary>
	/// <remarks>
	/// Member names follow the options' <c>PropertyNamingPolicy</c> and <c>PropertyNameCaseInsensitive</c>, unknown
	/// members are skipped, a missing member keeps its default, and <c>DefaultIgnoreCondition</c> is honoured when
	/// writing - the behaviour the model-class converter had.
	/// </remarks>
	static void EmitJsonConverter(CodeWriter writer, ComplexValueObjectModel model)
	{
		if (!model.Options.GenerateJsonConverter)
			return;

		var helper = TypeLibrary.ValueObjectJsonGlobalName;
		var properties = model.Properties.ToArray();
		var hydrateArgs = string.Join(", ", properties.Select(static (_, index) => MemberLocal(index)));
		var valueObjectType = ValueObjectType(model);
		var unableToDeserialize = $"\"Unable to deserialize {model.TypeModel.Name}.\"";

		writer.Class(
			new($"{model.TypeModel.Name}JsonConverter")
			{
				IsPartial = false,
				IsSealed = true,
				BaseType = TypeLibrary.System.Text.Json.Serialization.JsonConverter.MakeGeneric(valueObjectType),
			},
			body =>
			{
				for (var index = 0; index < properties.Length; index++)
				{
					var property = properties[index];
					if (property.JsonConverterExpression is not { } converter)
						continue;

					body.Field(
						new FieldDeclarationOptions(
							MemberConverterField(index),
							TypeLibrary.System.Text.Json.Serialization.JsonConverter.MakeGeneric(property.Type)
						)
						{
							Accessibility = TypeDeclarationAccessibility.Private,
							IsStatic = true,
							IsReadOnly = true,
							Initializer = converter,
						}
					);
				}

				body.Method(
					new("Read", valueObjectType, TypeDeclarationAccessibility.Public)
					{
						IsOverride = true,
						Parameters =
						[
							new("reader", TypeLibrary.System.Text.Json.Utf8JsonReader, ParameterModifier.Ref),
							new("typeToConvert", PurviewTypeLibrary.System.Type),
							new("options", TypeLibrary.System.Text.Json.JsonSerializerOptions),
						],
					},
					methodBody =>
					{
						methodBody.IfBlock(
							"reader.TokenType != global::System.Text.Json.JsonTokenType.StartObject",
							ifBody =>
								ifBody.Line($"throw new global::System.Text.Json.JsonException({unableToDeserialize});")
						);

						for (var index = 0; index < properties.Length; index++)
							methodBody.Line($"{properties[index].TypeName} {MemberLocal(index)} = default!;");

						methodBody.While(
							"reader.Read()",
							loop =>
							{
								loop.IfBlock(
									"reader.TokenType == global::System.Text.Json.JsonTokenType.EndObject",
									ifBody => ifBody.Return($"{model.HydrateFactoryName}({hydrateArgs})")
								);

								loop.Line("var jsonName = reader.GetString();");
								loop.Line("reader.Read();");

								for (var index = 0; index < properties.Length; index++)
								{
									var property = properties[index];
									var condition = $"{helper}.IsMember(jsonName, \"{property.Name}\", options)";
									var statement =
										$"{MemberLocal(index)} = {helper}.Read<{property.TypeName}>(ref reader, options{ConverterArgument(property, index)})!;";

									if (index == 0)
										loop.IfBlock(condition, branch => branch.Line(statement));
									else
										loop.ElseIf(condition, branch => branch.Line(statement));
								}

								if (properties.Length == 0)
									loop.Line("reader.Skip();");
								else
									loop.Else(static branch => branch.Line("reader.Skip();"));
							}
						);

						methodBody.Line($"throw new global::System.Text.Json.JsonException({unableToDeserialize});");
					}
				);

				body.Method(
					new("Write", TypeDeclarationAccessibility.Public)
					{
						IsOverride = true,
						Parameters =
						[
							new("writer", TypeLibrary.System.Text.Json.Utf8JsonWriter),
							new("value", valueObjectType),
							new("options", TypeLibrary.System.Text.Json.JsonSerializerOptions),
						],
					},
					methodBody =>
					{
						methodBody.Line("writer.WriteStartObject();");
						for (var index = 0; index < properties.Length; index++)
						{
							var property = properties[index];
							methodBody.Line(
								$"{helper}.WriteMember<{property.TypeName}>(writer, \"{property.Name}\", value.{property.Name}, options{ConverterArgument(property, index)});"
							);
						}

						methodBody.Line("writer.WriteEndObject();");
					}
				);
			}
		);
	}

	static string ConverterArgument(ComplexPropertyModel property, int index) =>
		property.JsonConverterExpression is null ? string.Empty : $", {MemberConverterField(index)}";

	static string MemberLocal(int index) => $"member{index}";

	static string MemberConverterField(int index) => $"Member{index}Converter";
}
