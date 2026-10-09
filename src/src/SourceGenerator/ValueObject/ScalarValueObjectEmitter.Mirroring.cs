namespace Purview.ValueObjects.SourceGenerator.ValueObject;

static partial class ScalarValueObjectEmitter
{
	/// <summary>
	/// Mirrors the underlying value's <c>ISpanFormattable</c> / <c>IUtf8SpanFormattable</c> by forwarding
	/// <c>TryFormat</c> to the wrapped value, so the value object can be formatted into a span by the same
	/// generic APIs as the type it wraps.
	/// </summary>
	static void EmitSpanFormatting(CodeWriter writer, ScalarValueObjectModel model)
	{
		if (model.MirrorISpanFormattable)
			EmitTryFormat(writer, model, utf8: false);

		if (model.MirrorIUtf8SpanFormattable)
			EmitTryFormat(writer, model, utf8: true);
	}

	/// <summary>
	/// Mirrors the underlying value's <c>IParsable&lt;T&gt;</c> / <c>ISpanParsable&lt;T&gt;</c> /
	/// <c>IUtf8SpanParsable&lt;T&gt;</c> by forwarding to the wrapped value and wrapping the result through
	/// <c>Create</c> (validation) or <c>TryCreate</c> (non-throwing), so the value object can be parsed by the
	/// same generic APIs as the type it wraps.
	/// </summary>
	static void EmitParsing(CodeWriter writer, ScalarValueObjectModel model)
	{
		if (model.MirrorIParsable)
		{
			EmitParse(writer, model, PurviewTypeLibrary.System.String.AsTypeReference(), "s", "ParseStringCore");
			EmitTryParse(
				writer,
				model,
				PurviewTypeLibrary.System.String.MakeNullable(writer),
				"s",
				"TryParseStringCore"
			);
		}

		if (model.MirrorISpanParsable)
		{
			EmitParse(writer, model, ReadOnlySpanOfChar, "s", "ParseSpanCore");
			EmitTryParse(writer, model, ReadOnlySpanOfChar, "s", "TryParseSpanCore");
		}

		if (model.MirrorIUtf8SpanParsable)
		{
			EmitParse(writer, model, ReadOnlySpanOfByte, "utf8Text", "ParseUtf8Core");
			EmitTryParse(writer, model, ReadOnlySpanOfByte, "utf8Text", "TryParseUtf8Core");
		}

		EmitParsingHelpers(writer, model);
	}

	static void EmitTryFormat(CodeWriter writer, ScalarValueObjectModel model, bool utf8)
	{
		var destinationName = utf8 ? "utf8Destination" : "destination";
		var writtenName = utf8 ? "bytesWritten" : "charsWritten";
		var destinationType = utf8 ? SpanOfByte : SpanOfChar;
		var interfaceName = utf8 ? "global::System.IUtf8SpanFormattable" : "global::System.ISpanFormattable";

		writer.Method(
			new("TryFormat", PurviewTypeLibrary.System.Boolean, TypeDeclarationAccessibility.Public)
			{
				Parameters =
				[
					new(destinationName, destinationType),
					new(writtenName, PurviewTypeLibrary.System.Int32, ParameterModifier.Out),
					new("format", ReadOnlySpanOfChar),
					new("provider", FormatProviderType(writer)),
				],
			},
			body =>
			{
				// A null underlying value writes nothing and reports success, matching the parameterless
				// ToString override returning the empty string.
				if (model.ScalarValueIsNullable)
				{
					body.IfBlock(
						$"{model.ScalarPropertyName} is null",
						ifBody =>
						{
							ifBody.Assignment(writtenName, "0");
							ifBody.Return("true");
						}
					);
				}

				body.Return(
					$"(({interfaceName}){model.ScalarPropertyName}).TryFormat({destinationName}, out {writtenName}, format, provider)"
				);
			}
		);
	}

	static void EmitParse(
		CodeWriter writer,
		ScalarValueObjectModel model,
		TypeReference inputType,
		string inputName,
		string coreMethodName
	)
	{
		writer.MethodExpression(
			new("Parse", ValueObjectType(model), TypeDeclarationAccessibility.Public)
			{
				IsStatic = true,
				Parameters = [new(inputName, inputType), new("provider", FormatProviderType(writer))],
				ExpressionBody =
					$"Create({coreMethodName}<{model.EffectiveScalarTypeReference.RenderFullName}>({inputName}, provider))",
			}
		);
	}

	static void EmitTryParse(
		CodeWriter writer,
		ScalarValueObjectModel model,
		TypeReference inputType,
		string inputName,
		string coreMethodName
	)
	{
		var valueObjectType = ValueObjectType(model);

		writer.Method(
			new("TryParse", PurviewTypeLibrary.System.Boolean, TypeDeclarationAccessibility.Public)
			{
				IsStatic = true,
				Parameters =
				[
					new(inputName, inputType),
					new("provider", FormatProviderType(writer)),
					new("result", valueObjectType, ParameterModifier.Out)
					{
						Attributes = [MaybeNullWhenFalseAttribute],
					},
				],
			},
			body =>
			{
				body.IfBlock(
					$"{coreMethodName}<{model.EffectiveScalarTypeReference.RenderFullName}>({inputName}, provider, out var value)",
					ifBody => ifBody.Return("TryCreate(value, out result)")
				);
				body.Assignment("result", "default!");
				body.Return("false");
			}
		);
	}

