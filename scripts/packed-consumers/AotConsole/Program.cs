// Packed-consumer gate, consumer (a): a Native AOT console built from the packages alone. Every check records a
// failure rather than throwing, so one run reports every mismatch; any failure makes the process exit non-zero.
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using PackedValueObjects;
using ZodSharp;
using ZodSharp.Core;
using ZodSharp.Rules;

var failures = new List<string>();
var passed = 0;

void Check(bool condition, string description)
{
	if (condition)
	{
		passed++;
		Console.WriteLine($"  pass  {description}");
	}
	else
	{
		failures.Add(description);
		Console.WriteLine($"  FAIL  {description}");
	}
}

void CheckPaths(ImmutableArray<ValidationError> errors, string[] expected, string description)
{
	var actual = errors.IsDefault ? [] : errors.Select(static e => e.FullPath.ToDottedString()).Distinct().ToArray();
	var missing = expected.Except(actual).ToArray();
	var unexpected = actual.Except(expected).ToArray();
	Check(
		missing.Length == 0 && unexpected.Length == 0,
		missing.Length == 0 && unexpected.Length == 0
			? description
			: $"{description} (missing: [{string.Join(", ", missing)}], unexpected: [{string.Join(", ", unexpected)}])"
	);
}

void Section(string name, Action body)
{
	Console.WriteLine(name);
	try
	{
		body();
	}
	catch (Exception ex)
	{
		Check(false, $"{name} threw {ex.GetType().Name}: {ex.Message}");
	}
}

static T? Catch<T>(Action action)
	where T : Exception
{
	try
	{
		action();
		return null;
	}
	catch (T ex)
	{
		return ex;
	}
}

// The value objects are declared in this project, so the System.Text.Json source generator - which never sees
// another generator's output - cannot see the [JsonConverter] attribute the value object generator adds, and would
// describe each value object as an object with a Value property. Registering the generated converters on the
// options fixes that; a value object declared in a referenced library needs nothing, because the attribute is then
// compiled into it.
JsonSerializerOptions JsonOptions = new(ConsumerJsonContext.Default.Options)
{
	Converters =
	{
		new EmailAddress.EmailAddressJsonConverter(),
		new CustomerId.CustomerIdJsonConverter(),
		new Quantity.QuantityJsonConverter(),
		new Money.MoneyJsonConverter(),
	},
};

static Order ValidOrder() =>
	new()
	{
		Customer = CustomerId.Create(Guid.NewGuid()),
		ContactEmail = EmailAddress.Create("buyer@example.com"),
		Lines =
		[
			new()
			{
				Sku = "ABC-1234",
				Quantity = Quantity.Create(2),
				Price = Money.Create(9.99m, "GBP"),
			},
		],
	};

Section(
	"native image",
	() =>
		// A native image compiles nothing at run time; `dotnet run` of the same project would JIT.
		Check(System.Runtime.JitInfo.GetCompiledMethodCount() == 0, "the process is a Native AOT image (no JIT)")
);

Section(
	"generated scalar factories",
	() =>
	{
		var email = EmailAddress.Create("  Buyer@Example.COM ");
		Check(email.Value == "buyer@example.com", $"Create normalizes before validating (got '{email.Value}')");
		Check(
			EmailAddress.Hydrate("not-an-email").Value == "not-an-email",
			"Hydrate is replay-safe and does not validate"
		);
		Check(!EmailAddress.TryCreate("not-an-email", out _), "TryCreate returns false for a schema failure");
		Check(Quantity.Create(5).Value == 5, "a hook-validated scalar creates");
		Check(
			Catch<ArgumentOutOfRangeException>(() => Quantity.Create(0)) is not null,
			"a hook-validated scalar rejects an invalid value with ArgumentOutOfRangeException"
		);
		Check(Money.Create(1.5m, "EUR").Amount == 1.5m, "a complex value object creates");
		Check(
			Catch<ArgumentOutOfRangeException>(() => Money.Create(-1m, "EUR")) is not null,
			"a complex value object runs its OnValidate hook"
		);
		Check(EmailAddress.Create("a@b.co") == EmailAddress.Create("A@B.CO"), "equality is by normalized value");
	}
);

