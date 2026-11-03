using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PackedValueObjectsWasm;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The generated, trim-safe registration of this assembly's [ZodSchema] types (the form model and the value objects),
// then ZodSharp for Blazor forms, which also makes <DataAnnotationsValidator /> validate them.
builder.Services.AddZodSharpPackedValueObjectsWasmSchemas();
builder.Services.AddZodSharpBlazor();

await builder.Build().RunAsync();
