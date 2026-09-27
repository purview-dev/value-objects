using Purview.SourceGeneratorFramework;

namespace Purview.ValueObjects.SourceGenerator.Analyzers;

public sealed class ValueObjectDiagnosticAnalyzerTests : AnalyzerTestBase<ValueObjectDiagnosticAnalyzer>
{
	[Test]
	public async Task Generate_GivenNonPartialValueObject_ReportsValueObjectMustBePartial(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
				[Scalar]
				public readonly record struct EmailAddress
				{
					public string Value { get; }
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.ValueObjectMustBePartial);
	}

	[Test]
	public async Task Generate_GivenNestedValueObject_ReportsNestedValueObjectsAreNotSupported(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
				public class Outer
				{
					[Scalar]
					public readonly partial record struct EmailAddress
					{
						public string Value { get; }
					}
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.NestedValueObjectsAreNotSupported);
	}

	[Test]
	public async Task Generate_GivenGenericValueObject_ReportsGenericValueObjectsAreNotSupported(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
				[Scalar]
				public readonly partial record struct EmailAddress<T>
				{
					public string Value { get; }
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.GenericValueObjectsAreNotSupported);
	}

	[Test]
	public async Task Generate_GivenMissingScalarProperty_ReportsScalarPropertyMissing(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
				[Scalar]
				public readonly partial record struct EmailAddress
				{
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.ScalarPropertyMissing);
	}

	[Test]
	public async Task Generate_GivenNonRecordStructScalar_ReportsScalarShouldBeRecordStruct(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
				[Scalar]
				public partial struct EmailAddress
				{
					public string Value { get; }
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.ScalarShouldBeRecordStruct);
	}

	[Test]
	public async Task Generate_GivenConflictingValueObjectAttributes_ReportsConflictingValueObjectAttributes(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
				[Scalar]
				[ValueObject]
				public readonly partial record struct EmailAddress
				{
					public string Value { get; }
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.ConflictingValueObjectAttributes);
	}

	[Test]
	public async Task Generate_GivenStrictModeWithoutCreate_ReportsStrictDeserializationRequiresCreate(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
				[Scalar(DeserializationMode = ValueObjectDeserializationMode.Strict)]
				public readonly partial record struct EmailAddress
				{
					public string Value { get; }
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.StrictDeserializationRequiresCreate);
	}

