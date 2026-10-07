# Validating Value Objects with ZodSharp

[Purview.ZodSharp](https://www.nuget.org/packages/Purview.ZodSharp) is a high-performance C# port of the
[Zod](https://github.com/colinhacks/zod) schema validation library. It complements `Purview.ValueObjects`:
the value object owns the invariants, ZodSharp owns the rule definitions and validation results.

The patterns below are demonstrated in the `src/src/ZodSharpSample` project:

1. **Generator-integrated validation** — a value object annotated with both `[Scalar]`/`[ValueObject]`
   and `[ZodSchema]` has its generated `Create` wired to the ZodSharp-generated schema.
2. **Generated validators** — annotate a value object or DTO with `[ZodSchema]` and DataAnnotations; a
   source generator emits a zero-allocation `{Type}Schema` validator.
3. **Schema-first validation** — build a schema for the scalar's underlying value with `Z.String()`,
   `Z.Number()`, `Z.Enum()`, then construct the value object through its strict `Create` factory.

See [Custom rules on scalars](#custom-rules-on-scalars) for type-level rules that validate a scalar as a unit,
and [Reusing a normal rule for a scalar](#reusing-a-normal-rule-for-a-scalar) for adapting a rule written
against the underlying value.

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

- `SchemaName` — overrides the generated schema class name (default `{TypeName}Schema`); the ZodSharp DI
  adapter becomes `{SchemaName}Validator`. The value object generator resolves the same name for its
  generated `Create`, so a custom name works — as long as it is a valid C# identifier. ZodSharp applies
  any non-empty value verbatim (including whitespace), so an unusable name is reported as `VO1015`
  rather than silently falling back to the default.
- `CustomValidationMethodName` — names a static async method that the generated validator's
  `ValidateAsync` awaits after the synchronous rules pass (default `CustomValidationAsync`).
- Synchronous refinements are written as the generator-declared `OnZodValidate` hook rather than a named
  method; see [Zod-compatible refinement hooks on value objects](#zod-compatible-refinement-hooks-on-value-objects).

### Zod-compatible refinement hooks on value objects

A value object annotated with `[ZodSchema]` gets the ZodSharp generator's optional partial hook for rules that
DataAnnotations cannot express (cross-member invariants, allowed domains, state checks). Implement
`OnZodValidate(RefineCtx<T>)` and add issues with the ZodSharp context:

```csharp
[Scalar]
[ZodSchema]
public readonly partial record struct CorporateEmail
{
    [EmailAddress]
    public string Value { get; }

    static partial void OnNormalize(ref string value) => value = value?.Trim().ToLowerInvariant()!;

    partial void OnZodValidate(RefineCtx<CorporateEmail> context)
    {
        if (!context.Value.Value.EndsWith("@contoso.com", StringComparison.Ordinal))
            context.AddIssue("invalid_domain", "Corporate emails must use the contoso.com domain.", [nameof(Value)]);
    }
}
```

```csharp
CorporateEmail.Create("demo@gmail.com");   // throws ZodException carrying 'invalid_domain'
CorporateEmail.Hydrate("demo@gmail.com");  // replay-safe: no validation runs
```

- The hook is **declared and invoked by the ZodSharp generator** inside `{Type}Schema.Validate`, so it runs for
  every schema entry point: `Validate`, `Parse`, the DI adapter, `IValidateOptions`, the value object's
  generated `Create`, and `ValueObjectDeserializationMode.Strict` (which deserializes through `Create`). The
  default `Hydrate` mode never validates.
- Because the hook belongs to the schema, the value-object generator neither declares nor invokes it. A value
  object therefore observes refinements through exactly the same path as any other `[ZodSchema]` consumer, and
  `{Type}Schema.Validate(instance)` reports the same issues as `Create`.
- The target type (and every containing type) must be declared `partial` so the ZodSharp generator can declare
  the hook on it. ZodSharp reports `ZODSGEN034` (not `partial`) and `ZODSGEN035` (malformed signature).
- Refinements are no longer written as an `IEnumerable<ValidationError> Validate()` method on the value object.
  That contract is retired; ZodSharp reports `ZODSGEN036` if a member still uses it.
- The hook is independent of `ZodSchemaMode`: `InsteadOfHooks` only skips the value object's own `OnValidate`
  hook, never the Zod refinement hook.

### Custom rules on scalars

ZodSharp rules are first-class, and a scalar value object is validated as a **unit** (its single `Value` *is*
the value). Purview.ZodSharp ships a validation attribute for every built-in rule in the `ZodSharp.Rules`
namespace, so the common checks need no hand-written rule or attribute:

| Attribute | Rule | Notes |
| --- | --- | --- |
| `[NonSentinel(Message = "…")]` | `NonSentinelRule<T>` | Rejects `Guid.Empty`, the `DateTime`/`DateTimeOffset`/`DateOnly`/`TimeOnly` bounds, and null/empty/whitespace strings. |
| `[Email]` | `EmailRule` | |
| `[E164]` | `E164Rule` | |
| `[UUID(UuidVersion.V4)]` | `UUIDRule` | A rule value without a default becomes a required attribute constructor argument. |
| `[MinLengthZod(3)]` | `MinLengthRule` | The `Zod` suffix avoids a clash with `System.ComponentModel.DataAnnotations.MinLengthAttribute` (also `[MaxLengthZod]`, `[UrlZod]`, `[PhoneZod]`, `[CreditCardZod]`, `[Base64StringZod]`). |

Each attribute mirrors its rule's constructor: a rule value without a default (the `minLength` of
`MinLengthRule`, the bound of `MinValueRule<T>`, …) is a **required constructor argument**, while an optional
value keeps its default. The rule's `message` and `code` stay named properties (`[NonSentinel(Message = "…",
Code = "…")]`), so an error code or message can be overridden per use.

The same attributes work on a primitive member and on a `[Scalar]` type. On a scalar, an attribute whose rule
is written against the underlying value is **adapted automatically** (see
[Reusing a normal rule for a scalar](#reusing-a-normal-rule-for-a-scalar)); on a member it validates the member
directly.

```csharp
using ZodSharp;
using ZodSharp.Rules;

[Scalar]
[ZodSchema]
[NonSentinel(Message = "AssetId must not be empty.")]
public readonly partial record struct AssetId
{
    public Guid Value { get; init; }
}
```

`AssetId.Create(Guid.Empty)` throws a `ZodException` whose error carries `Code = "invalid_value"` (the rule's
own `ErrorCode`), no structured `Origin`, and an **empty path** (the rule applies to the value object, not a
member). Because the rule runs inside the generated schema, `Hydrate` stays replay-safe and the value-objects
layer needs no extra code.

#### Writing a custom rule attribute

A custom rule is exposed as a `[ZodRule]`-mapped attribute in one of two ways.

**1. Map a hand-authored attribute to the rule.** Declare a `ValidationAttribute` and point it at the rule with
`[ZodRule(typeof(...))]`; the open generic form serves a primitive member and a scalar:

```csharp
using ZodSharp.Core;

// Reads the value object through IScalarValueObject<TSelf, TValue>. Implementing IZodRule lets the rule own
// its error identity, so one attribute can report a different code per scalar.
public readonly record struct NoWhitespaceRule<TSelf>(string? Message = null)
    : IValidationRule<TSelf>, IZodRule
    where TSelf : IScalarValueObject<TSelf, string>
{
    public const string ErrorCode = "invalid_string";
    public const string MessageFormat = "Value must not contain whitespace.";

    public bool IsValid(in TSelf value) => !value.Value.Any(char.IsWhiteSpace);

    public string GetErrorMessage(in TSelf value) => Message ?? MessageFormat;

    string? IZodRule.Code => ErrorCode;

    string? IZodRule.Origin => "value_object";
}

[ZodRule(typeof(NoWhitespaceRule<>))]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property)]
public sealed class NoWhitespaceAttribute : ValidationAttribute
{
    public string? Message { get; set; }
}
```

**2. Let the generator write the attribute.** Mark the rule with the parameterless `[ZodRule]` marker and the
ZodSharp generator emits a `{RuleName}Attribute` in the rule's namespace (a trailing `Rule` becomes
`Attribute`; a name that clashes with a `System.ComponentModel.DataAnnotations` attribute gets a `Zod`
suffix):

```csharp
[ZodRule] // the generator emits NoWhitespaceAttribute
public readonly record struct NoWhitespaceRule<TSelf>(string? Message = null)
    : IValidationRule<TSelf>, IZodRule
    where TSelf : IScalarValueObject<TSelf, string>
{
    // ...
}
```

The generator runs over the compilation that declares the rule, so the marker form suits a rule in the same
project (or in a rules library that references Purview.ZodSharp). A rule that accepts a `code`/`origin`
constructor parameter must implement `IZodRule`, or ZodSharp reports `ZODSGEN039`. A rule should also expose
`public const string ErrorCode` and `public const string MessageFormat`; ZodSharp reports `ZODSGEN042`
otherwise.

The reported `Code`/`Origin` resolve in this order (first match wins):

1. **Rule-owned** — the rule implements `IZodRule` (`IZodRule.Code`/`IZodRule.Origin`).
2. **Attribute-declared** — `Code`/`Origin` named arguments on the applied attribute.
3. **Mapping** — `[ZodRule(typeof(X), Code = "…", Origin = "…")]` on the attribute type.
4. **Default** — `validation_failed`, no origin.

The value-object layer never reconstructs codes or messages: it calls `{Type}Schema.Validate(instance)` and
forwards `result.Errors` (`ImmutableArray<ValidationError>`) into a `ZodException`, so a rule's `Code`,
`Message`, `Origin`, `Category`, and path arrive intact.

#### Reusing a normal rule for a scalar

A rule written against the underlying value (for example `NonSentinelRule<Guid>`) validates that value, not
the value object: closing it with the scalar type (`NonSentinelRule<AssetId>`) compiles but always passes,
because the sentinel check only knows the primitive.

The value-object generator emits `ScalarRuleAdapter<TSelf, TValue, TRule>` into every project that references
both `Purview.ValueObjects` and `Purview.ZodSharp`. It turns such a rule into one that validates the value
object as a unit:

```csharp
internal readonly record struct ScalarRuleAdapter<TSelf, TValue, TRule>(TRule Rule) : IValidationRule<TSelf>
    where TSelf : IScalarValueObject<TSelf, TValue>
    where TRule : IValidationRule<TValue>
{
    public bool IsValid(in TSelf value) => Rule.IsValid(value.Value);

    public string GetErrorMessage(in TSelf value) => Rule.GetErrorMessage(value.Value);
}
```

Do not declare it yourself. A project that already declares its own copy (the guidance before the generator
emitted it) is detected and the generated copy is skipped, so both keep compiling.

ZodSharp applies the adapter automatically when a rule is applied to a `[Scalar]` type, so the common case
needs no extra code. The built-in `[NonSentinel]` is exactly this shape — its rule is written against the
underlying value and the generator adapts it:

```csharp
using ZodSharp;
using ZodSharp.Rules;

