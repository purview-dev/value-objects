namespace Purview.ValueObjects.SourceGenerator.ValueObject;

static partial class ComplexValueObjectEmitter
{
	static void EmitEquality(CodeWriter writer, ComplexValueObjectModel model)
	{
		if (!model.EqualsSelfExists)
		{
			writer.Method(
				new("Equals", PurviewTypeLibrary.System.Boolean, TypeDeclarationAccessibility.Public)
				{
					// A non-sealed record class requires Equals(TSelf) to allow overriding (CS8872); a sealed
					// record class, a record struct and a non-record type declare it non-virtual.
					IsVirtual = model.IsRecord && model.IsReferenceType && !model.IsSealed,
					// IEquatable<TSelf>.Equals takes a nullable parameter for a reference type, so a class
					// value object must accept null to satisfy the interface.
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
						body.IfBlock("other is null", ifBody => ifBody.Return("false"));

					// A derived record's base members are part of its identity. The base record's own
					// equality decides whether they match; the contract check keeps a derived record from
					// comparing equal to a differently derived one, as the compiler-synthesized record
					// equality does.
					if (model.RecordBaseTypeName is { } recordBaseType)
						body.IfBlock($"!base.Equals(({recordBaseType})other)", ifBody => ifBody.Return("false"));

					if (
						model.IsRecord
						&& model.IsReferenceType
						&& (model.RecordBaseTypeName is not null || !model.IsSealed)
					)
						body.IfBlock("EqualityContract != other.EqualityContract", ifBody => ifBody.Return("false"));

					foreach (var property in model.Properties)
					{
						body.IfBlock($"!{EqualityExpression(property)}", ifBody => ifBody.Return("false"));
					}

					body.Return("true");
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
					ExpressionBody = $"obj is {model.TypeName} other && Equals(other)",
				}
			);
		}

		if (!model.GetHashCodeExists)
		{
			writer.Method(
				new("GetHashCode", PurviewTypeLibrary.System.Int32, TypeDeclarationAccessibility.Public)
				{
					IsOverride = true,
				},
				body =>
				{
					body.Assignment("var", "hash", "new global::System.HashCode()");
					// Equal derived records compare their base members through base.Equals above, so the
					// hash must include the base hash to keep the equal-implies-equal hash contract.
					if (model.RecordBaseTypeName is not null)
						body.MethodCallOn("hash", "Add", "base.GetHashCode()");

					foreach (var property in model.Properties)
						body.MethodCallOn("hash", "Add", HashExpression(property));

					body.Return("hash.ToHashCode()");
				}
			);
		}
	}

	static void EmitOperators(CodeWriter writer, ComplexValueObjectModel model)
	{
		var valueObjectType = ValueObjectType(model);

		if (!model.EqualityOperatorExists)
		{
			ValueObjectEmitterHelpers.EmitBinaryOperator(
				writer,
				valueObjectType,
				valueObjectType,
				"==",
				model.IsReferenceType ? "left is null ? right is null : left.Equals(right)" : "left.Equals(right)"
			);
		}

		if (!model.InequalityOperatorExists)
		{
			ValueObjectEmitterHelpers.EmitBinaryOperator(
				writer,
				valueObjectType,
				valueObjectType,
				"!=",
				"!(left == right)"
			);
		}
	}

	static void EmitComparison(CodeWriter writer, ComplexValueObjectModel model)
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

					foreach (var property in model.AllProperties)
					{
						body.Assignment("var", $"compare{property.Name}", ComparisonExpression(property));
						body.IfBlock($"compare{property.Name} != 0", ifBody =>
							ifBody.Return($"compare{property.Name}")
						);
					}

					body.Return("0");
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
					body.IfBlock($"obj is {model.TypeName} otherValueObject", ifBody =>
						ifBody.Return("CompareTo(otherValueObject)")
					);
					body.Throw(
						$"new global::System.ArgumentException($\"Object must be of type {{nameof({model.TypeModel.Name})}}.\", nameof(obj))"
					);
				}
			);
		}

		if (model.Options.GenerateComparable && model.Options.GenerateComparisonOperators)
		{
			ValueObjectEmitterHelpers.EmitRelationalOperators(
				writer,
				model.ExistingRelationalOperators,
				ValueObjectType(model),
				ValueObjectType(model),
				"CompareTo(right)"
			);
		}
	}

	static string EqualityExpression(ComplexPropertyModel property) =>
		property.CollectionKind switch
		{
			ComplexMemberCollectionKind.Sequence =>
				$"{TypeLibrary.ValueObjectEqualityGlobalName}.SequenceEquals<{property.CollectionElementTypeName}>({property.Name}, other.{property.Name}{ComparerArgument(property)})",
			ComplexMemberCollectionKind.Set =>
				$"{TypeLibrary.ValueObjectEqualityGlobalName}.SetEquals<{property.CollectionElementTypeName}>({property.Name}, other.{property.Name}{ComparerArgument(property)})",
			ComplexMemberCollectionKind.Dictionary =>
				$"{TypeLibrary.ValueObjectEqualityGlobalName}.DictionaryEquals({property.Name}, other.{property.Name}{ValueComparerArgument(property)})",
			_ =>
				$"global::System.Collections.Generic.EqualityComparer<{property.TypeName}>.Default.Equals({property.Name}, other.{property.Name})",
		};

	static string HashExpression(ComplexPropertyModel property) =>
		property.CollectionKind switch
		{
			ComplexMemberCollectionKind.Sequence =>
				$"{TypeLibrary.ValueObjectEqualityGlobalName}.SequenceHash<{property.CollectionElementTypeName}>({property.Name}{ComparerArgument(property)})",
			ComplexMemberCollectionKind.Set =>
				$"{TypeLibrary.ValueObjectEqualityGlobalName}.SetHash<{property.CollectionElementTypeName}>({property.Name}{ComparerArgument(property)})",
			ComplexMemberCollectionKind.Dictionary =>
				$"{TypeLibrary.ValueObjectEqualityGlobalName}.DictionaryHash({property.Name}{ValueComparerArgument(property)})",
			_ => property.Name,
		};

	static string ComparisonExpression(ComplexPropertyModel property) =>
		property.CollectionKind switch
		{
			ComplexMemberCollectionKind.Sequence =>
				$"{TypeLibrary.ValueObjectEqualityGlobalName}.SequenceCompare<{property.CollectionElementTypeName}>({property.Name}, other.{property.Name}{ComparerArgument(property)})",
			ComplexMemberCollectionKind.Set =>
				$"{TypeLibrary.ValueObjectEqualityGlobalName}.SetCompare<{property.CollectionElementTypeName}>({property.Name}, other.{property.Name}{ComparerArgument(property)})",
			ComplexMemberCollectionKind.Dictionary =>
				$"{TypeLibrary.ValueObjectEqualityGlobalName}.DictionaryCompare({property.Name}, other.{property.Name}{ValueComparerArgument(property)})",
			_ =>
				$"global::System.Collections.Generic.Comparer<{property.TypeName}>.Default.Compare({property.Name}, other.{property.Name})",
		};

	static string ComparerArgument(ComplexPropertyModel property) =>
		property.CollectionElementComparerExpression is { } comparer ? $", {comparer}" : string.Empty;

	static string ValueComparerArgument(ComplexPropertyModel property) =>
		property.CollectionValueComparerExpression is { } comparer ? $", {comparer}" : string.Empty;
}
