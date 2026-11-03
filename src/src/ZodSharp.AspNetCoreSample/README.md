# Purview.ValueObjects + ZodSharp ASP.NET Core Sample

Shows how value objects validated by [Purview.ZodSharp](https://www.nuget.org/packages/Purview.ZodSharp)
surface as standard ASP.NET Core `Problem Details` responses using
[Purview.ZodSharp.AspNetCore](https://www.nuget.org/packages/Purview.ZodSharp.AspNetCore).

## Run

```text
dotnet run --project src/src/ZodSharp.AspNetCoreSample
```

## What it shows

- **Validating a request of value objects** — `EmailAddress` and `OrderQuantity` are `[Scalar]` +
  `[ZodSchema]`, and `PlaceOrderRequest` is `[ZodSchema]`. The request binds with the default `Hydrate` mode,
  and `.WithZodSharpValidation()` validates it with `PlaceOrderRequestSchema`, which validates each member
  through that value object's own schema. Every invalid member is reported under its own path
  (`CustomerEmail.Value`, `Quantity.Value`) in an `HttpValidationProblemDetails` response with the structured
  `issues` extension. The validators are registered with the generated, trim-safe
  `AddZodSharpPurviewValueObjectsZodSharpAspNetCoreSampleSchemas()`; nothing is registered on the JSON options.
- **Automatic exception handling** — `AddZodSharpProblemDetails()` registers `ZodExceptionHandler` and
  `app.UseExceptionHandler()` catches a thrown `ZodException` (below), converting it to the same contract.
- **Mapping error types to status codes** — `ErrorTypes.SaveFailed` (a `[ErrorType]`-generated partial)
  is registered in `ErrorTypeRegistry.Default`; `ErrorTypes.ThrowSaveFailed(...)` throws a `ZodException`
  carrying the `aggregate_save_failed` code, which is returned as a `409 Conflict` with a message
  formatted from the error's parameters.
- **On-demand mapping** — `ZodException.ToHttpValidationProblemDetails(...)` converts an exception
  explicitly, without the exception-handling middleware.
- **Compile-time placeholder checking** — the `ZODSASP001` analyzer (bundled with the package) verifies
  that every `MessageFormat` placeholder is declared in `ErrorType.Parameters`.

## Try it

Place an order with a valid body:

```bash
curl -i -X POST http://localhost:5000/orders \
  -H "Content-Type: application/json" \
  -d '{"customerEmail":"demo@example.com","quantity":2}'
```

Send an invalid email and quantity — the filter returns a 400 Problem Details payload with
`errors["CustomerEmail.Value"]` and `errors["Quantity.Value"]`:

```bash
curl -i -X POST http://localhost:5000/orders \
  -H "Content-Type: application/json" \
  -d '{"customerEmail":"not-an-email","quantity":500}'
```

Simulate an optimistic-concurrency failure (returns `409 Conflict` with a formatted message):

```bash
curl -i -X POST http://localhost:5000/orders/ord-123/confirm
```

Validate an email on demand:

```bash
curl -i "http://localhost:5000/orders/ord-123/email?email=not-an-email"
```

See `docs/ZodSharp-Validation.md` for the full guide.