	[Test]
	public async Task Generate_GivenValidScalarValueObject_ReportsNoDiagnostics(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
				[Scalar]
				public readonly partial record struct EmailAddress
				{
					public string Value { get; }
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasNoDiagnostics();
	}

	[Test]
	public async Task Generate_GivenMutableScalarMember_ReportsValueObjectMemberIsMutable(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
				[Scalar]
				public partial record struct EmailAddress
				{
					public string Value { get; set; }
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.ValueObjectMemberIsMutable);
	}

	[Test]
	public async Task Generate_GivenInitOnlyScalarMember_DoesNotReportValueObjectMemberIsMutable(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
				[Scalar]
				public readonly partial record struct EmailAddress
				{
					public string Value { get; init; }
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.ValueObjectMemberIsMutable);
	}

	/// <summary>
	/// The <c>[ZodSchema]</c> attribute is emitted into the consuming compilation by the ZodSharp
	/// generator, so a stub with the same shape keeps the ZodSharp diagnostics tests self-contained.
	/// </summary>
	const string ZodSchemaAttributeStub = """
		namespace ZodSharp
		{
			[System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
			public sealed class ZodSchemaAttribute : System.Attribute
			{
				public string? SchemaName { get; init; }

				public string? RefinementMethodName { get; init; }
			}
		}
		""";

	[Test]
	public async Task Generate_GivenImplementedZodRefinementHookWithShadowingMethod_ReportsRefinementHookNotInvoked(
		CancellationToken cancellationToken
	)
	{
		// A member named like the refinement method (default 'Validate') makes the generator defer the
		// refinement to ZodSharp, so the hook the caller implemented is declared but never invoked.
		const string source =
			ZodSchemaAttributeStub
			+ """

				namespace Testing
				{
					[Scalar]
					[ZodSharp.ZodSchema]
					public readonly partial record struct EmailAddress
					{
						public string Value { get; }

						public void Validate() { }

						partial void OnZodValidate(object context);

						partial void OnZodValidate(object context)
						{
							_ = context;
						}
					}
				}
				""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.ZodRefinementHookNotInvoked);
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.ZodRefinementNameShadowed);
	}

	[Test]
	public async Task Generate_GivenZodRefinementNameShadowedByProperty_ReportsRefinementNameShadowed(
		CancellationToken cancellationToken
	)
	{
		// ZodSharp binds refinements by looking for a *method* of the configured name. A property with
		// that name makes the generator step aside while ZodSharp reports nothing, so no Zod rules - and
		// no hook - apply to the value object.
		const string source =
			ZodSchemaAttributeStub
			+ """

				namespace Testing
				{
					[Scalar]
					[ZodSharp.ZodSchema]
					public readonly partial record struct EmailAddress
					{
						public string Value { get; }

						public string Validate => "not-a-refinement";
					}
				}
				""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.ZodRefinementNameShadowed);
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.ZodRefinementHookNotInvoked);
	}

	[Test]
	public async Task Generate_GivenInsteadOfHooksWithImplementedOnValidate_ReportsOnValidateSkipped(
		CancellationToken cancellationToken
	)
	{
		const string source =
			ZodSchemaAttributeStub
			+ """

				namespace Testing
				{
					[Scalar(ZodSchemaMode = ZodSchemaMode.InsteadOfHooks)]
					[ZodSharp.ZodSchema]
					public readonly partial record struct EmailAddress
					{
						public string Value { get; }

						static partial void OnValidate(string value);

						static partial void OnValidate(string value)
						{
							throw new System.ArgumentException("OnValidate must not run.", nameof(value));
						}
					}
				}
				""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.OnValidateSkippedByInsteadOfHooks);
	}

	[Test]
	public async Task Generate_GivenInvalidZodSchemaName_ReportsZodSchemaNameInvalid(
		CancellationToken cancellationToken
	)
	{
		// ZodSharp applies any non-empty SchemaName, so a name the value object generator cannot
		// reference must be reported instead of silently falling back to "{TypeName}Schema".
		const string source =
			ZodSchemaAttributeStub
			+ """

				namespace Testing
				{
					[Scalar]
					[ZodSharp.ZodSchema(SchemaName = "Not A Name")]
					public readonly partial record struct EmailAddress
					{
						public string Value { get; }
					}
				}
				""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.ZodSchemaNameInvalid);
	}

	[Test]
	public async Task Generate_GivenWellFormedZodIntegration_ReportsNoZodDiagnostics(
		CancellationToken cancellationToken
	)
	{
		// Implements the generated hook, declares no member that shadows the refinement name, keeps the
		// default InAdditionToHooks mode, and uses a valid custom schema name.
		const string source =
			ZodSchemaAttributeStub
			+ """

				namespace Testing
				{
					[Scalar]
					[ZodSharp.ZodSchema(SchemaName = "CorporateEmailSchema")]
					public readonly partial record struct CorporateEmail
					{
						public string Value { get; }

						partial void OnZodValidate(object context);

						partial void OnZodValidate(object context)
						{
							_ = context;
						}
					}
				}
				""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.ZodRefinementHookNotInvoked);
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.ZodRefinementNameShadowed);
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.OnValidateSkippedByInsteadOfHooks);
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.ZodSchemaNameInvalid);
	}

	protected override AnalyzerTestOptions OnBeforeRun(
		IEnumerable<string> sources,
		AnalyzerTestOptions options,
		CancellationToken cancellationToken
	)
	{
		return base.OnBeforeRun(
			sources,
			// Fully-qualified because the referenced ZodSharp generator assembly exposes a
			// global-namespace TypeLibrary that would otherwise shadow the source generator's.
			options.WithAdditionalNamespaces(TypeLibrary.SerializationNamespace),
			cancellationToken
		);
	}
}
