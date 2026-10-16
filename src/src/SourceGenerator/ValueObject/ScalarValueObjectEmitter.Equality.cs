namespace Purview.ValueObjects.SourceGenerator.ValueObject;

static partial class ScalarValueObjectEmitter
{
	static void EmitEquality(CodeWriter writer, ScalarValueObjectModel model)
	{
		if (!model.EqualsSelfExists)
		{
			writer.MethodExpression(
				new("Equals", PurviewTypeLibrary.System.Boolean, TypeDeclarationAccessibility.Public)
				{
					// IEquatable<TSelf>.Equals takes a nullable parameter for a reference type, so a class
					// scalar must accept null to satisfy the interface (records synthesize their own).
					Parameters =
					[
						new(
							"other",
							model.IsReferenceType ? ValueObjectType(model).Nullable(writer) : ValueObjectType(model)
						),
					],
					ExpressionBody = model.IsReferenceType
						? $"other is not null && global::System.Collections.Generic.EqualityComparer<{model.ScalarTypeName}>.Default.Equals({model.ScalarPropertyName}, other.{model.ScalarPropertyName})"
						: $"global::System.Collections.Generic.EqualityComparer<{model.ScalarTypeName}>.Default.Equals({model.ScalarPropertyName}, other.{model.ScalarPropertyName})",
				}
			);
		}

		if (!model.EqualsPrimitiveExists)
		{
			writer.MethodExpression(
				new("Equals", PurviewTypeLibrary.System.Boolean, TypeDeclarationAccessibility.Public)
				{
					// IEquatable<TValue>.Equals takes a nullable parameter for a reference type.
					Parameters = [new("other", ScalarComparableParameter(model, writer))],
					ExpressionBody =
						$"global::System.Collections.Generic.EqualityComparer<{model.ScalarTypeName}>.Default.Equals({model.ScalarPropertyName}, other)",
				}
			);
		}

		if (!model.EqualsObjectExists)
		{
			writer.MethodExpression(
				new("Equals", PurviewTypeLibrary.System.Boolean, TypeDeclarationAccessibility.Public)
				{
					IsOverride = true,
					Parameters = [new("obj", PurviewTypeLibrary.System.Object.MakeNullable(writer))],
					ExpressionBody =
						$"obj is {model.TypeName} other ? Equals(other) : obj is {ScalarPatternTypeName(model)} primitive && Equals({ScalarPatternArgument(model)})",
				}
			);
		}

		if (!model.GetHashCodeExists)
		{
			writer.MethodExpression(
				new("GetHashCode", PurviewTypeLibrary.System.Int32, TypeDeclarationAccessibility.Public)
				{
					IsOverride = true,
					// EqualityComparer<T>.GetHashCode is annotated [DisallowNull]; a nullable scalar
					// (string? or int?) still hashes null as 0 at runtime, so the annotation is suppressed
					// rather than branched.
					ExpressionBody =
						$"global::System.Collections.Generic.EqualityComparer<{model.ScalarTypeName}>.Default.GetHashCode({model.ScalarPropertyName}{(model.ScalarValueIsNullable ? "!" : string.Empty)})",
				}
			);
		}
	}

