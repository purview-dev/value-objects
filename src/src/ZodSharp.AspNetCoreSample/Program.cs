using Purview.ValueObjects.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Register the ScalarJsonConverterFactory so value objects serialize as their scalar value and
// deserialize through the configured ValueObjectDeserializationMode (Strict here).
builder.Services.ConfigureHttpJsonOptions(options =>
	options.SerializerOptions.Converters.Add(new ScalarJsonConverterFactory())
);

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

// Binds a request containing strict scalar value objects. Invalid JSON (for example an invalid
// email or a quantity outside 1-100) throws a ZodException during deserialization, which the
// exception handler turns into a 400 HttpValidationProblemDetails response.
app.MapPost(
	"/orders",
	(PlaceOrderRequest request) =>
		Results.Ok(new { Email = request.CustomerEmail.Value, Quantity = request.Quantity.Value })
);

// Throws a ZodException carrying a registered error code. The handler resolves the ErrorType from
// the registry and returns a 409 Conflict response whose message is formatted from the error's
// parameters. ThrowSaveFailed is generated as void + [DoesNotReturn], so the endpoint is a void
// handler that always throws.
app.MapPost("/orders/{orderId}/confirm", ConfirmOrder);

static void ConfirmOrder(string orderId) => ErrorTypes.ThrowSaveFailed(orderId, "Order");

// Demonstrates on-demand mapping: a ZodException caught in the handler is converted explicitly
// with ErrorType resolution, without relying on the exception-handling middleware.
app.MapGet(
	"/orders/{orderId}/email",
	(string orderId, string email) =>
	{
		try
		{
			var parsed = EmailAddress.Create(email);
			return Results.Ok(new { Email = parsed.Value, OrderId = orderId });
		}
		catch (ZodException ex)
		{
			var problem = ex.ToHttpValidationProblemDetails(ErrorTypeRegistry.Default);
			return Results.Problem(
				problem.Detail,
				statusCode: problem.Status,
				title: problem.Title,
				extensions: new Dictionary<string, object?> { ["issues"] = problem.Extensions["issues"] }
			);
		}
	}
);

app.Run();
