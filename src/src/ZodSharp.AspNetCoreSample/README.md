# Purview.ValueObjects + ZodSharp ASP.NET Core Sample

Shows how value objects validated by [Purview.ZodSharp](https://www.nuget.org/packages/Purview.ZodSharp)
surface as standard ASP.NET Core `Problem Details` responses using
[Purview.ZodSharp.AspNetCore](https://www.nuget.org/packages/Purview.ZodSharp.AspNetCore).

## Run

```text
dotnet run --project src/src/ZodSharp.AspNetCoreSample
```

## What it shows

- **Strict deserialization of value objects** — `EmailAddress` and `OrderQuantity` are annotated with
  `[Scalar]` + `[ZodSchema]` and `ValueObjectDeserializationMode.Strict`. Their generated `Create` runs
  the ZodSharp-generated schema, so deserializing an invalid value throws a `ZodException`.
- **Automatic exception handling** — `AddZodSharpProblemDetails()` registers `ZodExceptionHandler` and
  `app.UseExceptionHandler()` catches the thrown `ZodException`, converting it to a
  `HttpValidationProblemDetails` response with the structured issues in the `issues` extension.
- **Mapping error types to status codes** — `ConcurrentErrorType.SaveFailed` is registered in
  `ErrorTypeRegistry.Default`; a `ZodException` carrying the `aggregate_save_failed` code is returned as
  a `409 Conflict` with a message formatted from the error's parameters.
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

Send an invalid email — the strict value object throws and the handler returns a 400 Problem Details
payload:

```bash
curl -i -X POST http://localhost:5000/orders \
  -H "Content-Type: application/json" \
  -d '{"customerEmail":"not-an-email","quantity":2}'
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