	static void EmitOperators(CodeWriter writer, ScalarValueObjectModel model)
	{
		var valueObjectType = ValueObjectType(model);

		if (!model.SameTypeEqualityOperatorExists)
		{
			ValueObjectEmitterHelpers.EmitBinaryOperator(
				writer,
				valueObjectType,
				valueObjectType,
				"==",
				model.IsReferenceType ? "left is null ? right is null : left.Equals(right)" : "left.Equals(right)"
			);
		}

		if (!model.SameTypeInequalityOperatorExists)
		{
			ValueObjectEmitterHelpers.EmitBinaryOperator(
				writer,
				valueObjectType,
				valueObjectType,
				"!=",
				"!(left == right)"
			);
		}

		if (!model.PrimitiveEqualityOperatorExists)
		{
			ValueObjectEmitterHelpers.EmitBinaryOperator(
				writer,
				valueObjectType,
				model.ScalarTypeReference,
				"==",
				model.IsReferenceType ? "left is null ? false : left.Equals(right)" : "left.Equals(right)"
			);
		}

		if (!model.PrimitiveInequalityOperatorExists)
		{
			ValueObjectEmitterHelpers.EmitBinaryOperator(
				writer,
				valueObjectType,
				model.ScalarTypeReference,
				"!=",
				"!(left == right)"
			);
		}

		if (!model.ReversePrimitiveEqualityOperatorExists)
		{
			ValueObjectEmitterHelpers.EmitBinaryOperator(
				writer,
				model.ScalarTypeReference,
				valueObjectType,
				"==",
				model.IsReferenceType ? "right is not null && right.Equals(left)" : "right.Equals(left)"
			);
		}

		if (!model.ReversePrimitiveInequalityOperatorExists)
		{
			ValueObjectEmitterHelpers.EmitBinaryOperator(
				writer,
				model.ScalarTypeReference,
				valueObjectType,
				"!=",
				"!(left == right)"
			);
		}
	}

	static void EmitConversions(CodeWriter writer, ScalarValueObjectModel model)
	{
		var valueObjectType = ValueObjectType(model);

		if (model.Options.GenerateImplicitToPrimitive && !model.HasValueObjectToPrimitiveConversion)
		{
			using (
				writer.OperatorScope(
					new OperatorDeclarationOptions(
						"op_Implicit",
						model.ScalarTypeReference,
						new("valueObject", valueObjectType)
					)
					{
						Accessibility = TypeDeclarationAccessibility.Public,
						Kind = OperatorDeclarationKind.ImplicitConversion,
						ExpressionBody = $"valueObject.{model.ScalarPropertyName}",
					}
				)
			)
			{
				//
			}
		}

		if (
			model.Options.GenerateImplicitFromPrimitive
			&& !model.HasContextualCreateOverload
			&& !model.HasPrimitiveToValueObjectConversion
		)
		{
			using (
				writer.OperatorScope(
					new OperatorDeclarationOptions(
						"op_Implicit",
						valueObjectType,
						new("value", model.ScalarTypeReference)
					)
					{
						Accessibility = TypeDeclarationAccessibility.Public,
						Kind = OperatorDeclarationKind.ImplicitConversion,
						ExpressionBody = "Create(value)",
					}
				)
			)
			{
				//
			}
		}
	}

	static void EmitToString(CodeWriter writer, ScalarValueObjectModel model)
	{
		if (!model.ToStringExists)
		{
			writer.MethodExpression(
				new("ToString", PurviewTypeLibrary.System.String, TypeDeclarationAccessibility.Public)
				{
					IsOverride = true,
					ExpressionBody = model.ScalarTypeIsNullableReference
						? $"{model.ScalarPropertyName}?.ToString() ?? string.Empty"
						: $"{model.ScalarPropertyName}.ToString() ?? string.Empty",
				}
			);
		}

		if (model.ScalarHasFormatToString && !model.FormatToStringExists)
			EmitFormatToString(writer, model);

		if (model.ScalarImplementsIFormattable && !model.FormattedToStringExists)
			EmitFormattedToString(writer, model);
	}

