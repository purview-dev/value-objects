namespace Purview.ValueObjects.SourceGenerator.ValueObject;

static class ValueObjectEmitterHelpers
{
	/// <summary>The complete <c>ValueConverter</c> base type of a generated converter class.</summary>
	public static string EFConverterBaseType(string valueObjectTypeName, string providerTypeName) =>
		$"global::{TypeLibrary.Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverterFullName}<{valueObjectTypeName}, {providerTypeName}>";

	/// <summary>
	/// The JSON value reader/writer Entity Framework Core's design-time model generator requires a generated
	/// converter to expose so compiled models rebuild the converter itself rather than a plain
	/// <c>ValueConverter</c>. Chosen from the converter's provider type; unrelated to the conversion itself.
	/// </summary>
	public static string EFJsonReaderWriterType(string providerTypeName)
	{
		var name = providerTypeName.EndsWith("?", StringComparison.Ordinal)
			? providerTypeName.Substring(0, providerTypeName.Length - 1)
			: providerTypeName;
		var lastDot = name.LastIndexOf('.');
		var typeName = lastDot < 0 ? name : name.Substring(lastDot + 1);
		var singleton = typeName switch
		{
			"string" or "String" => "JsonStringReaderWriter",
			"bool" or "Boolean" => "JsonBoolReaderWriter",
			"char" or "Char" => "JsonCharReaderWriter",
			"byte" or "Byte" => "JsonByteReaderWriter",
			"sbyte" or "SByte" => "JsonSByteReaderWriter",
			"short" or "Int16" => "JsonInt16ReaderWriter",
			"int" or "Int32" => "JsonInt32ReaderWriter",
			"long" or "Int64" => "JsonInt64ReaderWriter",
			"ushort" or "UInt16" => "JsonUInt16ReaderWriter",
			"uint" or "UInt32" => "JsonUInt32ReaderWriter",
			"ulong" or "UInt64" => "JsonUInt64ReaderWriter",
			"float" or "Single" => "JsonFloatReaderWriter",
			"double" or "Double" => "JsonDoubleReaderWriter",
			"decimal" or "Decimal" => "JsonDecimalReaderWriter",
			"byte[]" or "Byte[]" => "JsonByteArrayReaderWriter",
			"Guid" => "JsonGuidReaderWriter",
			"DateTime" => "JsonDateTimeReaderWriter",
			"DateTimeOffset" => "JsonDateTimeOffsetReaderWriter",
			"DateOnly" => "JsonDateOnlyReaderWriter",
			"TimeOnly" => "JsonTimeOnlyReaderWriter",
			"TimeSpan" => "JsonTimeSpanReaderWriter",
			_ => "JsonStringReaderWriter",
		};

		return $"global::Microsoft.EntityFrameworkCore.Storage.Json.{singleton}";
	}

	public static void EmitBinaryOperator(
		CodeWriter writer,
		TypeReference leftType,
		TypeReference rightType,
		string operatorToken,
		string expression
	)
	{
		writer.Operator(
			new OperatorDeclarationOptions(
				operatorToken,
				PurviewTypeLibrary.System.Boolean,
				new("left", leftType),
				new("right", rightType)
			)
			{
				Accessibility = TypeDeclarationAccessibility.Public,
			},
			body => body.Return(expression)
		);
	}

	public static void EmitRelationalOperators(
		CodeWriter writer,
		EquatableArray<string> existingOperators,
		TypeReference leftType,
		TypeReference rightType,
		string compareExpression
	)
	{
		EmitRelationalOperator(
			writer,
			existingOperators,
			OperatorNames.EqualityAndRelational.LessThanOperatorName,
			"<",
			leftType,
			rightType,
			compareExpression,
			"< 0"
		);
		EmitRelationalOperator(
			writer,
			existingOperators,
			OperatorNames.EqualityAndRelational.GreaterThanOperatorName,
			">",
			leftType,
			rightType,
			compareExpression,
			"> 0"
		);
		EmitRelationalOperator(
			writer,
			existingOperators,
			OperatorNames.EqualityAndRelational.LessThanOrEqualOperatorName,
			"<=",
			leftType,
			rightType,
			compareExpression,
			"<= 0"
		);
		EmitRelationalOperator(
			writer,
			existingOperators,
			OperatorNames.EqualityAndRelational.GreaterThanOrEqualOperatorName,
			">=",
			leftType,
			rightType,
			compareExpression,
			">= 0"
		);
	}

	static void EmitRelationalOperator(
		CodeWriter writer,
		EquatableArray<string> existingOperators,
		string operatorMethodName,
		string operatorToken,
		TypeReference leftType,
		TypeReference rightType,
		string compareExpression,
		string comparisonSuffix
	)
	{
		if (existingOperators.Contains(operatorMethodName))
			return;

		writer.Operator(
			new OperatorDeclarationOptions(
				operatorToken,
				PurviewTypeLibrary.System.Boolean,
				new("left", leftType),
				new("right", rightType)
			)
			{
				Accessibility = TypeDeclarationAccessibility.Public,
			},
			body => body.Return($"left.{compareExpression} {comparisonSuffix}")
		);
	}
}
