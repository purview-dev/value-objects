using PackedValueObjectsServer.Components;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// The generated, trim-safe registration of this assembly's [ZodSchema] types (the form model and the value objects),
// then ZodSharp for Blazor forms.
builder.Services.AddZodSharpPackedValueObjectsServerSchemas();
builder.Services.AddZodSharpBlazor();

var app = builder.Build();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

await app.RunAsync();