	/// <summary>
	/// Mirrors the underlying type's format-only <c>ToString(string? format)</c> overload (for example
	/// <c>Guid.ToString(string? format)</c>), so a value object offers the same overload the property does.
	/// The overload is emitted only when the underlying type declares it.
	/// </summary>
	static void EmitFormatToString(CodeWriter writer, ScalarValueObjectModel model)
	{
		var value = model.ScalarPropertyName;
		var expressionBody = model.ScalarValueIsNullable
			? model.ScalarIsReferenceType
				? $"{value}?.ToString(format) ?? string.Empty"
				: $"{value}.HasValue ? {value}.GetValueOrDefault().ToString(format) : string.Empty"
			: $"{value}.ToString(format)";

		writer.MethodExpression(
			new("ToString", PurviewTypeLibrary.System.String, TypeDeclarationAccessibility.Public)
			{
				Parameters = [new("format", PurviewTypeLibrary.System.String.MakeNullable(writer))],
				ExpressionBody = expressionBody,
			}
		);
	}

	/// <summary>
	/// Implements <see cref="IFormattable"/> by forwarding the format string and provider to the underlying
	/// scalar value, so a value object exposes the same formatting options as the property it wraps (for
	/// example <c>amount.ToString("N2", CultureInfo.InvariantCulture)</c> or <c>string.Format("{0:N2}", amount)</c>).
	/// </summary>
	/// <remarks>
	/// Emitted only when the scalar type implements <see cref="IFormattable"/> and the author has not already
	/// declared the overload. A null underlying value formats as the empty string, matching the parameterless
	/// <c>ToString</c> override.
	/// </remarks>
	static void EmitFormattedToString(CodeWriter writer, ScalarValueObjectModel model)
	{
		var value = model.ScalarPropertyName;
		var formattableValue = $"((global::System.IFormattable){value})";
		var formatCall = $"{formattableValue}.ToString(format, formatProvider)";

		var expressionBody = model.ScalarValueIsNullable
			? model.ScalarIsReferenceType
				? $"{value} is null ? string.Empty : {formatCall}"
				: $"{value}.HasValue ? {formatCall} : string.Empty"
			: formatCall;

		writer.MethodExpression(
			new("ToString", PurviewTypeLibrary.System.String, TypeDeclarationAccessibility.Public)
			{
				Parameters =
				[
					new("format", PurviewTypeLibrary.System.String.MakeNullable(writer)),
					new("formatProvider", TypeLibrary.System.IFormatProvider.MakeNullable(writer)),
				],
				ExpressionBody = expressionBody,
			}
		);
	}

	static void EmitJsonConverter(CodeWriter writer, ScalarValueObjectModel model)
	{
		if (!model.Options.GenerateJsonConverter)
			return;

		var valueObjectType = ValueObjectType(model);
		var factoryMethod =
			model.Options.DeserializationMode == ValueObjectSymbolInspector.StrictModeName ? "Create" : "Hydrate";

		writer.Class(
			new($"{model.TypeModel.Name}JsonConverter")
			{
				IsPartial = false,
				IsSealed = true,
				BaseType = TypeLibrary.System.Text.Json.Serialization.JsonConverter.MakeGeneric(valueObjectType),
			},
			body =>
			{
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
						methodBody.Assignment(
							"var",
							"value",
							writeValue =>
								writeValue.MethodCall(
									"Deserialize",
									[new MethodCallArgumentOptions("reader", ParameterModifier.Ref), "options"],
									receiver: $"{TypeLibrary.System.Text.Json.JsonSerializer}",
									genericArguments: [model.ScalarTypeReference]
								)
						);

						// A nullable scalar (`string?`, `int?`) round-trips JSON null; only a non-nullable
						// reference scalar rejects it.
						if (model.ScalarCanBeNull && !model.ScalarValueIsNullable)
						{
							methodBody.IfBlock(
								"value is null",
								ifBody =>
									ifBody.Throw(
										TypeLibrary.System.Text.Json.JsonException,
										$"{model.TypeModel.Name} cannot be null."
									)
							);
						}

						methodBody.Return($"{factoryMethod}(value)");
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
						methodBody.MethodCallOn(
							$"{TypeLibrary.System.Text.Json.JsonSerializer}",
							"Serialize",
							"writer",
							$"value.{model.ScalarPropertyName}",
							"options"
						)
				);
			}
		);
	}
}
