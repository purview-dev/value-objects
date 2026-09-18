# Purview.ValueObjects + ZodSharp Sample

Validates scalar and complex value objects with [Purview.ZodSharp](https://www.nuget.org/packages/Purview.ZodSharp),
a C# port of the Zod schema validation library.

## Run

```text
dotnet run --project samples/ValueObjects.ZodSharpSample
```

## What it shows

- **Generated validators on value objects** — `[Scalar]` value objects annotated with `[ZodSchema]`
  plus DataAnnotations on their underlying value. `EmailAddressSchema.Validate/Parse` and
  `CurrencyCodeSchema.Validate` validate the value object directly; `ApplyRefine` composes extra
  rules such as "only example.com addresses allowed".
- **Schema-first validation** — hand-built schemas (`Z.String().Email()`, `Z.String().Regex(...)`,
  `Z.Enum<OrderStatusKind>()`, `Z.Number().Positive()`) validate the raw underlying value, then the
  result is mapped onto the value object via its strict `Create` factory.
- **DTO validation** — a `[ZodSchema]` `RegistrationDto` validated by the generated schema, including
  a custom `Validate()` refinement method, then mapped to value objects.
- **DI / factory** — `ZodSchemaFactory` resolving both the generated `EmailAddressSchemaValidator`
  and a hand-built `ZodSchemaValidator<string>`.

See `docs/ZodSharp-Validation.md` for the full guide.