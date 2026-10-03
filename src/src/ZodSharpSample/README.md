# Purview.ValueObjects + ZodSharp Sample

Validates scalar and complex value objects with [Purview.ZodSharp](https://www.nuget.org/packages/Purview.ZodSharp),
a C# port of the Zod schema validation library.

## Run

```text
dotnet run --project src/src/ZodSharpSample
```

## What it shows

- **Generator-integrated validation** — a value object annotated with both `[Scalar]` and `[ZodSchema]`
  has its generated `Create` wired to the ZodSharp-generated schema. `EmailAddress.Create("not-an-email")`
  throws a `ZodException`, and `PhoneNumber` uses `ZodSchemaMode.InsteadOfHooks` so the schema is the
  sole validation gate.
- **Zod refinement hooks** — `CorporateEmail` implements the generated `OnZodValidate(RefineCtx<T>)` hook to
  add a rule ZodSharp's DataAnnotations cannot express; the generated `Create` merges the hook's issues with
  the schema's own, while `Hydrate` stays replay-safe.
- **Type-level custom rules on scalars** — `AssetId` carries a `[ZodRule]`-mapped `[NonEmpty]` attribute whose
  rule reads the value object as a unit and owns its `Code`/`Origin`. `TenantId` reuses the normal
  `NonSentinelRule<Guid>` for a scalar through `ScalarRuleAdapter`, and the generated `Create` surfaces the
  rule's code and message while `Hydrate` stays replay-safe.
- **Generated validators on value objects** — `EmailAddressSchema.Validate/Parse` and
  `CurrencyCodeSchema.Validate` validate the value object directly; `ApplyRefine` composes extra
  rules such as "only example.com addresses allowed".
- **Schema-first validation** — hand-built schemas (`Z.String().Email()`, `Z.String().Regex(...)`,
  `Z.Enum<OrderStatusKind>()`, `Z.Number().Positive()`) validate the raw underlying value, then the
  result is mapped onto the value object via its strict `Create` factory.
- **DTO validation** — a `[ZodSchema]` `RegistrationDto` validated by the generated schema; it implements the
  generated `OnZodValidate` hook to add a refinement, and the validated values are mapped to value objects.
- **Async custom validation** — a `[ZodSchema(CustomValidationMethodName = ...)]` `PromoCode` whose
  generated `PromoCodeSchemaValidator.ValidateAsync` awaits a hand-written async rule after the
  synchronous DataAnnotations rules pass.
- **DI / factory** — `ZodSchemaFactory` resolving both the generated `EmailAddressSchemaValidator`
  and a hand-built `ZodSchemaValidator<string>`.

See `docs/ZodSharp-Validation.md` for the full guide.