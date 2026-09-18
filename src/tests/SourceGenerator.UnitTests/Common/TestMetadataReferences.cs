using Microsoft.CodeAnalysis;

namespace Purview.ValueObjects.SourceGenerator.Common;

public static class TestMetadataReferences
{
	public static IReadOnlyList<MetadataReference> GetAdditionalReferences()
	{
		List<MetadataReference> references =
		[
			MetadataReference.CreateFromFile(typeof(System.Text.Json.JsonSerializer).Assembly.Location),
			MetadataReference.CreateFromFile(
				typeof(System.ComponentModel.DataAnnotations.RequiredAttribute).Assembly.Location
			),
		];

		return references;
	}
}