Section(
	"Create routes through the ZodSharp schema",
	() =>
	{
		var email = Catch<ZodException>(() => EmailAddress.Create("not-an-email"));
		Check(email is { Errors.Length: > 0 }, "EmailAddress.Create throws ZodException for an invalid address");
		if (email is not null)
			CheckPaths(email.Errors, ["Value"], "the scalar's member rule reports the Value path");

		var customer = Catch<ZodException>(() => CustomerId.Create(Guid.Empty));
		ValidationError? error = customer is null ? null : customer.Errors.Single();
		Check(
			error is { Code: NonSentinelRule<Guid>.ErrorCode, Message: "CustomerId must not be empty." },
			$"a type-level [NonSentinel] rejects Guid.Empty (got {error?.Code}: {error?.Message})"
		);
		Check(
			error?.Path.IsEmpty == true,
			"a type-level rule reports an empty path: the scalar is validated as a unit"
		);
		Check(
			error?.Origin == ZodTypeNames.String,
			$"ZodSharp 3 reports the rule's structured origin (got '{error?.Origin}')"
		);
	}
);

Section(
	"generated DTO validation over value objects",
	() =>
	{
		Check(OrderSchema.Validate(ValidOrder()).IsSuccess, "a valid order passes");

		Order invalid = new()
		{
			Customer = null,
			ContactEmail = EmailAddress.Hydrate("not-an-email"),
			Lines =
			[
				new()
				{
					Sku = "abc",
					Quantity = Quantity.Hydrate(1),
					Price = Money.Hydrate(1m, "GBP"),
				},
			],
		};
		var errors = OrderSchema.Validate(invalid).Errors;
		foreach (var e in errors)
			Console.WriteLine($"        {e.FullPath.ToDottedString()}: {e.Code} - {e.Message}");

		CheckPaths(
			errors,
			["Customer", "ContactEmail.Value", "Lines[0].Sku"],
			"a missing value object, a nested value object's own rule and a collection element report typed paths"
		);

		var empty = ValidOrder();
		empty.Lines = [];
		var tooFew = OrderSchema.Validate(empty).Errors.Single();
		Check(
			tooFew is { Code: "too_small", Minimum: 1 } && tooFew.FullPath.ToDottedString() == "Lines",
			$"[MinCount] reports too_small at the member (got {tooFew.Code} at {tooFew.FullPath.ToDottedString()})"
		);
	}
);

Section(
	"System.Text.Json with a source-generated context",
	() =>
	{
		var emailInfo = (JsonTypeInfo<EmailAddress>)JsonOptions.GetTypeInfo(typeof(EmailAddress));
		var moneyInfo = (JsonTypeInfo<Money>)JsonOptions.GetTypeInfo(typeof(Money));
		var orderInfo = (JsonTypeInfo<Order>)JsonOptions.GetTypeInfo(typeof(Order));

		var json = JsonSerializer.Serialize(EmailAddress.Create("demo@example.com"), emailInfo);
		Check(json == "\"demo@example.com\"", $"a scalar serializes as its underlying value (got {json})");

		var moneyJson = JsonSerializer.Serialize(Money.Create(12.5m, "USD"), moneyInfo);
		Check(
			moneyJson == """{"amount":12.5,"currency":"USD"}""",
			$"a complex value object writes its members (got {moneyJson})"
		);
		Check(
			JsonSerializer.Deserialize(moneyJson, moneyInfo) == Money.Create(12.5m, "USD"),
			"a complex value object round-trips"
		);

		var order = ValidOrder();
		var orderJson = JsonSerializer.Serialize(order, orderInfo);
		Console.WriteLine($"        {orderJson}");
		var roundTripped = JsonSerializer.Deserialize(orderJson, orderInfo);
		Check(
			roundTripped is not null
				&& roundTripped.ContactEmail == order.ContactEmail
				&& roundTripped.Customer == order.Customer
				&& roundTripped.Lines.Single().Quantity == Quantity.Create(2),
			"a DTO of value objects round-trips through the generated converters"
		);

		var validator = new OrderSchemaValidator();
		var invalid = validator.DeserializeAndValidate(
			"""{"customer":"3f2504e0-4f89-11d3-9a0c-0305e82c3301","contactEmail":"not-an-email","lines":[]}""",
			orderInfo
		);
		CheckPaths(
			invalid.Errors,
			["ContactEmail.Value", "Lines"],
			"DeserializeAndValidate validates the value objects it read"
		);
	}
);

Console.WriteLine();
if (failures.Count == 0)
{
	Console.WriteLine($"Packed Native AOT consumer passed: {passed} checks.");
	return 0;
}

Console.WriteLine($"Packed Native AOT consumer FAILED: {failures.Count} failure(s), {passed} passed.");
foreach (var failure in failures)
	Console.WriteLine($"  {failure}");

return 1;
