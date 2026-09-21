using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.ValueObjects.SourceGenerator.Generators;

[Generator]
public sealed partial class ValueObjectSourceGenerator : IIncrementalGenerator
{
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		context
			.RegisterEmbeddedAttribute<ValueObjectSourceGenerator>()
			.RegisterPostInitializationOutput(ctx =>
			{
				foreach (var (HintName, Source) in ValueObjectsAttributeEmitter.EmitAttributes())
					ctx.AddSource(HintName, Source);
			});

		var generationContext = IncrementalPipeline.GenerationContextValueProvider(
			context,
			SourceGenLibrary.CreateGenerationSettings<ValueObjectSourceGenerator>(
				PropertyLibrary.DisableSourceGenerator
			),
			static (_, _, _, _) => EmptyCapabilities.Instance
		);

		var efDisabled = IncrementalPipeline.IsDisabledValueProvider(context, PropertyLibrary.DisableEfGeneration);

		// Keep the compilation reference stable across identical reruns so the incremental pipeline
		// short-circuits instead of re-executing every value-object transform (see PreCompilationMarker).
#pragma warning disable RSEXPERIMENTAL007 // Pre-compilation source output is intentionally used to stabilize the incremental cache.
		context.RegisterPreCompilationSourceOutput(
			PreCompilationMarker.Provider(context),
			static (spc, source) => spc.AddSource(PreCompilationMarker.HintName, source)
		);
#pragma warning restore RSEXPERIMENTAL007

		var scalarCandidates = context
			.SyntaxProvider.ForAttributeWithMetadataName(
				ValueObjectSymbolInspector.ScalarAttributeName,
				predicate: static (node, _) => node is TypeDeclarationSyntax,
				transform: static (ctx, ct) =>
					ScalarValueObjectModelBuilder.Build(
						(INamedTypeSymbol)ctx.TargetSymbol,
						(TypeDeclarationSyntax)ctx.TargetNode,
						ctx.SemanticModel.Compilation,
						ct
					)
			)
			.WithTrackingName("GetScalarValueObjectTargets");

		var complexCandidates = context
			.SyntaxProvider.ForAttributeWithMetadataName(
				ValueObjectSymbolInspector.ValueObjectAttributeName,
				predicate: static (node, _) => node is TypeDeclarationSyntax,
				transform: static (ctx, ct) =>
					ComplexValueObjectModelBuilder.Build(
						(INamedTypeSymbol)ctx.TargetSymbol,
						(TypeDeclarationSyntax)ctx.TargetNode,
						ctx.SemanticModel.Compilation,
						ct
					)
			)
			.WithTrackingName("GetComplexValueObjectTargets");

		context.RegisterSourceOutput(
			scalarCandidates.Combine(generationContext.Combine(efDisabled)),
			static (spc, tuple) => EmitScalarResult(spc, tuple.Left, tuple.Right.Left, tuple.Right.Right)
		);
		context.RegisterSourceOutput(
			complexCandidates.Combine(generationContext.Combine(efDisabled)),
			static (spc, tuple) => EmitComplexResult(spc, tuple.Left, tuple.Right.Left, tuple.Right.Right)
		);

