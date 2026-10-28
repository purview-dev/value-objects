# Trimming, Native AOT and Blazor

The code the generator writes into your project - the factories, equality, comparison, parsing and formatting
members, the JSON converters and the ZodSharp integration - is trim- and Native AOT-safe. It uses no reflection,
so a trimmed or AOT-compiled application, a Blazor WebAssembly client among them, gets no IL2xxx/IL3xxx warning
from it.

That is proven against the packed package, not the repository's own projects: `just smoke-packed-consumers`
packs `Purview.ValueObjects`, then publishes and runs a Native AOT console, a Blazor WebAssembly app (trimmed, and
with `RunAOTCompilation=true`, loaded in a headless browser), a Blazor Server app and a `net8.0` console against
it, every one with warnings as errors, and scans every output for compiler-host assemblies.

## Target frameworks

| Package | Target frameworks |
| --- | --- |
| `Purview.ValueObjects` | `net8.0`, `net9.0`, `net10.0`, `net11.0` |
| `Purview.ZodSharp` 3.x (optional) | `net10.0`, `net11.0` |
| `Purview.ZodSharp.Blazor` 3.x (optional) | `net11.0` |

`Purview.ValueObjects` has no dependency on ZodSharp. A project that adds the ZodSharp integration needs the
target frameworks ZodSharp supports; a project that does not keeps the full range.

## The one reflection-based component

`ScalarJsonConverterFactory` builds converters with reflection and expression trees, and is annotated
`[RequiresUnreferencedCode]`/`[RequiresDynamicCode]`, so registering it in a trimmed or AOT application warns at
your call site. You do not need it for a generated value object: each one carries `[JsonConverter]` pointing at
its generated converter.

## System.Text.Json source generation

The generated converters read and write through the `JsonSerializerOptions` they are called with, so naming
policies, number handling and your own converters apply as usual. With a source-generated `JsonSerializerContext`
there is one thing to know.

**A context in the project that declares the value objects cannot see their `[JsonConverter]` attribute.** The
value object generator adds it, and one source generator never sees another's output, so the context describes
each value object as a plain object with a `Value` property. Register the generated converters on the options:

```csharp
[JsonSerializable(typeof(Order))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public sealed partial class AppJsonContext : JsonSerializerContext;

JsonSerializerOptions options = new(AppJsonContext.Default.Options)
{
    Converters =
    {
        new EmailAddress.EmailAddressJsonConverter(),
        new Money.MoneyJsonConverter(),
    },
};

var json = JsonSerializer.Serialize(order, (JsonTypeInfo<Order>)options.GetTypeInfo(typeof(Order)));
```

A value object declared in a **referenced library** needs none of this: the attribute is compiled into the library,
and the context in your application sees it.

Inside a generated converter:

- a member of a complex value object that is itself a value object is written with that value object's generated
  converter, so `Money.Currency` (a `CurrencyCode`) does not have to be registered or listed;
- an underlying or member value of a primitive type (`string`, `Guid`, the numeric types, `bool`, the date and time
  types, `Uri`, `Version`, and their nullable forms) falls back to the built-in System.Text.Json converter when the
  context never listed it;
- anything else - an enum, or a custom underlying type - needs metadata, so add `[JsonSerializable(typeof(T))]` to
  the context. The `NotSupportedException` thrown otherwise names the type.

## Blazor

### Binding a value object in a form

The built-in inputs bind primitives. A small input over a string-backed scalar binds the value object itself, and
uses `Hydrate` - which never validates - so the form's validator reports what is wrong with the value, through the
value object's own rules, instead of the input rejecting it before any rule runs:

```csharp
public sealed class InputScalar<TScalar> : InputBase<TScalar>
    where TScalar : struct, IScalarValueObject<TScalar, string>
{
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "input");
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        if (!string.IsNullOrEmpty(NameAttributeValue))
            builder.AddAttribute(2, "name", NameAttributeValue);
        builder.AddAttribute(3, "class", CssClass);
        builder.AddAttribute(4, "value", CurrentValueAsString);
        builder.AddAttribute(5, "onchange",
            EventCallback.Factory.CreateBinder<string?>(this, v => CurrentValueAsString = v, CurrentValueAsString));
        builder.CloseElement();
    }

    protected override string? FormatValueAsString(TScalar value) => value.Value;

    protected override bool TryParseValueFromString(
        string? value, [MaybeNullWhen(false)] out TScalar result, [NotNullWhen(false)] out string? validationErrorMessage)
    {
        result = TScalar.Hydrate(value ?? string.Empty);
        validationErrorMessage = null;
        return true;
    }
}
```

```razor
<InputScalar TScalar="EmailAddress" @bind-Value="model.Email" />
```

In a **statically rendered** form, a posted value binds through the value object's generated `IParsable`
implementation, whose `TryParse` runs `TryCreate`: an invalid value is reported as a form mapping error on its
field ("The value 'abc' is not valid for 'Postcode'."), and a valid one arrives normalized.

### Validating with ZodSharp

Mark the form model and its value objects `[ZodSchema]` and register the generated schemas and ZodSharp for Blazor
(`Purview.ZodSharp.Blazor`, `net11.0`) in the client and the server:

```csharp
builder.Services.AddZodSharpMyAppSchemas(); // generated: AddZodSharp{Assembly}Schemas()
builder.Services.AddZodSharpBlazor();
```

```razor
<EditForm EditContext="editContext">
    <ZodValidator />
    <InputScalar TScalar="EmailAddress" @bind-Value="model.Email" />
    <ZodValidationMessage For="() => model.Email" />
    <ZodValidationSummary />
</EditForm>
```

Each value object member is validated by the value object's own generated schema, so the rules live with the value
object rather than being repeated on every form.

An error inside a value object is located at `Email.Value`, but a struct cannot be a `FieldIdentifier` model, so
ZodSharp places it on the owning member's field: `<ZodValidationMessage For="() => model.Email" />` (or
`<ValidationMessage>`) shows it next to the value object's input, with `<ZodValidator>` and with
`<DataAnnotationsValidator />`, on Blazor Server and in WebAssembly alike. A scalar's messages name the owning
member, not its own `Value`: `Field 'Email' must be a valid email address.` A type-level rule such as
`[NonSentinel(Message = "Choose a plan.")]` is located at the member itself and keeps its own message.

A form or request model may declare a value object member as `[Required]` and nullable (`EmailAddress?`): a missing
value is reported at the member, and a present one is validated by the value object's own schema.
