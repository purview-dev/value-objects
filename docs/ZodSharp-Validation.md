# Validating Value Objects with ZodSharp

[Purview.ZodSharp](https://www.nuget.org/packages/Purview.ZodSharp) is a high-performance C# port of the
[Zod](https://github.com/colinhacks/zod) schema validation library. It complements `Purview.ValueObjects`:
the value object owns the invariants, ZodSharp owns the rule definitions and validation results.

Three patterns are covered here, demonstrated in the `src/src/ZodSharpSample` project:

1. **Generator-integrated validation** — a value object annotated with both `[Scalar]`/`[ValueObject]`
   and `[ZodSchema]` has its generated `Create` wired to the ZodSharp-generated schema.
2. **Generated validators** — annotate a value object or DTO with `[ZodSchema]` and DataAnnotations; a
   source generator emits a zero-allocation `{Type}Schema` validator.
3. **Schema-first validation** — build a schema for the scalar's underlying value with `Z.String()`,
   `Z.Number()`, `Z.Enum()`, then construct the value object through its strict `Create` factory.

## Install

```text
dotnet add package Purview.ZodSharp
```

## 1. Generated validators on value objects

Mark a `[Scalar]` value object with `[ZodSchema]` and add DataAnnotations to its underlying value. The
generator produces a static `{Type}Schema` class plus a `{Type}SchemaValidator` adapter.

```csharp
using System.ComponentModel.DataAnnotations;
using Purview.ValueObjects.Serialization;
using ZodSharp;

[Scalar]
[ZodSchema]
public readonly partial record struct EmailAddress
{
    [EmailAddress]
    [StringLength(254, MinimumLength = 3)]
    public string Value { get; }

    static partial void OnNormalize(ref string value) => value = value?.Trim().ToLowerInvariant()!;

    static partial void OnValidate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Email is required.", nameof(value));
    }
}
```

Validate the value object directly:

```csharp
var email = EmailAddress.Create("demo@example.com");

var result = EmailAddressSchema.Validate(email);      // ValidationResult<EmailAddress>
if (result.IsSuccess)
    Console.WriteLine(result.Value);                   // demo@example.com

var parsed = EmailAddressSchema.Parse(email);          // throws ZodException on failure

// Compose additional rules:
var allowed = EmailAddressSchema.ApplyRefine(
    email,
    static e => e.Domain == "example.com",
    "Only example.com addresses allowed");
```

`[ZodSchema]` supports classes, structs, and records, and reads DataAnnotations such as `[Required]`,
`[StringLength]`, `[Range]`, `[RegularExpression]`, `[EmailAddress]`, `[AllowedValues]`, and
`[DeniedValues]`.

## 2. Generator-integrated validation

When a value object is annotated with **both** `[Scalar]`/`[ValueObject]` **and** `[ZodSchema]`, the
value-object generator detects it and routes the generated `Create(...)` through the ZodSharp-generated
schema — no manual schema wiring needed:

```csharp
[Scalar]
[ZodSchema]
public readonly partial record struct EmailAddress
{
    [EmailAddress]
    public string Value { get; }
    // ...
}

EmailAddress.Create("not-an-email");   // throws ZodException via EmailAddressSchema.Validate
```

The generated `Create` constructs the instance, calls `EmailAddressSchema.Validate(instance)`, and
throws a `ZodException` when validation fails. `Hydrate(...)` remains replay-safe (no re-validation),
and `ValueObjectDeserializationMode.Strict` (which deserializes through `Create`) picks up the schema
validation automatically.

`ZodSchemaMode` on `[Scalar]`/`[ValueObject]` controls how the schema and the hand-written hooks
combine:

- `ZodSchemaMode.InAdditionToHooks` (default) — the schema runs **and** the `OnValidate` hook runs.
- `ZodSchemaMode.InsteadOfHooks` — the schema runs **instead of** the `OnValidate` hook. `OnNormalize`
  still runs so input is canonicalized first.

```csharp
[Scalar(ZodSchemaMode = ZodSchemaMode.InsteadOfHooks)]
[ZodSchema]
public readonly partial record struct PhoneNumber
{
    [RegularExpression(@"^\+?\d{7,15}$")]
    public string Value { get; }
}
```

The `[ZodSchema]` attribute also exposes generator options that tune the emitted schema:

- `RefinementMethodName` — names a synchronous instance refinement method (default `Validate`) that the
  generator runs after the DataAnnotations rules.
- `CustomValidationMethodName` — names a static async method that the generated validator's
  `ValidateAsync` awaits after the synchronous rules pass (default `CustomValidationAsync`).
- `GenerateParseMethod` / `GenerateValidateMethod` / `EnableComposition` — toggle the emitted `Parse`,
  `Validate`, and composition (`ApplyAnd`/`ApplyOr`/`ApplyRefine`) members.

> Note: `SchemaName` on `[ZodSchema]` is reserved by the attribute today but is not yet applied by the
> ZodSharp generator — the generated schema class is always named `{TypeName}Schema`. Use the default
> name when combining `[Scalar]`/`[ValueObject]` with `[ZodSchema]`.

## 3. Schema-first validation

When you do not want the generator involved, build a schema for the scalar's underlying value and
map a successful result onto the value object:

```csharp
using ZodSharp;

public static class ScalarSchemas
{
    public static readonly IZodSchema<string, string> EmailSchema =
        Z.String().Email().Min(3).Max(254);

    public static readonly IZodSchema<string, string> CurrencySchema =
        Z.String().Regex("^[A-Z]{3}$");

    public static readonly IZodSchema<OrderStatusKind, OrderStatusKind> OrderStatusSchema =
        Z.Enum<OrderStatusKind>();

    public static readonly IZodSchema<double, double> MoneyAmountSchema =
        Z.Number().Positive();

    public static ValidationResult<EmailAddress> ValidateEmail(string value) =>
        Map(EmailSchema.Validate(value), EmailAddress.Create);

    static ValidationResult<TTarget> Map<TSource, TTarget>(
        ValidationResult<TSource> result,
        Func<TSource, TTarget> construct) =>
        result.IsSuccess
            ? ValidationResult<TTarget>.Success(construct(result.Value!))
            : ValidationResult<TTarget>.Failure(result.Errors);
}
```

Note that ZodSharp validates the raw value exactly as supplied — normalization (trimming, casing) is
the value object's job in `OnNormalize`. Validate the raw input, then construct with `Create` so the
value object normalizes and wraps it.

## 4. Validating DTOs before mapping to value objects

Annotate a request/DTO class with `[ZodSchema]`, validate it, then map the validated values onto
value objects:

```csharp
[ZodSchema(RefinementMethodName = nameof(ValidateRegistration))]
public sealed class RegistrationDto
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;

    [Range(13, 120)]
    public int Age { get; init; }

    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    // Custom sync refinement, discovered via the RefinementMethodName option. The generator runs
    // these errors after the DataAnnotations rules.
    public IEnumerable<ValidationError> ValidateRegistration()
    {
        if (Name.StartsWith("x", StringComparison.OrdinalIgnoreCase))
            yield return new ValidationError("name", "Name cannot start with 'x'.", [nameof(Name)]);
    }
}

var result = RegistrationDtoSchema.Validate(dto);

if (result.IsSuccess)
{
    var email = EmailAddress.Create(result.Value.Email);
    var currency = CurrencyCode.Create("USD");
    var money = Money.Create(19.99m, currency);
}
```

### Async custom validation

`CustomValidationMethodName` names a static async method with the signature
`static ValueTask<ValidationResult<T>> Method(T value, CancellationToken cancellationToken)`. The
generated `{Type}SchemaValidator` (which implements `IZodSchemaValidator<T>`) awaits it in its
`ValidateAsync` after the synchronous rules pass:

```csharp
[ZodSchema(CustomValidationMethodName = nameof(ValidatePromoCodeAsync))]
public sealed class PromoCode
{
    [Required, RegularExpression(@"^[A-Z0-9]{4,10}$")]
    public string Code { get; init; } = string.Empty;

    internal static ValueTask<ValidationResult<PromoCode>> ValidatePromoCodeAsync(
        PromoCode value, CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            value.Code is "SAVE10" or "WELCOME20"
                ? ValidationResult<PromoCode>.Success(value)
                : ValidationResult<PromoCode>.Failure(
                    new ValidationError("code", "Unknown promotional code.", [nameof(Code)]))
        );
}

PromoCodeSchemaValidator validator = new();
var result = await validator.ValidateAsync(new PromoCode { Code = "HOMERUN42" });
```

## 5. Dependency injection and the schema factory

`ZodSchemaFactory` resolves validators by validated type. Register the generated adapter or wrap a
hand-built schema with `ZodSchemaValidator<T>`:

```csharp
using ZodSharp.Core;

ZodSchemaFactory factory = new();

factory.Register(new EmailAddressSchemaValidator());                 // generated adapter
factory.Register(new ZodSchemaValidator<string>(ScalarSchemas.EmailSchema)); // hand-built

var emailResult = factory.Validate(EmailAddress.Create("demo@example.com"));
var stringResult = factory.Validate("demo@example.com");
```

## Error handling

`ValidationResult<T>` is a struct with `IsSuccess`, `Value` (only when successful), and `Errors`
(`ImmutableArray<ValidationError>`). Each `ValidationError` has a `Path` and a `Message`:

```csharp
foreach (var error in result.Errors)
    Console.WriteLine($"{string.Join(".", error.Path)}: {error.Message}");
```

Use `Parse` / `GetValueOrThrow()` to throw a `ZodException` on failure instead of inspecting the
result.

### ASP.NET Core Problem Details

In ASP.NET Core, `Purview.ZodSharp.AspNetCore` maps thrown `ZodException`s to standard
`HttpValidationProblemDetails` responses. This covers strict deserialization of value objects
(`ValueObjectDeserializationMode.Strict`) and any `Create`/`Parse` failure that bubbles up as a
`ZodException`.

Wire the handler into the pipeline:

```csharp
builder.Services.AddZodSharpProblemDetails();
builder.Services.AddProblemDetails();

var app = builder.Build();
app.UseExceptionHandler();
```

Mark the value object for strict deserialization and ZodSharp validation so invalid request bodies throw
during model binding:

```csharp
[Scalar(
    ZodSchemaMode = ZodSchemaMode.InsteadOfHooks,
    DeserializationMode = ValueObjectDeserializationMode.Strict)]
[ZodSchema]
public readonly partial record struct EmailAddress
{
    [Required, EmailAddress]
    public string Value { get; }
}

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new ScalarJsonConverterFactory()));
```

A `POST` body with an invalid email now returns `400 application/problem+json` with the structured issues
in the `issues` extension.

Map error codes to HTTP statuses and formatted messages with `ErrorType` + `ErrorTypeRegistry`. Mark a
static partial class with `[ErrorType]` on a `static readonly ErrorType` field and the bundled
`ErrorTypeGenerator` emits `Create{Field}(...)` (builds a `ValidationError`) and `Throw{Field}(...)`
(a `void` + `[DoesNotReturn]` method that throws the `ZodException`):

```csharp
[ErrorType]
public static readonly ErrorType SaveFailed = new(
    Code: "aggregate_save_failed",
    Description: "The order could not be saved because it was modified concurrently.",
    HttpStatus: StatusCodes.Status409Conflict,
    MessageFormat: "Order '{OrderId}' (of type {AggregateType}) failed to save")
{
    Parameters = ["OrderId", "AggregateType"]
};

ErrorTypeRegistry.Default.Register(ErrorTypes.SaveFailed);
```

The generated `ThrowSaveFailed(orderId, aggregateType)` throws a `ZodException` carrying the
`aggregate_save_failed` code, yielding a `409 Conflict` whose message is formatted from the error's
parameters. Because it is `void` + `[DoesNotReturn]`, use it as a terminal call — for example a `void`
minimal-API handler that always throws (the endpoint returns the mapped `409` via the exception
handler):

```csharp
app.MapPost("/orders/{orderId}/confirm", ConfirmOrder);

static void ConfirmOrder(string orderId) => ErrorTypes.ThrowSaveFailed(orderId, "Order");
```

(If you prefer not to use the generator, construct the `ZodException` manually with
`ValidationError.Create(code, message, path: [], parameters: ...)` — it maps the same way.)

The bundled `ZODSASP001` analyzer flags `MessageFormat` placeholders missing from `Parameters` at
compile time. See the
[ASP.NET Core integration](https://purview.dev/docs/zodsharp/aspnetcore-integration/) guide and the
`src/src/ZodSharp.AspNetCoreSample` project.

## JSON Schema export

Export a schema to JSON Schema (Draft 2020-12) for cross-platform sharing with TypeScript Zod:

```csharp
var jsonSchema = Z.ToJsonSchema(ScalarSchemas.EmailSchema, new ToJsonSchemaOptions { Title = "Email" });
```

## Direct-reference note

When a project uses `Purview.ZodSharp` types directly (as this sample does), reference the package
explicitly — do not rely on transitive flow. The ZodSharp source generator is active in any project
that references the package, so `[ZodSchema]` is available there.

## Testing the dual-generator integration

Tests that run the value-object generator and the ZodSharp generator together come in two shapes:

- **In-memory (unit):** `src/tests/SourceGenerator.UnitTests` registers the packaged ZodSharp generator
  through `ZodSchemaValidationGeneratorTestOptions`, which resolves the component types out of band via
  `Common/ZodSharpSourceGenerators.cs`. The project copies
  `analyzers/dotnet/cs/Purview.ZodSharp.SourceGenerators.dll` beside the test binaries
  (`GeneratePathProperty` + `None`/`CopyToOutputDirectory`) and loads it with `Assembly.LoadFrom`.
  Never turn that copy into a `<Reference>`: a merged analyzer component used to carry
  `Purview.SourceGeneratorFramework.*` types that then collide (`CS0433`) with the framework assembly
  the test harness loads. `Common/ZodSharpSourceGeneratorsTests.cs` guards the invariant.
- **Real compile (integration):** `src/tests/ValueObjects.IntegrationTests` declares `[Scalar]` +
  `[ZodSchema]` fixtures and asserts runtime behaviour directly — both generators run in the real
  compiler for that project, so nothing has to be reflected or registered.

The loaded generator carries its own framework implementation, so it keeps its own log sink and
CodeWriter scope validation: do not assert on its log entries, and leave `ValidateCodeWriterScopes`
off for that run.

## See also

- The runnable `src/src/ZodSharpSample` project.
- [Getting Started](Getting-Started.md)
- [Value Object Design](Value-Object-Design.md)