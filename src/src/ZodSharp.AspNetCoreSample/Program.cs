using ZodSharp;
using ZodSharp.AspNetCore;
using ZodSharp.Core;

var builder = WebApplication.CreateBuilder(args);

// No JSON registration: each value object carries a generated [JsonConverter] that serializes it as its
// scalar value and, in the default Hydrate mode, binds it without validating. Do not register
// ScalarJsonConverterFactory: it adds reflection and is not trimming- or Native AOT-compatible.

// Registers the ZodSharp validators generated for this assembly, trim- and Native AOT-safe (assembly
// scanning is not). WithZodSharpValidation() below resolves the request's validator from them.
builder.Services.AddZodSharpPurviewValueObjectsZodSharpAspNetCoreSampleSchemas();

// Registers ZodExceptionHandler as an IExceptionHandler. Requires UseExceptionHandler() in the
// pipeline (added below), otherwise the handler is never invoked.
builder.Services.AddZodSharpProblemDetails();

// Required by the parameterless UseExceptionHandler() for its default fallback response when no
// registered handler matches the exception.
builder.Services.AddProblemDetails();

// Map error codes to HTTP statuses and formatted messages.
ErrorTypeRegistry.Default.Register(ErrorTypes.SaveFailed);

var app = builder.Build();

app.UseExceptionHandler();

// Binds a request of value objects, then validates it with PlaceOrderRequestSchema, which validates each
// member through that value object's own schema. An invalid email or a quantity outside 1-100 is a 400
// HttpValidationProblemDetails response listing every invalid member under its own path
// (CustomerEmail.Value, Quantity.Value). Strict deserialization reports the same path (on .NET 10 with
// AddZodSharpProblemDetails(o => o.HandleBindingFailures = true)), but stops at the first invalid member.
app.MapPost("/orders", (PlaceOrderRequest request) =>
		Results.Ok(new { Email = request.CustomerEmail.Value, Quantity = request.Quantity.Value })
	)
	.WithZodSharpValidation();

// Throws a ZodException carrying a registered error code. The handler resolves the ErrorType from
// the registry and returns a 409 Conflict response whose message is formatted from the error's
// parameters. ThrowSaveFailed is generated as void + [DoesNotReturn], so the endpoint is a void
// handler that always throws.
app.MapPost("/orders/{orderId}/confirm", ConfirmOrder);

static void ConfirmOrder(string orderId) => ErrorTypes.ThrowSaveFailed(orderId, "Order");

// Demonstrates on-demand mapping: a ZodException caught in the handler is converted explicitly
// with ErrorType resolution, without relying on the exception-handling middleware.
app.MapGet("/orders/{orderId}/email", (string orderId, string email, HttpContext httpContext) =>
{
	try
	{
		var parsed = EmailAddress.Create(email);
		return Results.Ok(new { Email = parsed.Value, OrderId = orderId });
	}
	catch (ZodException ex)
	{
		var problem = ex.ToHttpValidationProblemDetails(
			new ZodProblemDetailsOptions { Registry = ErrorTypeRegistry.Default },
			httpContext
		);
		return Results.Problem(
			problem.Detail,
			statusCode: problem.Status,
			title: problem.Title,
			extensions: new Dictionary<string, object?> { ["issues"] = problem.Extensions["issues"] }
		);
	}
});

app.Run();