		RegisterEfRegistryOutput(context, scalarCandidates, complexCandidates, efDisabled, generationContext);
	}

	static void EmitScalarResult(
		SourceProductionContext context,
		GeneratorResult<ScalarValueObjectModel> result,
		GeneratorContext generationContext,
		bool efDisabled
	)
	{
		if (generationContext.Settings.IsSourceGeneratorDisabled)
			return;

		if (!result.ShouldProcess)
			return;

		var writer = generationContext.CreateCodeWriter();
		ScalarValueObjectEmitter.Emit(writer, result.Value, emitEf: !efDisabled);
		context.AddSource(result.Value.HintName, writer);
	}

	static void EmitComplexResult(
		SourceProductionContext context,
		GeneratorResult<ComplexValueObjectModel> result,
		GeneratorContext generationContext,
		bool efDisabled
	)
	{
		if (generationContext.Settings.IsSourceGeneratorDisabled)
			return;

		if (!result.ShouldProcess)
			return;

		var writer = generationContext.CreateCodeWriter();
		ComplexValueObjectEmitter.Emit(writer, result.Value, emitEf: !efDisabled);
		context.AddSource(result.Value.HintName, writer);
	}

	static void RegisterEfRegistryOutput(
		IncrementalGeneratorInitializationContext context,
		IncrementalValuesProvider<GeneratorResult<ScalarValueObjectModel>> scalarCandidates,
		IncrementalValuesProvider<GeneratorResult<ComplexValueObjectModel>> complexCandidates,
		IncrementalValueProvider<bool> efDisabled,
		IncrementalValueProvider<GeneratorContext> generationContext
	)
	{
		var scalarDescriptors = scalarCandidates
			.Select(static (result, _) => result.ShouldProcess ? BuildScalarEfDescriptor(result.Value) : null)
			.Collect();
		var complexDescriptors = complexCandidates
			.Select(static (result, _) => result.ShouldProcess ? BuildComplexEfDescriptor(result.Value) : null)
			.Collect();

		var combinedDescriptors = scalarDescriptors.Combine(complexDescriptors);

		var anyEfValueObject = combinedDescriptors.Select(
			static (pair, _) =>
				pair.Left.Any(static descriptor => descriptor is not null)
				|| pair.Right.Any(static descriptor => descriptor is not null)
		);

		var registryInput = anyEfValueObject
			.Combine(efDisabled)
			.Combine(combinedDescriptors)
			.Combine(generationContext);

		context.RegisterSourceOutput(
			registryInput,
			static (spc, tuple) =>
			{
				var (left, generationContext) = tuple;
				var (anyEf, combined) = left;
				var (anyValueObject, efDisabled) = anyEf;
				if (!anyValueObject || efDisabled)
					return;

				var scalars = ImmutableArray.CreateBuilder<EfScalarDescriptor>();
				var complex = ImmutableArray.CreateBuilder<EfComplexDescriptor>();
				foreach (var descriptor in combined.Left)
				{
					if (descriptor is not null)
						scalars.Add(descriptor.Value);
				}

				foreach (var descriptor in combined.Right)
				{
					if (descriptor is not null)
						complex.Add(descriptor.Value);
				}

				if (scalars.Count == 0 && complex.Count == 0)
					return;

				var writer = generationContext.CreateCodeWriter();
				ValueObjectEfRegistryEmitter.Emit(writer, scalars, complex);
				spc.AddSource($"{TypeLibrary.EfValueObjectExtensionsClassName}.g.cs", writer);
			}
		);
	}

	static EfScalarDescriptor? BuildScalarEfDescriptor(ScalarValueObjectModel model)
	{
		if (!model.IsEfReferenced || (!model.Options.GenerateEfConverter && !model.Options.GenerateEfComparer))
			return null;

		// If the value object is EF-referenced, but neither a converter nor a comparer is requested, we don't need to generate any EF-related code.
		return new(
			model.TypeModel.FullyQualifiedName,
			model.Options.GenerateEfConverter,
			model.Options.GenerateEfComparer,
			model.EfProviderMappable
		);
	}

	static EfComplexDescriptor? BuildComplexEfDescriptor(ComplexValueObjectModel model)
	{
		if (
			!model.IsEfReferenced
			|| (
				model.Options.EfMapping != null
				&& ValueObjectSymbolInspector.IsEfMappingNone(model.Options.EfMapping)
				&& !model.Options.GenerateEfComparer
			)
		)
			return null;

		// If the value object is EF-referenced, but neither a mapping nor a comparer is requested, we don't need to generate any EF-related code.
		return new(
			model.TypeModel.FullyQualifiedName,
			model.Options.EfMapping,
			model.IsEf8Referenced,
			model.Options.GenerateEfComparer,
			model.Options.EfMapping != null && ValueObjectSymbolInspector.IsEfMappingJson(model.Options.EfMapping)
		);
	}
}
