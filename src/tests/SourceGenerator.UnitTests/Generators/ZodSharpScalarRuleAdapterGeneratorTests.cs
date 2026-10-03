namespace Purview.ValueObjects.SourceGenerator.Generators;

/// <summary>
/// Tests the assembly-level <c>ScalarRuleAdapter</c> emission: it is only emitted when the compilation
/// references the Purview.ZodSharp runtime, and never over a declaration the consumer already made.
/// </summary>
/// <remarks>
/// The adapter only needs the ZodSharp runtime's rule contract, so these tests reference the runtime without
/// running the ZodSharp schema generator: the emission is a value-object generator decision.
/// </remarks>
public sealed class ZodSharpScalarRuleAdapterGeneratorTests
	: ValueObjectSourceGeneratorTestBase<ValueObjectsGeneratorTestOptions>
{
	[Test]
	public async Task Emit_GivenZodSharpReferenced_EmitsAdapter(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar]
				public readonly partial record struct EmailAddress
				{
					public string Value { get; }
				}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsGeneratorTestOptions.Default, cancellationToken);

		var adapter = await Assert.That(result.GetSource(TypeLibrary.ScalarRuleAdapterHintName)).IsNotNull();

		await Assert
			.That(adapter!)
			.Contains("internal readonly record struct ScalarRuleAdapter<TSelf, TValue, TRule>(TRule Rule)");
		await Assert.That(adapter).Contains("global::ZodSharp.Core.IValidationRule<TSelf>");
		await Assert.That(adapter).Contains("Rule.IsValid(value.Value)");
	}

	[Test]
	public async Task Emit_GivenZodSharpReferenced_IsUsableByAScalarRuleFamily(CancellationToken cancellationToken)
	{
		// The adapter is the seam a scalar rule family composes; composing it here proves the emitted type
		// satisfies the [ZodRule] rule contract end to end.
		const string source = """
			using ZodSharp.Core;

			namespace Testing
			{
				public readonly record struct NonSentinelRule<T>(string? Message = null) : IValidationRule<T>
					where T : System.IEquatable<T>
				{
					public bool IsValid(in T value) => !value!.Equals(default!);

					public string GetErrorMessage(in T value) => Message ?? "Value must not be the default.";
				}

				public readonly record struct NonSentinelScalarRule<TSelf>(string? Message = null) : IValidationRule<TSelf>
					where TSelf : Purview.ValueObjects.IScalarValueObject<TSelf, System.Guid>
				{
					public bool IsValid(in TSelf value) =>
						new global::Purview.ValueObjects.ScalarRuleAdapter<TSelf, System.Guid, NonSentinelRule<System.Guid>>(
							new(Message)
						).IsValid(value);

					public string GetErrorMessage(in TSelf value) =>
						new global::Purview.ValueObjects.ScalarRuleAdapter<TSelf, System.Guid, NonSentinelRule<System.Guid>>(
							new(Message)
						).GetErrorMessage(value);
				}

				[Purview.ValueObjects.Serialization.Scalar]
				public readonly partial record struct TenantId
				{
					public System.Guid Value { get; }
				}

				public static class Harness
				{
					public static bool AcceptsNonSentinel() =>
						new NonSentinelScalarRule<TenantId>().IsValid(TenantId.Hydrate(System.Guid.NewGuid()));

					public static bool RejectsSentinel() =>
						!new NonSentinelScalarRule<TenantId>().IsValid(TenantId.Hydrate(System.Guid.Empty));
				}
			}
			""";

		var result = await GenerateAsync(
			source,
			ValueObjectsGeneratorTestOptions.Default with
			{
				CompileToAssembly = true,
			},
			cancellationToken
		);

		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harness = assembly!.GetType("Testing.Harness")!;

		var accepts = (bool)harness.GetMethod("AcceptsNonSentinel")!.Invoke(null, null)!;
		var rejects = (bool)harness.GetMethod("RejectsSentinel")!.Invoke(null, null)!;

		await Assert.That(accepts).IsTrue();
		await Assert.That(rejects).IsTrue();
	}

	[Test]
	public async Task Emit_GivenConsumerDeclaredAdapter_IsSkipped(CancellationToken cancellationToken)
	{
		// A consumer that copied the adapter under the earlier guidance keeps theirs: emission is skipped so
		// the compilation does not declare the type twice.
		const string source = """
			using ZodSharp.Core;

			namespace Purview.ValueObjects
			{
				public readonly record struct ScalarRuleAdapter<TSelf, TValue, TRule>(TRule Rule) : IValidationRule<TSelf>
					where TSelf : Purview.ValueObjects.IScalarValueObject<TSelf, TValue>
					where TRule : IValidationRule<TValue>
				{
					public bool IsValid(in TSelf value) => Rule.IsValid(value.Value);

					public string GetErrorMessage(in TSelf value) => Rule.GetErrorMessage(value.Value);
				}
			}

			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar]
				public readonly partial record struct EmailAddress
				{
					public string Value { get; }
				}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsGeneratorTestOptions.Default, cancellationToken);

		await Assert.That(result.GetSource(TypeLibrary.ScalarRuleAdapterHintName)).IsNull();
	}

	[Test]
	public async Task ResolveState_GivenZodSharpNotReferenced_DoesNotWantTheAdapter()
	{
		// The adapter forwards to the ZodSharp rule contract, so without that reference the emitted source
		// would not compile. The harness always references the assemblies loaded into the test process, so
		// the reference set is built explicitly here rather than through the generator options.
		var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(
			"WithoutZodSharp",
			[],
			[Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
			new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(
				Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary
			)
		);

		var state = ZodSharpScalarRuleAdapterEmitter.ResolveState(compilation);

		await Assert.That(state.IsZodSharpReferenced).IsFalse();
		await Assert.That(state.ShouldEmit).IsFalse();
	}

	[Test]
	public async Task ResolveState_GivenZodSharpReferencedAndNoExistingAdapter_WantsTheAdapter()
	{
		var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(
			"WithZodSharp",
			[],
			[
				Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
				Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(
					typeof(ZodSharp.Core.IValidationRule<>).Assembly.Location
				),
			],
			new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(
				Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary
			)
		);

		var state = ZodSharpScalarRuleAdapterEmitter.ResolveState(compilation);

		await Assert.That(state.IsZodSharpReferenced).IsTrue();
		await Assert.That(state.ShouldEmit).IsTrue();
	}
}
