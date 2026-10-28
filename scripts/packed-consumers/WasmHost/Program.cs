using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

// Serves a published Blazor WebAssembly wwwroot folder: the files as they are, index.html for any other path.
if (args.Length != 1 || !Directory.Exists(args[0]))
{
	Console.Error.WriteLine("Usage: PackedWasmHost <published wwwroot folder>");
	return 2;
}

var root = Path.GetFullPath(args[0]);
var builder = WebApplication.CreateSlimBuilder();
var app = builder.Build();

PhysicalFileProvider files = new(root);
FileExtensionContentTypeProvider contentTypes = new();
contentTypes.Mappings[".dat"] = "application/octet-stream";
contentTypes.Mappings[".wasm"] = "application/wasm";
contentTypes.Mappings[".blat"] = "application/octet-stream";

app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
app.UseStaticFiles(
	new StaticFileOptions
	{
		FileProvider = files,
		ContentTypeProvider = contentTypes,
		ServeUnknownFileTypes = true,
	}
);
app.MapFallback(async context =>
{
	context.Response.ContentType = "text/html";
	await context.Response.SendFileAsync(Path.Combine(root, "index.html"));
});

await app.RunAsync();
return 0;
