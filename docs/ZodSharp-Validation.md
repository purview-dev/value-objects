# Validating Value Objects with ZodSharp

[Purview.ZodSharp](https://www.nuget.org/packages/Purview.ZodSharp) is a high-performance C# port of the
[Zod](https://github.com/colinhacks/zod) schema validation library. It complements `Purview.ValueObjects`:
the value object owns the invariants, ZodSharp owns the rule definitions and validation results.

Three patterns are covered here, demonstrated in the `samples/` folder:

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

A custom schema class name (from ZodSharp's `[ZodSchema(SchemaName = "...")]`) is honored.

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
[ZodSchema]
public sealed class RegistrationDto
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;

    [Range(13, 120)]
    public int Age { get; init; }

    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    // Custom sync refinement: the generator runs these errors after the DataAnnotations rules.
    public IEnumerable<ValidationError> Validate()
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

Map error codes to HTTP statuses and formatted messages with `ErrorType` + `ErrorTypeRegistry`:

```csharp
public static class ConcurrentErrorType
{
    public static readonly ErrorType SaveFailed = new(
        Code: "aggregate_save_failed",
        Description: "The order could not be saved because it was modified concurrently.",
        HttpStatus: StatusCodes.Status409Conflict,
        MessageFormat: "Order '{OrderId}' (of type {AggregateType}) failed to save")
    {
        Parameters = ["OrderId", "AggregateType"]
    };
}

ErrorTypeRegistry.Default.Register(ConcurrentErrorType.SaveFailed);
```

Throwing a `ZodException` with that code and parameters yields a `409 Conflict` whose message is
formatted from the error's parameters:

```csharp
throw new ZodException([
    ValidationError.Create(
        "aggregate_save_failed",
        "The order could not be saved.",
        path: [],
        parameters: new Dictionary<string, object?>
        {
            ["OrderId"] = orderId,
            ["AggregateType"] = "Order",
        }),
]);
```

The bundled `ZODSASP001` analyzer flags `MessageFormat` placeholders missing from `Parameters` at
compile time. See the
[ASP.NET Core integration](https://purview.dev/docs/zodsharp/aspnetcore-integration/) guide and the
`src/ZodSharp.AspNetCoreSample` project.

## JSON Schema export

Export a schema to JSON Schema (Draft 2020-12) for cross-platform sharing with TypeScript Zod:

```csharp
var jsonSchema = Z.ToJsonSchema(ScalarSchemas.EmailSchema, new ToJsonSchemaOptions { Title = "Email" });
```

## Direct-reference note

When a project uses `Purview.ZodSharp` types directly (as this sample does), reference the package
explicitly — do not rely on transitive flow. The ZodSharp source generator is active in any project
that references the package, so `[ZodSchema]` is available there.

## See also

- The runnable `samples/ValueObjects.ZodSharpSample` project.
- [Getting Started](Getting-Started.md)
- [Value Object Design](Value-Object-Design.md)