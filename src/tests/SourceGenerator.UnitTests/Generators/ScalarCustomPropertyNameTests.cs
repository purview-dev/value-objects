namespace Purview.ValueObjects.SourceGenerator.Generators;

/// <summary>
/// Regression tests for <c>[Scalar("CustomName")]</c>.
/// </summary>
/// <remarks>
/// <see cref="ScalarOptionsAttribute.PropertyName"/> is a documented public
/// option, but the generated partial unconditionally implements
/// <c>IScalarValueObject&lt;TSelf, TValue&gt;</c>, which declares a member named <c>Value</c>. With a
/// custom name no <c>Value</c> was emitted, so the generated code failed with CS0535 — an error inside
/// generated code that the consumer cannot edit. These tests must therefore <b>compile</b> the
/// generated output; asserting on generated text would not have caught it, which is why the existing
/// coverage (an incremental-cache test that never compiles) missed it.
/// </remarks>
public sealed class ScalarCustomPropertyNameTests : ValueObjectSourceGeneratorTestBase
{
	[Test]
	public async Task ScalarGeneration_GivenCustomPropertyName_CompilesAndForwardsValue(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{

			[Purview.ValueObjects.Serialization.Scalar("Amount")]
			public readonly partial record struct Money
			{
				public decimal Amount { get; }
			}

			public static class CustomNameHarness
			{
				public static decimal CreateThenReadCustomName() => Money.Create(12.5m).Amount;

				// The interface contract the generated type advertises must actually be satisfied.
				public static decimal ReadThroughInterface()
				{
					global::Purview.ValueObjects.IScalarValueObject<Money, decimal> asInterface = Money.Create(12.5m);

					return asInterface.Value;
				}

				public static bool ValueForwardsToCustomName()
				{
					var money = Money.Create(99.25m);
					global::Purview.ValueObjects.IScalarValueObject<Money, decimal> asInterface = money;

					return asInterface.Value == money.Amount;
				}
			}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsGeneratorTestOptions.Default.Compile(), cancellationToken);

		// The assembly is only non-null when the generated code compiled, which is the actual assertion.
		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();

		var harnessType = assembly.GetType("Testing.CustomNameHarness")!;

		var created = (decimal)harnessType.GetMethod("CreateThenReadCustomName")!.Invoke(null, null)!;
		var throughInterface = (decimal)harnessType.GetMethod("ReadThroughInterface")!.Invoke(null, null)!;
		var forwards = (bool)harnessType.GetMethod("ValueForwardsToCustomName")!.Invoke(null, null)!;

		await Assert.That(created).IsEqualTo(12.5m);
		await Assert.That(throughInterface).IsEqualTo(12.5m);
		await Assert.That(forwards).IsTrue();
	}

	[Test]
	public async Task ScalarGeneration_GivenDefaultPropertyName_DoesNotEmitADuplicateValueMember(
		CancellationToken cancellationToken
	)
	{
		// The forwarding member must only appear when the scalar member is named something other than
		// "Value", otherwise the generated partial would declare Value twice.
		const string source = """
			namespace Testing
			{

			[Purview.ValueObjects.Serialization.Scalar]
			public readonly partial record struct EmailAddress
			{
				public string Value { get; }
			}

			public static class DefaultNameHarness
			{
				public static string CreateThenRead() => EmailAddress.Create("a@b.com").Value;
			}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsGeneratorTestOptions.Default.Compile(), cancellationToken);
		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();

		var harnessType = assembly.GetType("Testing.DefaultNameHarness")!;
		var created = (string)harnessType.GetMethod("CreateThenRead")!.Invoke(null, null)!;

		await Assert.That(created).IsEqualTo("a@b.com");
	}
}