[Scalar]
[ZodSchema]
[NonSentinel(Message = "UserId must not be empty.")]
public readonly partial record struct UserId
{
    public Guid Value { get; init; }
}
```

The generator closes `NonSentinelRule<>` with the underlying `Guid` and wraps it in
`ScalarRuleAdapter<UserId, Guid, NonSentinelRule<Guid>>`, so the rule runs against the value object with an
empty path and the **wrapped** rule owns the reported `Code` (the built-in rules report no structured
`Origin`). One attribute serves every scalar backed by the same primitive. The runnable version lives in
`src/src/ZodSharpSample/NonSentinelModels.cs`. See ZodSharp's
[Custom Rules](https://purview.dev/docs/zodsharp/custom-rules/) for the rule contract and the shipped
attributes.

### ZodSharp integration diagnostics

The value-object analyzer reports the integration states that would otherwise pass silently:

| Rule | Severity | Reported when |
| --- | --- | --- |
| `VO1013` | Warning | `OnValidate` is implemented while `ZodSchemaMode.InsteadOfHooks` is set, making that implementation unreachable in the generated `Create`. |
| `VO1015` | Error | `[ZodSchema(SchemaName = "...")]` is not a valid C# identifier, which ZodSharp applies verbatim and this generator cannot reference. Generation is skipped for that type. |

ZodSharp's own diagnostics cover the refinement hook (`ZODSGEN034`-`ZODSGEN036`) and the rule mapping and
convention checks (`ZODSGEN030`-`ZODSGEN043`, for example `ZODSGEN042` for a rule that omits `ErrorCode`/
`MessageFormat`, or `ZODSGEN043` for a built-in rule that cannot produce a validation attribute); see
ZodSharp's
[Source Generator Diagnostics](https://purview.dev/docs/zodsharp/source-generator-diagnostics/) for the full
list.

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
public sealed partial class RegistrationDto
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;

    [Range(13, 120)]
    public int Age { get; init; }

    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    // Custom refinement, declared by the ZodSharp generator. The generator runs these issues after
    // the DataAnnotations rules.
    partial void OnZodValidate(RefineCtx<RegistrationDto> context)
    {
        if (context.Value.Name.StartsWith("x", StringComparison.OrdinalIgnoreCase))
            context.AddIssue("name", "Name cannot start with 'x'.", [nameof(Name)]);
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
(`ImmutableArray<ValidationError>`). Each `ValidationError` carries a `Code`, `Message`, and `Path`, plus the
optional structured `Origin`, `Category`, and size bounds that rules and structured issues report:

```csharp
foreach (var error in result.Errors)
    Console.WriteLine($"{string.Join(".", error.Path)}: {error.Message} ({error.Code}, {error.Origin})");
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
  compiler for that project, so nothing has to be reflected or registered. The fixtures include type-level
  custom rules (`Serialization/ZodSchemaRuleModels.cs`), including a rule written against the underlying
  value that the ZodSharp generator adapts automatically, so rule `Code`/`Origin` and the empty path are
  covered end-to-end.

The loaded generator carries its own framework implementation, so it keeps its own log sink and
CodeWriter scope validation: do not assert on its log entries, and leave `ValidateCodeWriterScopes`
off for that run.

## See also

- The runnable `src/src/ZodSharpSample` project.
- [Getting Started](Getting-Started.md)
- [Value Object Design](Value-Object-Design.md)
