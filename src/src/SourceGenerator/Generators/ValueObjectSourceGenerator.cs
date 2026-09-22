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

		var efDisabled = IncrementalPipeline.IsDisabledValueProvider(context, PropertyLibrary.DisableEFGeneration);
		var registryDisabled = IncrementalPipeline.IsDisabledValueProvider(
			context,
			PropertyLibrary.DisableEFRegistryGeneration
		);

		// Keep the compilation reference stable across identical reruns so the incremental pipeline
		// short-circuits instead of re-executing every value-object transform (see PreCompilationMarker).
#pragma warning disable RSEXPERIMENTAL007 // Pre-compilation source output is intentionally used to stabilize the incremental cache.
		context.RegisterPreCompilationSourceOutput(
			PreCompilationMarker.Provider(context),
			static (spc, source) => spc.AddSource(PreCompilationMarker.HintName, source)
		);
#pragma warning restore RSEXPERIMENTAL007

		var scalarCandidates = IncrementalPipeline.ForAttributeWithMetadataName(
			context,
			TypeLibrary.Purview.ValueObjects.Serialization.ScalarAttribute,
			static (ctx, ct) =>
				ScalarValueObjectModelBuilder.Build(
					(INamedTypeSymbol)ctx.TargetSymbol,
					(TypeDeclarationSyntax)ctx.TargetNode,
					ctx.SemanticModel.Compilation,
					ct
				),
			static (node, _) => node is TypeDeclarationSyntax,
			trackingName: "GetScalarValueObjectTargets"
		);

		var complexCandidates = IncrementalPipeline.ForAttributeWithMetadataName(
			context,
			TypeLibrary.Purview.ValueObjects.Serialization.ValueObjectAttribute,
			static (ctx, ct) =>
				ComplexValueObjectModelBuilder.Build(
					(INamedTypeSymbol)ctx.TargetSymbol,
					(TypeDeclarationSyntax)ctx.TargetNode,
					ctx.SemanticModel.Compilation,
					ct
				),
			static (node, _) => node is TypeDeclarationSyntax,
			trackingName: "GetComplexValueObjectTargets"
		);

		context.RegisterSourceOutput(
			scalarCandidates.Combine(generationContext.Combine(efDisabled)),
			static (spc, tuple) => EmitScalarResult(spc, tuple.Left, tuple.Right.Left, tuple.Right.Right)
		);
		context.RegisterSourceOutput(
			complexCandidates.Combine(generationContext.Combine(efDisabled)),
			static (spc, tuple) => EmitComplexResult(spc, tuple.Left, tuple.Right.Left, tuple.Right.Right)
		);

		RegisterEFRegistryOutput(
			context,
			scalarCandidates,
			complexCandidates,
			efDisabled,
			registryDisabled,
			generationContext
		);
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
		ScalarValueObjectEmitter.Emit(writer, result.Value, emitEF: !efDisabled);
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
		ComplexValueObjectEmitter.Emit(writer, result.Value, emitEF: !efDisabled);
		context.AddSource(result.Value.HintName, writer);
	}

	static void RegisterEFRegistryOutput(
		IncrementalGeneratorInitializationContext context,
		IncrementalValuesProvider<GeneratorResult<ScalarValueObjectModel>> scalarCandidates,
		IncrementalValuesProvider<GeneratorResult<ComplexValueObjectModel>> complexCandidates,
		IncrementalValueProvider<bool> efDisabled,
		IncrementalValueProvider<bool> registryDisabled,
		IncrementalValueProvider<GeneratorContext> generationContext
	)
	{
		var scalarDescriptors = scalarCandidates
			.Select(static (result, _) => result.ShouldProcess ? BuildScalarEFDescriptor(result.Value) : null)
			.Collect();
		var complexDescriptors = complexCandidates
			.Select(static (result, _) => result.ShouldProcess ? BuildComplexEFDescriptor(result.Value) : null)
			.Collect();

		// Discover EF-enabled value objects declared in referenced assemblies (e.g. a shared domain
		// models project) via their marker interfaces so the consumer registry maps them too.
		var referencedDescriptors = context.CompilationProvider.Select(
			static (compilation, cancellationToken) =>
				ReferencedEFValueObjectDiscovery.Scan(compilation, cancellationToken)
		);

		var combinedDescriptors = scalarDescriptors.Combine(complexDescriptors).Combine(referencedDescriptors);

		var anyEFValueObject = combinedDescriptors.Select(
			static (pair, _) =>
				pair.Left.Left.Any(static descriptor => descriptor is not null)
				|| pair.Left.Right.Any(static descriptor => descriptor is not null)
				|| !pair.Right.IsEmpty
		);

		var registryInput = anyEFValueObject
			.Combine(efDisabled.Combine(registryDisabled))
			.Combine(combinedDescriptors)
			.Combine(generationContext);

		context.RegisterSourceOutput(
			registryInput,
			static (spc, tuple) =>
			{
				var (left, generationContext) = tuple;
				var (anyEF, combined) = left;
				var (anyValueObject, efOptions) = anyEF;
				var (efDisabled, registryDisabled) = efOptions;
				if (generationContext.Settings.IsSourceGeneratorDisabled)
					return;

				if (!anyValueObject || efDisabled || registryDisabled)
					return;

				var scalars = ImmutableArray.CreateBuilder<EFScalarDescriptor>();
				var complex = ImmutableArray.CreateBuilder<EFComplexDescriptor>();
				foreach (var descriptor in combined.Left.Left)
				{
					if (descriptor is not null)
						scalars.Add(descriptor.Value);
				}

				foreach (var descriptor in combined.Left.Right)
				{
					if (descriptor is not null)
						complex.Add(descriptor.Value);
				}

				scalars.AddRange(combined.Right.Scalars);
				complex.AddRange(combined.Right.Complex);

				if (scalars.Count == 0 && complex.Count == 0)
					return;

				var writer = generationContext.CreateCodeWriter();
				ValueObjectEFRegistryEmitter.Emit(writer, scalars, complex);
				spc.AddSource($"{TypeLibrary.EFValueObjectExtensionsClassName}.g.cs", writer);
			}
		);
	}

	static EFScalarDescriptor? BuildScalarEFDescriptor(ScalarValueObjectModel model)
	{
		if (!model.IsEFReferenced || (!model.Options.GenerateEFConverter && !model.Options.GenerateEFComparer))
			return null;

		// If the value object is EF-referenced, but neither a converter nor a comparer is requested, we don't need to generate any EF-related code.
		return new(
			model.TypeModel.FullyQualifiedName,
			model.Options.GenerateEFConverter,
			model.Options.GenerateEFComparer,
			model.EFProviderMappable
		);
	}

	static EFComplexDescriptor? BuildComplexEFDescriptor(ComplexValueObjectModel model)
	{
		if (
			!model.IsEFReferenced
			|| (
				model.Options.EFMapping != null
				&& ValueObjectSymbolInspector.IsEFMappingNone(model.Options.EFMapping)
				&& !model.Options.GenerateEFComparer
			)
		)
			return null;

		// If the value object is EF-referenced, but neither a mapping nor a comparer is requested, we don't need to generate any EF-related code.
		return new(
			model.TypeModel.FullyQualifiedName,
			model.Options.EFMapping,
			model.IsEF8Referenced,
			model.Options.GenerateEFComparer,
			model.Options.EFMapping != null && ValueObjectSymbolInspector.IsEFMappingJson(model.Options.EFMapping)
		);
	}
}
