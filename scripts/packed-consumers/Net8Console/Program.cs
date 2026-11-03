// Packed-consumer gate, consumer (d): the net8.0 target of the runtime package, with the trim and AOT analyzers on
// (IsAotCompatible), so the generated code is analyzed for that target too.
using System.Text.Json;
using System.Text.Json.Serialization;
using Purview.ValueObjects.Serialization;

var failures = 0;
void Check(bool condition, string description)
{
	Console.WriteLine($"  {(condition ? "pass" : "FAIL")}  {description}");
	if (!condition)
		failures++;
}

Console.WriteLine($"net8.0 consumer on {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
Check(Environment.Version.Major == 8, "running on the .NET 8 runtime");

var sku = Sku.Create("  abc-1234 ");
Check(sku.Value == "ABC-1234", $"Create normalizes (got '{sku.Value}')");
Check(!Sku.TryCreate("nope", out _), "TryCreate rejects an invalid value");

JsonSerializerOptions options = new(Net8JsonContext.Default.Options)
{
	Converters = { new Sku.SkuJsonConverter(), new Price.PriceJsonConverter() },
};
var price = Price.Create(9.5m, sku);
var json = JsonSerializer.Serialize(price, options.GetTypeInfo(typeof(Price)));
Check(json == """{"amount":9.5,"sku":"ABC-1234"}""", $"a complex value object serializes its members (got {json})");
Check(
	JsonSerializer.Deserialize(json, options.GetTypeInfo(typeof(Price))) is Price roundTripped && roundTripped == price,
	"and round-trips"
);

return failures == 0 ? 0 : 1;

[Scalar]
public readonly partial record struct Sku
{
	public string Value { get; }

	static partial void OnNormalize(ref string value) => value = value?.Trim().ToUpperInvariant()!;

	static partial void OnValidate(string value)
	{
		if (value is not { Length: 8 })
			throw new ArgumentException("A SKU is eight characters.", nameof(value));
	}
}

[ValueObject]
public readonly partial record struct Price
{
	public decimal Amount { get; }

	public Sku Sku { get; }
}

[JsonSerializable(typeof(Price))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
partial class Net8JsonContext : JsonSerializerContext;
