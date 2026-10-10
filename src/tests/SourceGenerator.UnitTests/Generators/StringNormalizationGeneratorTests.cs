namespace Purview.ValueObjects.SourceGenerator.Generators;

/// <summary>
/// Tests the built-in string normalization options (<c>Trim</c> and <c>Casing</c>) on
/// <c>[Scalar]</c>/<c>[Scalar&lt;T&gt;]</c>, <c>[StringNormalize]</c> on complex members, and the
/// assembly-level <c>[ValueObjectDefaults]</c>.
/// </summary>
public sealed class StringNormalizationGeneratorTests : ValueObjectSourceGeneratorTestBase
{
	[Test]
	public async Task Scalar_GivenTrim_TrimsOnCreateButNotHydrate(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar<string>(Trim = true)]
				public readonly partial record struct Name { }

				public static class NameHarness
				{
					public static string CreateTrimmed() => Name.Create("  Bob  ").Value;

					public static string HydratePreserves() => Name.Hydrate("  Bob  ").Value;
				}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsGeneratorTestOptions.Default.Compile(), cancellationToken);

		var createBody =
			result.Generated().GetRecord("Name", "Testing").GetMethod("Create").Node.Body?.ToString() ?? string.Empty;
		await Assert.That(createBody).Contains("value = value?.Trim()!;");

		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harness = assembly.GetType("Testing.NameHarness")!;

		await Assert.That((string)harness.GetMethod("CreateTrimmed")!.Invoke(null, null)!).IsEqualTo("Bob");
		await Assert.That((string)harness.GetMethod("HydratePreserves")!.Invoke(null, null)!).IsEqualTo("  Bob  ");
	}

	[Test]
	public async Task Scalar_GivenCasing_CanonicalizesCaseOnCreate(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar<string>(Casing = Purview.ValueObjects.Serialization.StringCasing.LowerInvariant)]
				public readonly partial record struct Email { }

				public static class EmailHarness
				{
					public static string CreateLowered() => Email.Create("  Bob@Example.COM ").Value;
				}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsGeneratorTestOptions.Default.Compile(), cancellationToken);

		var createBody =
			result.Generated().GetRecord("Email", "Testing").GetMethod("Create").Node.Body?.ToString() ?? string.Empty;
		await Assert.That(createBody).Contains("value = value?.ToLowerInvariant()!;");

		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harness = assembly.GetType("Testing.EmailHarness")!;

		await Assert
			.That((string)harness.GetMethod("CreateLowered")!.Invoke(null, null)!)
			.IsEqualTo("  bob@example.com ");
	}

	[Test]
	public async Task Scalar_GivenTrimAndCasing_AppliesTrimThenCasing(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar<string>(Trim = true, Casing = Purview.ValueObjects.Serialization.StringCasing.UpperInvariant)]
				public readonly partial record struct CurrencyCode { }

				public static class CurrencyHarness
				{
					public static string CreateCanonical() => CurrencyCode.Create(" gbp ").Value;
				}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsGeneratorTestOptions.Default.Compile(), cancellationToken);

		var createBody =
			result.Generated().GetRecord("CurrencyCode", "Testing").GetMethod("Create").Node.Body?.ToString()
			?? string.Empty;
		await Assert.That(createBody).Contains("value = value?.Trim().ToUpperInvariant()!;");

		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harness = assembly.GetType("Testing.CurrencyHarness")!;

		await Assert.That((string)harness.GetMethod("CreateCanonical")!.Invoke(null, null)!).IsEqualTo("GBP");
	}

	[Test]
	public async Task Scalar_GivenAssemblyDefault_AppliesTrimToEveryStringScalar(CancellationToken cancellationToken)
	{
		const string source = """
			[assembly: Purview.ValueObjects.Serialization.ValueObjectDefaults(Trim = true)]

			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar<string>]
				public readonly partial record struct Name { }

				public static class NameHarness
				{
					public static string CreateTrimmed() => Name.Create("  Bob  ").Value;
				}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsGeneratorTestOptions.Default.Compile(), cancellationToken);

		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harness = assembly.GetType("Testing.NameHarness")!;

		await Assert.That((string)harness.GetMethod("CreateTrimmed")!.Invoke(null, null)!).IsEqualTo("Bob");
	}

	[Test]
	public async Task Scalar_GivenAssemblyDefaultTrim_TypeOverrideWins(CancellationToken cancellationToken)
	{
		const string source = """
			[assembly: Purview.ValueObjects.Serialization.ValueObjectDefaults(Trim = true)]

			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar<string>(Trim = false)]
				public readonly partial record struct Name { }

				public static class NameHarness
				{
					public static string CreatePreserves() => Name.Create("  Bob  ").Value;
				}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsGeneratorTestOptions.Default.Compile(), cancellationToken);

		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harness = assembly.GetType("Testing.NameHarness")!;

		await Assert.That((string)harness.GetMethod("CreatePreserves")!.Invoke(null, null)!).IsEqualTo("  Bob  ");
	}

	[Test]
	public async Task Complex_GivenMemberNormalize_NormalizesOnlyThatMember(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.ValueObject]
				public readonly partial record struct Contact
				{
					[Purview.ValueObjects.Serialization.StringNormalize(Trim = true, Casing = Purview.ValueObjects.Serialization.StringCasing.LowerInvariant)]
					public string Email { get; }

					public string Name { get; }

					private Contact(string email, string name)
					{
						Email = email;
						Name = name;
					}
				}

				public static class ContactHarness
				{
					public static string CreateNormalized()
					{
						var contact = Contact.Create("  Bob@Example.COM  ", "  Keep Me  ");
						return contact.Email + "|" + contact.Name;
					}
				}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsGeneratorTestOptions.Default.Compile(), cancellationToken);

		var createBody =
			result.Generated().GetRecord("Contact", "Testing").GetMethod("Create").Node.Body?.ToString()
			?? string.Empty;
		await Assert.That(createBody).Contains("email = email?.Trim().ToLowerInvariant()!;");
		await Assert.That(createBody).DoesNotContain("name = name?");

		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harness = assembly.GetType("Testing.ContactHarness")!;

		await Assert
			.That((string)harness.GetMethod("CreateNormalized")!.Invoke(null, null)!)
			.IsEqualTo("bob@example.com|  Keep Me  ");
	}

	[Test]
	public async Task Complex_GivenAssemblyDefault_AppliesToEveryStringMember(CancellationToken cancellationToken)
	{
		const string source = """
			[assembly: Purview.ValueObjects.Serialization.ValueObjectDefaults(Trim = true)]

			namespace Testing
			{
				[Purview.ValueObjects.Serialization.ValueObject]
				public readonly partial record struct Contact
				{
					public string Email { get; }

					public string Name { get; }

					private Contact(string email, string name)
					{
						Email = email;
						Name = name;
					}
				}

				public static class ContactHarness
				{
					public static string CreateTrimmed()
					{
						var contact = Contact.Create("  a@b.com  ", "  Bob  ");
						return contact.Email + "|" + contact.Name;
					}
				}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsGeneratorTestOptions.Default.Compile(), cancellationToken);

		var createBody =
			result.Generated().GetRecord("Contact", "Testing").GetMethod("Create").Node.Body?.ToString()
			?? string.Empty;
		await Assert.That(createBody).Contains("email = email?.Trim()!;");
		await Assert.That(createBody).Contains("name = name?.Trim()!;");

		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harness = assembly.GetType("Testing.ContactHarness")!;

		await Assert.That((string)harness.GetMethod("CreateTrimmed")!.Invoke(null, null)!).IsEqualTo("a@b.com|Bob");
	}

	[Test]
	public async Task Scalar_GivenAssemblyDefaultAndOnNormalize_HookWins(CancellationToken cancellationToken)
	{
		const string source = """
			[assembly: Purview.ValueObjects.Serialization.ValueObjectDefaults(Trim = true)]

			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar<string>]
				public readonly partial record struct Name
				{
					static partial void OnNormalize(ref string value) => value = value?.ToUpperInvariant()!;
				}

				public static class NameHarness
				{
					public static string CreateNormalized() => Name.Create("  Bob  ").Value;
				}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsGeneratorTestOptions.Default.Compile(), cancellationToken);

		var createBody =
			result.Generated().GetRecord("Name", "Testing").GetMethod("Create").Node.Body?.ToString() ?? string.Empty;
		await Assert.That(createBody).DoesNotContain("value = value?.Trim()");

		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harness = assembly.GetType("Testing.NameHarness")!;

		await Assert.That((string)harness.GetMethod("CreateNormalized")!.Invoke(null, null)!).IsEqualTo("  BOB  ");
	}

	[Test]
	public async Task Complex_GivenAssemblyDefaultAndOnNormalize_HookWins(CancellationToken cancellationToken)
	{
		const string source = """
			[assembly: Purview.ValueObjects.Serialization.ValueObjectDefaults(Trim = true)]

			namespace Testing
			{
				[Purview.ValueObjects.Serialization.ValueObject]
				public readonly partial record struct Contact
				{
					public string Email { get; }

					public string Name { get; }

					private Contact(string email, string name)
					{
						Email = email;
						Name = name;
					}

					static partial void OnNormalize(ref string email, ref string name) =>
						email = email?.ToUpperInvariant()!;
				}

				public static class ContactHarness
				{
					public static string CreateNormalized()
					{
						var contact = Contact.Create("  a@b.com  ", "  Bob  ");
						return contact.Email + "|" + contact.Name;
					}
				}
			}
			""";

		var result = await GenerateAsync(source, ValueObjectsGeneratorTestOptions.Default.Compile(), cancellationToken);

		var createBody =
			result.Generated().GetRecord("Contact", "Testing").GetMethod("Create").Node.Body?.ToString()
			?? string.Empty;
		await Assert.That(createBody).DoesNotContain("email = email?.Trim()");

		var assembly = await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		var harness = assembly.GetType("Testing.ContactHarness")!;

		await Assert
			.That((string)harness.GetMethod("CreateNormalized")!.Invoke(null, null)!)
			.IsEqualTo("  A@B.COM  |  Bob  ");
	}
}
