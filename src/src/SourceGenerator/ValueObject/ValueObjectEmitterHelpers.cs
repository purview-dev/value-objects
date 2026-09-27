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

	/// <summary>
	/// ZodSharp refinement emission shared by the scalar and complex value object emitters.
	/// </summary>
	/// <remarks>
	/// The ZodSharp schema generator runs before the value object generator, so a refinement emitted here is
	/// invisible to it (ZodSharp resolves the refinement from the compilation it is handed, which contains only
	/// user source). Refinement rules therefore flow through the generated <c>Create</c> path: the
	/// <see cref="TypeLibrary.ZodRefinementHookName"/> hook the user implements is invoked with a ZodSharp
	/// <c>RefineCtx&lt;T&gt;</c> and the issues it collects are reported as a <c>ZodException</c>, merged with
	/// the schema's own issues.
	/// </remarks>
	public static class ZodRefinement
	{
		/// <summary>
		/// Emits the generated <c>Create</c> validation step: the ZodSharp schema is always consulted and its
		/// issues surface as one <c>ZodException</c>.
		/// </summary>
		/// <remarks>
		/// Refinement rules — including the <c>OnZodValidate</c> hook the ZodSharp generator declares on the
		/// type — run inside the generated schema's <c>Validate</c>. This generator neither declares nor
		/// invokes that hook, so a value object observes refinements through exactly the same path as any
		/// other <c>[ZodSchema]</c> consumer.
		/// </remarks>
		public static void EmitCreateValidation(CodeWriter body, string schemaReference)
		{
			body.Assignment("var", "result", $"{schemaReference}.Validate(instance)");
			body.IfBlock(
				"!result.IsSuccess",
				ifBody => ifBody.Throw($"new global::{TypeLibrary.ZodExceptionTypeName}(result.Errors)")
			);
		}
	}

	/// <summary>
	/// Emits the Entity Framework Core key value generator pair for one Guid-backed scalar value object:
	/// a <c>ValueGenerator&lt;TSelf&gt;</c> that hydrates a time-ordered identifier, and the
	/// <c>ValueGeneratorFactory</c> Entity Framework Core instantiates once per property.
	/// </summary>
	/// <remarks>
	/// The generator must be typed as the value object, not as its provider value: Entity Framework Core
	/// assigns what a generator returns straight to the property, so a <c>Guid</c>-producing generator
	/// throws <see cref="InvalidCastException"/> on a converted value object property.
	/// </remarks>
	/// <summary>Describes the default UUIDv7 ordering strategy in generated documentation.</summary>
	public const string EFUuidV7StrategyDescription = "a time-ordered (UUIDv7) identifier";

	/// <summary>Describes the SQL Server ordering strategy in generated documentation.</summary>
	public const string EFSqlServerStrategyDescription =
		"an identifier whose bytes ascend in SQL Server's uniqueidentifier ordering";

	/// <summary>
	/// Emits the per-value-object <c>EF</c> key value generator and its factory for one ordering strategy.
	/// </summary>
	/// <param name="writer">The writer receiving the types.</param>
	/// <param name="valueObjectTypeName">The fully qualified value object type name.</param>
	/// <param name="generatorClassName">The nested generator class name.</param>
	/// <param name="factoryClassName">The nested factory class name.</param>
	/// <param name="sequentialGuidExpression">
	/// The call appended to <see cref="TypeLibrary.EFSequentialGuidFullTypeName"/>, for example
	/// <c>.NewGuid()</c> or <c>.NewSqlServerGuid()</c>.
	/// </param>
	/// <param name="strategyDescription">
	/// A sentence fragment describing the generated value, used in the generator's XML documentation.
	/// </param>
	/// <param name="isEF8Referenced">
	/// True when the consuming project references Entity Framework Core 8 or later. Version 8 changed the
	/// second parameter of <c>ValueGeneratorFactory.Create</c> from <c>IEntityType</c> to <c>ITypeBase</c>,
	/// so the emitted override must match the reference set.
	/// </param>
	/// <param name="accessibility">The accessibility of the emitted types.</param>
	public static void EmitEFValueGeneratorStrategy(
		CodeWriter writer,
		string valueObjectTypeName,
		string generatorClassName,
		string factoryClassName,
		string sequentialGuidExpression,
		string strategyDescription,
		bool isEF8Referenced,
		TypeDeclarationAccessibility accessibility
	)
	{
		TypeReference generatorType = new(
			new TypeIdentity($"{TypeLibrary.EFValueGeneratorFullTypeName}<{valueObjectTypeName}>", null)
		);
		TypeReference factoryType = new(new TypeIdentity(TypeLibrary.EFValueGeneratorFactoryFullTypeName, null));
		TypeReference propertyType = new(new TypeIdentity(TypeLibrary.EFIPropertyFullTypeName, null));
		TypeReference typeBaseType = new(
			new TypeIdentity(
				isEF8Referenced ? TypeLibrary.EFITypeBaseFullTypeName : TypeLibrary.EFIEntityTypeFullTypeName,
				null
			)
		);
		TypeReference entityEntryType = new(new TypeIdentity(TypeLibrary.EFEntityEntryFullTypeName, null));
		TypeReference valueObjectType = new(new TypeIdentity(valueObjectTypeName, null));

		writer
			.XmlSummary(
				"Creates the Entity Framework Core key value generator for this value object.",
				$"Use it on a property, or register {TypeLibrary.EFKeyValueGeneratorConventionClassName} through",
				$"{TypeLibrary.EFValueObjectExtensionsClassName}.UseValueObjectKeyGenerators() to apply it to every",
				"key property of this type."
			)
			.Class(
				new TypeDeclarationOptions(factoryClassName)
				{
					Accessibility = accessibility,
					IsSealed = true,
					IsPartial = false,
					BaseType = factoryType,
				},
				factoryBody =>
					factoryBody
						.XmlSummary("Creates the generator Entity Framework Core owns for the property's lifetime.")
						.Method(
							// The class may be internal, but the members it overrides are public.
							new MethodDeclarationOptions("Create", generatorType, TypeDeclarationAccessibility.Public)
							{
								IsOverride = true,
								Parameters =
								[
									new("property", propertyType),
									new(isEF8Referenced ? "typeBase" : "entityType", typeBaseType),
								],
							},
							method => method.Return($"new {generatorClassName}()")
						)
			);

		writer
			.XmlSummary(
				$"Assigns {strategyDescription} to an unset key of this value object.",
				"Entity Framework Core only invokes a generator while the property still holds its CLR default, so a",
				"value supplied by domain code is never overwritten."
			)
			.Class(
				new TypeDeclarationOptions(generatorClassName)
				{
					Accessibility = accessibility,
					IsSealed = true,
					IsPartial = false,
					BaseType = generatorType,
				},
				generatorBody =>
				{
					generatorBody
						.XmlSummary("False: the generated identifier is the permanent key value.")
						.Property(
							new PropertyDeclarationOptions(
								"GeneratesTemporaryValues",
								PurviewTypeLibrary.System.Boolean,
								TypeDeclarationAccessibility.Public
							)
							{
								IsOverride = true,
								ExpressionBody = "false",
							}
						);

					generatorBody
						.XmlSummary(
							"Creates the identifier.",
							"Hydration is the persistence path and never re-runs validation: the generated value is well",
							"formed by construction."
						)
						.Method(
							new MethodDeclarationOptions("Next", valueObjectType, TypeDeclarationAccessibility.Public)
							{
								IsOverride = true,
								Parameters = [new("entry", entityEntryType)],
							},
							method =>
								method.Return(
									$"{valueObjectTypeName}.Hydrate({TypeLibrary.EFSequentialGuidFullTypeName}{sequentialGuidExpression})"
								)
						);
				}
			);
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
