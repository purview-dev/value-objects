using System.Collections.Immutable;
using ZodSharp.Core;

Console.WriteLine("== Generated scalar validators ([ZodSchema]) ==");
GeneratedScalarValidators();

Console.WriteLine();
Console.WriteLine("== Generator-integrated validation (Create calls the schema) ==");
GeneratorIntegratedValidation();

Console.WriteLine();
Console.WriteLine("== Schema-first validation (hand-built schemas) ==");
SchemaFirstValidation();

Console.WriteLine();
Console.WriteLine("== Generated DTO validation ==");
DtoValidation();

Console.WriteLine();
Console.WriteLine("== DI / factory ==");
FactoryValidation();

static void GeneratedScalarValidators()
{
	var email = EmailAddress.Create("  Demo@Example.COM  ");
	var result = EmailAddressSchema.Validate(email);
	Console.WriteLine($"EmailAddressSchema.Validate('{email.Value}') -> {result.IsSuccess}");

	var invalid = EmailAddress.Hydrate("not-an-email");
	var invalidResult = EmailAddressSchema.Validate(invalid);
	Console.WriteLine(
		$"EmailAddressSchema.Validate('not-an-email') -> {invalidResult.IsSuccess}, "
			+ $"errors: {FormatErrors(invalidResult.Errors)}"
	);

	var parsed = EmailAddressSchema.Parse(email);
	Console.WriteLine($"EmailAddressSchema.Parse -> '{parsed.Value}'");

	var refined = EmailAddressSchema.ApplyRefine(
		email,
		static e => e.Domain == "example.com",
		"Only example.com addresses allowed"
	);
	Console.WriteLine($"EmailAddressSchema.ApplyRefine(example.com) -> {refined.IsSuccess}");

	var otherDomain = EmailAddress.Hydrate("demo@contoso.com");
	var refinedRejected = EmailAddressSchema.ApplyRefine(
		otherDomain,
		static e => e.Domain == "example.com",
		"Only example.com addresses allowed"
	);
	Console.WriteLine($"EmailAddressSchema.ApplyRefine(contoso.com) -> {refinedRejected.IsSuccess}");

	var currency = CurrencyCode.Create("usd");
	var currencyResult = CurrencyCodeSchema.Validate(currency);
	Console.WriteLine($"CurrencyCodeSchema.Validate('{currency.Value}') -> {currencyResult.IsSuccess}");
}

static void GeneratorIntegratedValidation()
{
	// The value-object generator detected [ZodSchema] on EmailAddress and wired
	// EmailAddressSchema.Validate(instance) into the generated Create. Invalid input now throws
	// a ZodException before the value object is returned.
	try
	{
		EmailAddress.Create("not-an-email");
		Console.WriteLine("EmailAddress.Create('not-an-email') -> no exception");
	}
	catch (ZodException ex)
	{
		Console.WriteLine($"EmailAddress.Create('not-an-email') -> {ex.GetType().Name}, {ex.Errors.Length} error(s)");
	}

	var email = EmailAddress.Create("  Demo@Example.COM  ");
	Console.WriteLine($"EmailAddress.Create('  Demo@Example.COM  ') -> '{email.Value}'");

	// ZodSchemaMode.InsteadOfHooks: the schema is the sole validation gate.
	try
	{
		PhoneNumber.Create("abc");
		Console.WriteLine("PhoneNumber.Create('abc') -> no exception");
	}
	catch (ZodException ex)
	{
		Console.WriteLine($"PhoneNumber.Create('abc') -> {ex.GetType().Name}, {ex.Errors.Length} error(s)");
	}

	var phone = PhoneNumber.Create("+15551234567");
	Console.WriteLine($"PhoneNumber.Create('+15551234567') -> '{phone.Value}'");
}

static void SchemaFirstValidation()
{
	// ZodSharp validates the raw value as-is; the value object's Create normalizes afterwards.
	var ok = ScalarSchemas.ValidateEmail("demo@example.com");
	Console.WriteLine($"ScalarSchemas.ValidateEmail -> {ok.IsSuccess}, '{ok.Value.Value}'");

	var bad = ScalarSchemas.ValidateEmail("not-an-email");
	Console.WriteLine($"ScalarSchemas.ValidateEmail(invalid) -> {bad.IsSuccess}, {FormatErrors(bad.Errors)}");

	var money = ScalarSchemas.ValidateMoney(19.99m, "USD");
	Console.WriteLine(
		$"ScalarSchemas.ValidateMoney -> {money.IsSuccess}, {money.Value.Amount} {money.Value.Currency.Value}"
	);

	var negative = ScalarSchemas.ValidateMoney(-5m, "USD");
	Console.WriteLine(
		$"ScalarSchemas.ValidateMoney(negative) -> {negative.IsSuccess}, {FormatErrors(negative.Errors)}"
	);
}

static void DtoValidation()
{
	RegistrationDto dto = new()
	{
		Name = "John Doe",
		Age = 30,
		Email = "john@example.com",
	};

	var result = RegistrationDtoSchema.Validate(dto);
	Console.WriteLine($"RegistrationDtoSchema.Validate -> {result.IsSuccess}");

	var adult = RegistrationDtoSchema.ApplyRefine(dto, static d => d.Age >= 18, "Must be an adult");
	Console.WriteLine($"RegistrationDtoSchema.ApplyRefine(adult) -> {adult.IsSuccess}");

	RegistrationDto invalid = new()
	{
		Name = "xavier",
		Age = 16,
		Email = "not-an-email",
	};
	var invalidResult = RegistrationDtoSchema.Validate(invalid);
	Console.WriteLine(
		$"RegistrationDtoSchema.Validate(invalid) -> {invalidResult.IsSuccess}, {FormatErrors(invalidResult.Errors)}"
	);

	// Map a validated DTO to value objects.
	var email = EmailAddress.Create(result.Value!.Email);
	var money = Money.Create(19.99m, CurrencyCode.Create("USD"));
	Console.WriteLine($"Mapped -> {email.Value}, {money.Amount} {money.Currency.Value}");
}

static void FactoryValidation()
{
	ZodSchemaFactory factory = new();

	// The source-generated validator adapter for the scalar value object.
	factory.Register(new EmailAddressSchemaValidator());

	// A hand-built schema wrapped as a validator.
	factory.Register(new ZodSchemaValidator<string>(ScalarSchemas.EmailSchema));

	var emailResult = factory.Validate(EmailAddress.Create("demo@example.com"));
	Console.WriteLine($"factory.Validate(EmailAddress) -> {emailResult.IsSuccess}");

	var stringResult = factory.Validate("demo@example.com");
	Console.WriteLine($"factory.Validate(string) -> {stringResult.IsSuccess}");
}

static string FormatErrors(ImmutableArray<ValidationError> errors) =>
	string.Join("; ", errors.Select(static e => $"{string.Join(".", e.Path)}: {e.Message}"));
