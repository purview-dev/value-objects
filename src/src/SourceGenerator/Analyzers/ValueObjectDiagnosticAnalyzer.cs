using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Purview.ValueObjects.SourceGenerator.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ValueObjectDiagnosticAnalyzer : DiagnosticAnalyzer
{
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
		[
			DiagnosticLibrary.ValueObjectMustBePartial,
			DiagnosticLibrary.NestedValueObjectsAreNotSupported,
			DiagnosticLibrary.GenericValueObjectsAreNotSupported,
			DiagnosticLibrary.ConflictingValueObjectAttributes,
			DiagnosticLibrary.ScalarPropertyMissing,
			DiagnosticLibrary.ScalarShouldBeRecordStruct,
			DiagnosticLibrary.StrictDeserializationRequiresCreate,
			DiagnosticLibrary.EFMappingRequiresEntityFramework,
			DiagnosticLibrary.EFAutoConversionSkipped,
		];

	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
			throw new ArgumentNullException(nameof(context));

		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterCompilationStartAction(compilationContext =>
		{
			var scalarAttribute = compilationContext.Compilation.GetTypeByMetadataName(
				TypeLibrary.Purview.ValueObjects.Serialization.ScalarAttributeFullName
			);
			var valueObjectAttribute = compilationContext.Compilation.GetTypeByMetadataName(
				TypeLibrary.Purview.ValueObjects.Serialization.ValueObjectAttributeFullName
			);
			if (scalarAttribute is null && valueObjectAttribute is null)
				return;

			compilationContext.RegisterSymbolAction(
				context => ValidateValueObject(context, scalarAttribute, valueObjectAttribute),
				SymbolKind.NamedType
			);
		});
	}

	static void ValidateValueObject(
		SymbolAnalysisContext context,
		INamedTypeSymbol? scalarAttribute,
		INamedTypeSymbol? valueObjectAttribute
	)
	{
		var typeSymbol = (INamedTypeSymbol)context.Symbol;

		var hasScalarAttribute = scalarAttribute is not null && HasAttribute(typeSymbol, scalarAttribute);
		var hasValueObjectAttribute =
			valueObjectAttribute is not null && HasAttribute(typeSymbol, valueObjectAttribute);
		if (!hasScalarAttribute && !hasValueObjectAttribute)
			return;

		if (
			typeSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(context.CancellationToken)
			is not TypeDeclarationSyntax syntax
		)
			return;

		if (hasScalarAttribute)
		{
			var result = ScalarValueObjectModelBuilder.Build(
				typeSymbol,
				syntax,
				context.Compilation,
				context.CancellationToken
			);

			foreach (var diagnostic in result.Diagnostics)
				context.ReportDiagnostic(diagnostic.ToDiagnostic());
		}
		else
		{
			var result = ComplexValueObjectModelBuilder.Build(
				typeSymbol,
				syntax,
				context.Compilation,
				context.CancellationToken
			);

			foreach (var diagnostic in result.Diagnostics)
				context.ReportDiagnostic(diagnostic.ToDiagnostic());
		}
	}

	static bool HasAttribute(INamedTypeSymbol typeSymbol, INamedTypeSymbol attributeType) =>
		typeSymbol
			.GetAttributes()
			.Any(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType));
}
