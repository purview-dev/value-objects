// Shared by the Blazor consumers (copied in by scripts/test-packed-consumers.ps1).
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Purview.ValueObjects;

namespace PackedValueObjectsBlazor;

/// <summary>
/// A text input bound to a string-backed scalar value object. What is typed is kept with <c>Hydrate</c> - which
/// never validates - so the form's validator reports what is wrong with it through the value object's own schema,
/// instead of the input rejecting it as unparseable before any rule runs.
/// </summary>
/// <typeparam name="TScalar">The scalar value object.</typeparam>
public sealed class InputScalar<TScalar> : InputBase<TScalar>
	where TScalar : struct, IScalarValueObject<TScalar, string>
{
	protected override void BuildRenderTree(RenderTreeBuilder builder)
	{
		builder.OpenElement(0, "input");
		builder.AddMultipleAttributes(1, AdditionalAttributes);
		// The name a statically rendered form posts the value under, exactly as InputText writes it.
		if (!string.IsNullOrEmpty(NameAttributeValue))
			builder.AddAttribute(2, "name", NameAttributeValue);
		builder.AddAttribute(3, "class", CssClass);
		builder.AddAttribute(4, "value", CurrentValueAsString);
		builder.AddAttribute(
			5,
			"onchange",
			EventCallback.Factory.CreateBinder<string?>(
				this,
				value => CurrentValueAsString = value,
				CurrentValueAsString
			)
		);
		builder.CloseElement();
	}

	protected override string? FormatValueAsString(TScalar value) => value.Value;

	protected override bool TryParseValueFromString(
		string? value,
		[MaybeNullWhen(false)] out TScalar result,
		[NotNullWhen(false)] out string? validationErrorMessage
	)
	{
		result = TScalar.Hydrate(value ?? string.Empty);
		validationErrorMessage = null;
		return true;
	}
}
