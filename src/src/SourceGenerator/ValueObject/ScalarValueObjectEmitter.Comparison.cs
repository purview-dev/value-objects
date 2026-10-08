namespace Purview.ValueObjects.SourceGenerator.ValueObject;

static partial class ScalarValueObjectEmitter
{
	/// <summary>
	/// The comparer the generated <c>CompareTo</c> uses for the scalar value.
	/// </summary>
	/// <remarks>
	/// <para>
	/// For a <see cref="string"/> scalar this is <c>StringComparer.Ordinal</c>, not
	/// <c>Comparer&lt;string&gt;.Default</c>. The default string comparer orders by the current culture,
	/// while the generated equality and hash code use <c>EqualityComparer&lt;string&gt;.Default</c>, which is
	/// ordinal. Mixing the two breaks the <see cref="IComparable{T}"/> contract: <c>CompareTo(other) == 0</c>
	/// no longer implies <c>Equals(other)</c>, so a <c>SortedSet</c>, <c>SortedDictionary</c> or
	/// <c>List.BinarySearch</c> — all of which use <c>CompareTo</c> for identity — would treat values the
	/// type considers distinct as the same, and vice versa.
	/// </para>
	/// <para>
	/// It also made ordering machine-dependent: the same <c>OrderBy</c> over the same data produced
	/// different results under a different thread culture, and different results again from the database's
	/// collation. Ordinal ordering is deterministic and agrees with equality, which is what a value object
	/// used as a domain identity needs.
	/// </para>
	/// </remarks>
	static string ScalarComparerExpression(ScalarValueObjectModel model) =>
		IsStringScalar(model)
			? "global::System.StringComparer.Ordinal"
			: $"global::System.Collections.Generic.Comparer<{model.ScalarTypeName}>.Default";

	/// <summary>
	/// Whether the scalar value is a <see cref="string"/>, allowing for a nullable annotation.
	/// </summary>
	static bool IsStringScalar(ScalarValueObjectModel model) =>
		model.ScalarTypeName.TrimEnd('?') is "global::System.String" or "string";

	static void EmitComparison(CodeWriter writer, ScalarValueObjectModel model)
	{
		if (!model.CompareToSelfExists)
		{
			writer.Method(
				new("CompareTo", PurviewTypeLibrary.System.Int32, TypeDeclarationAccessibility.Public)
				{
					Parameters =
					[
						new(
							"other",
							model.IsReferenceType ? ValueObjectType(model).Nullable(writer) : ValueObjectType(model)
						),
					],
				},
				body =>
				{
					if (model.IsReferenceType)
						body.IfBlock("other is null", ifBody => ifBody.Return("1"));

					body.Return($"CompareTo(other.{model.ScalarPropertyName})");
				}
			);
		}

		if (!model.CompareToPrimitiveExists)
		{
			writer.MethodExpression(
				new("CompareTo", PurviewTypeLibrary.System.Int32, TypeDeclarationAccessibility.Public)
				{
					Parameters = [new("other", ScalarComparableParameter(model, writer))],
					ExpressionBody = $"{ScalarComparerExpression(model)}.Compare({model.ScalarPropertyName}, other)",
				}
			);
		}

		if (!model.CompareToObjectExists)
		{
			writer.Method(
				new("CompareTo", PurviewTypeLibrary.System.Int32, TypeDeclarationAccessibility.Public)
				{
					Parameters = [new("obj", PurviewTypeLibrary.System.Object.MakeNullable(writer))],
				},
				body =>
				{
					body.IfBlock("obj is null", ifBody => ifBody.Return("1"));
					body.IfBlock(
						$"obj is {model.TypeName} otherValueObject",
						ifBody => ifBody.Return("CompareTo(otherValueObject)")
					);
					body.IfBlock(
						$"obj is {ScalarPatternTypeName(model)} primitive",
						ifBody => ifBody.Return($"CompareTo({ScalarPatternArgument(model)})")
					);
					body.Throw(
						$"new global::System.ArgumentException($\"Object must be of type {{nameof({model.TypeModel.Name})}} or {model.ScalarTypeName}.\", nameof(obj))"
					);
				}
			);
		}

		if (model.Options.GenerateComparable && model.Options.GenerateComparisonOperators)
		{
			ValueObjectEmitterHelpers.EmitRelationalOperators(
				writer,
				model.ExistingSelfRelationalOperators,
				ValueObjectType(model),
				ValueObjectType(model),
				"CompareTo(right)"
			);

			if (!model.ScalarAndSelfAreSameType)
			{
				ValueObjectEmitterHelpers.EmitRelationalOperators(
					writer,
					model.ExistingScalarRelationalOperators,
					ValueObjectType(model),
					model.ScalarTypeReference,
					"CompareTo(right)"
				);
			}
		}
	}
}