	/// <summary>
	/// Emits the private generic helpers the parsing members call. A static abstract interface member can only
	/// be invoked on a type parameter, so these helpers are what make the forwarding work for both public and
	/// explicit implementations of the parsing interfaces.
	/// </summary>
	static void EmitParsingHelpers(CodeWriter writer, ScalarValueObjectModel model)
	{
		if (model.MirrorIParsable)
		{
			var constraint = TypeLibrary.System.IParsable.MakeGeneric(TypeParameterReference).RenderFullName;

			EmitParseHelper(
				writer,
				"ParseStringCore",
				constraint,
				PurviewTypeLibrary.System.String.AsTypeReference(),
				"s",
				"T.Parse(s, provider)",
				outParameter: false
			);
			EmitParseHelper(
				writer,
				"TryParseStringCore",
				constraint,
				PurviewTypeLibrary.System.String.MakeNullable(writer),
				"s",
				"T.TryParse(s, provider, out result)",
				outParameter: true
			);
		}

		if (model.MirrorISpanParsable)
		{
			var constraint = TypeLibrary.System.ISpanParsable.MakeGeneric(TypeParameterReference).RenderFullName;

			EmitParseHelper(
				writer,
				"ParseSpanCore",
				constraint,
				ReadOnlySpanOfChar,
				"s",
				"T.Parse(s, provider)",
				outParameter: false
			);
			EmitParseHelper(
				writer,
				"TryParseSpanCore",
				constraint,
				ReadOnlySpanOfChar,
				"s",
				"T.TryParse(s, provider, out result)",
				outParameter: true
			);
		}

		if (model.MirrorIUtf8SpanParsable)
		{
			var constraint = TypeLibrary.System.IUtf8SpanParsable.MakeGeneric(TypeParameterReference).RenderFullName;

			EmitParseHelper(
				writer,
				"ParseUtf8Core",
				constraint,
				ReadOnlySpanOfByte,
				"utf8Text",
				"T.Parse(utf8Text, provider)",
				outParameter: false
			);
			EmitParseHelper(
				writer,
				"TryParseUtf8Core",
				constraint,
				ReadOnlySpanOfByte,
				"utf8Text",
				"T.TryParse(utf8Text, provider, out result)",
				outParameter: true
			);
		}
	}

	static void EmitParseHelper(
		CodeWriter writer,
		string methodName,
		string constraint,
		TypeReference inputType,
		string inputName,
		string expressionBody,
		bool outParameter
	)
	{
		var parameters = outParameter
			? ImmutableArray.Create(
				new ParameterDeclarationOptions(inputName, inputType),
				new("provider", FormatProviderType(writer)),
				new("result", TypeParameterReference, ParameterModifier.Out)
				{
					Attributes = [MaybeNullWhenFalseAttribute],
				}
			)
			: ImmutableArray.Create(
				new ParameterDeclarationOptions(inputName, inputType),
				new("provider", FormatProviderType(writer))
			);

		writer.MethodExpression(
			new(
				methodName,
				outParameter ? PurviewTypeLibrary.System.Boolean : TypeParameterReference,
				TypeDeclarationAccessibility.Private
			)
			{
				IsStatic = true,
				GenericTypes = [new GenericTypeParameterOptions("T") { Constraints = [constraint] }],
				Parameters = parameters,
				ExpressionBody = expressionBody,
			}
		);
	}

	static TypeReference FormatProviderType(CodeWriter writer) =>
		TypeLibrary.System.IFormatProvider.MakeNullable(writer);

	static TypeReference CharType => PurviewTypeLibrary.System.Char.AsTypeReference();

	static TypeReference ByteType => PurviewTypeLibrary.System.Byte.AsTypeReference();

	static TypeReference SpanOfChar => TypeLibrary.System.Span.MakeGeneric(CharType);

	static TypeReference SpanOfByte => TypeLibrary.System.Span.MakeGeneric(ByteType);

	static TypeReference ReadOnlySpanOfChar => TypeLibrary.System.ReadOnlySpan.MakeGeneric(CharType);

	static TypeReference ReadOnlySpanOfByte => TypeLibrary.System.ReadOnlySpan.MakeGeneric(ByteType);

	/// <summary>
	/// A <c>TypeReference</c> for a bare type parameter named <c>T</c>, used by the parsing helpers.
	/// </summary>
	static TypeReference TypeParameterReference => new(new TypeIdentity("T", null));

	/// <summary>
	/// <c>[MaybeNullWhen(false)]</c>, matching the parsing interfaces' out-parameter annotation so the
	/// generated members are nullability-clean.
	/// </summary>
	static AttributeDeclarationOptions MaybeNullWhenFalseAttribute =>
		new(new TypeIdentity("MaybeNullWhenAttribute", "System.Diagnostics.CodeAnalysis")) { Arguments = [new(false)] };
}
