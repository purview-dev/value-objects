namespace Purview.ValueObjects.SourceGenerator.Common;

static partial class SourceGenLibrary
{
	public static GenerationSettings CreateGenerationSettings<TGenerator>(string? disablePropertyName = null) =>
		GenerationSettings.Create<TGenerator>(disablePropertyName) with
		{
			DefaultMethodAccessibility = null,
		};
}
