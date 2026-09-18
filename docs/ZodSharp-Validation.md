# Validating Value Objects with ZodSharp

[Purview.ZodSharp](https://www.nuget.org/packages/Purview.ZodSharp) is a high-performance C# port of the
[Zod](https://github.com/colinhacks/zod) schema validation library. It complements `Purview.ValueObjects`:
the value object owns the invariants, ZodSharp owns the rule definitions and validation results.

Two patterns are covered here, both demonstrated in `samples/ValueObjects.ZodSharpSample`:

1. **Generated validators** — annotate a value object or DTO with `[ZodSchema]` and DataAnnotations; a
   source generator emits a zero-allocation `{Type}Schema` validator.
2. **Schema-first validation** — build a schema for the scalar's underlying value with `Z.String()`,
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

## 2. Schema-first validation

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

## 3. Validating DTOs before mapping to value objects

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

## 4. Dependency injection and the schema factory

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