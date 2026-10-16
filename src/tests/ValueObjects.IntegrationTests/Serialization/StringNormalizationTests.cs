namespace Purview.ValueObjects.Serialization;

/// <summary>
/// Exercises the built-in string normalization options end-to-end against the real runtime package: a
/// scalar configured with <c>Trim</c>/<c>Casing</c>, and a complex member annotated with
/// <c>[StringNormalize]</c>.
/// </summary>
public sealed class StringNormalizationTests
{
	[Test]
	public async Task Scalar_CreateNormalizes_HydratePreserves()
	{
		var created = NormalizedEmail.Create("  Demo@Example.COM  ");
		var hydrated = NormalizedEmail.Hydrate("  Demo@Example.COM  ");

		await Assert.That(created.Value).IsEqualTo("demo@example.com");
		await Assert.That(hydrated.Value).IsEqualTo("  Demo@Example.COM  ");
	}

	[Test]
	public async Task Scalar_TryCreateNormalizes()
	{
		var ok = NormalizedEmail.TryCreate("  Demo@Example.COM  ", out var email);

		await Assert.That(ok).IsTrue();
		await Assert.That(email.Value).IsEqualTo("demo@example.com");
	}

	[Test]
	public async Task Complex_CreateNormalizesOnlyAnnotatedMember()
	{
		var contact = NormalizedContact.Create("  Demo@Example.COM  ", "  Keep Me  ");

		await Assert.That(contact.Email).IsEqualTo("demo@example.com");
		await Assert.That(contact.Name).IsEqualTo("  Keep Me  ");
	}
}

[Scalar<string>(Trim = true, Casing = StringCasing.LowerInvariant)]
public readonly partial record struct NormalizedEmail { }

[ValueObject]
public readonly partial record struct NormalizedContact
{
	[StringNormalize(Trim = true, Casing = StringCasing.LowerInvariant)]
	public string Email { get; }

	public string Name { get; }
}
