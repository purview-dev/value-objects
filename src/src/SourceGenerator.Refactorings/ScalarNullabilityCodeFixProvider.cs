using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Purview.ValueObjects.SourceGenerator.Common;

namespace Purview.ValueObjects.SourceGenerator.Refactorings;

/// <summary>
/// Resolves the duplicate nullability declaration reported as <c>VO1028</c>: the automatic
/// <c>[Scalar&lt;T&gt;(Nullable = true)]</c> form already declares the underlying <c>Value</c> property,
/// so a second declaration is a conflicting source of nullability. Three repairs are offered because
/// the right one depends on which source the author wants to keep.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ScalarNullabilityCodeFixProvider)), Shared]
public sealed class ScalarNullabilityCodeFixProvider : CodeFixProvider
{
	const string RemoveDeclaredPropertyKey = "ScalarNullability.RemoveDeclaredProperty";

	const string UseManualFormKey = "ScalarNullability.UseManualForm";

	const string RemoveNullableOptionKey = "ScalarNullability.RemoveNullableOption";

	/// <inheritdoc/>
	public override ImmutableArray<string> FixableDiagnosticIds =>
		[DiagnosticLibrary.ScalarNullabilityDeclaredTwice.Id];

	/// <inheritdoc/>
	public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	/// <inheritdoc/>
	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		if (root is null)
			return;

		foreach (var diagnostic in context.Diagnostics)
		{
			var property = root.FindToken(diagnostic.Location.SourceSpan.Start)
				.Parent?.AncestorsAndSelf()
				.OfType<PropertyDeclarationSyntax>()
				.FirstOrDefault();
			if (property is null || !IsNamedValue(property))
				continue;

			context.RegisterCodeFix(
				CodeAction.Create(
					title: "Remove the declared 'Value' property",
					createChangedDocument: cancellationToken =>
						RemovePropertyAsync(context.Document, property, cancellationToken),
					equivalenceKey: RemoveDeclaredPropertyKey
				),
				diagnostic
			);

			var attribute = await FindScalarAttributeAsync(root, property, context.CancellationToken)
				.ConfigureAwait(false);
			if (attribute is null)
				continue;

			context.RegisterCodeFix(
				CodeAction.Create(
					title: "Use the manual [Scalar] form (remove the type argument and 'Nullable = true')",
					createChangedDocument: cancellationToken =>
						RewriteAttributeAsync(context.Document, attribute, useManualForm: true, cancellationToken),
					equivalenceKey: UseManualFormKey
				),
				diagnostic
			);

			context.RegisterCodeFix(
				CodeAction.Create(
					title: "Remove only 'Nullable = true'",
					createChangedDocument: cancellationToken =>
						RewriteAttributeAsync(context.Document, attribute, useManualForm: false, cancellationToken),
					equivalenceKey: RemoveNullableOptionKey
				),
				diagnostic
			);
		}
	}

	static bool IsNamedValue(PropertyDeclarationSyntax property) =>
		string.Equals(property.Identifier.ValueText, "Value", StringComparison.Ordinal);

	/// <summary>
	/// The <c>[Scalar]</c>/<c>[Scalar&lt;T&gt;]</c> attribute applied to the property's containing type.
	/// The lookup is syntactic: a code-fix document in a bare workspace has no metadata references, so
	/// the attribute class cannot be resolved through the semantic model. A partial declaration that
	/// carries the attribute in a different document is not found, in which case only the
	/// remove-the-property repair is offered.
	/// </summary>
	static Task<AttributeSyntax?> FindScalarAttributeAsync(
		SyntaxNode root,
		PropertyDeclarationSyntax property,
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var typeDeclaration = property.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().FirstOrDefault();
		if (typeDeclaration is null)
			return Task.FromResult<AttributeSyntax?>(null);

		var typeName = typeDeclaration.Identifier.ValueText;
		var attribute = root.DescendantNodes()
			.OfType<TypeDeclarationSyntax>()
			.Where(candidate => string.Equals(candidate.Identifier.ValueText, typeName, StringComparison.Ordinal))
			.SelectMany(candidate => candidate.AttributeLists)
			.SelectMany(list => list.Attributes)
			.FirstOrDefault(candidate =>
				string.Equals(GetSimpleName(candidate.Name).Identifier.ValueText, "Scalar", StringComparison.Ordinal)
			);

		return Task.FromResult<AttributeSyntax?>(attribute);
	}

	static SimpleNameSyntax GetSimpleName(NameSyntax name) =>
		name switch
		{
			GenericNameSyntax generic => generic,
			QualifiedNameSyntax qualified => GetSimpleName(qualified.Right),
			AliasQualifiedNameSyntax alias => GetSimpleName(alias.Name),
			SimpleNameSyntax simple => simple,
			_ => SyntaxFactory.IdentifierName(name.ToString()),
		};

	static async Task<Document> RemovePropertyAsync(
		Document document,
		PropertyDeclarationSyntax property,
		CancellationToken cancellationToken
	)
	{
		var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
		if (root is null)
			return document;

		var updated = root.RemoveNode(property, SyntaxRemoveOptions.KeepNoTrivia);
		return updated is null
			? document
			: await FormatAsync(document.WithSyntaxRoot(updated), cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Rewrites the scalar attribute. <paramref name="useManualForm"/> also drops the underlying-type
	/// argument (and any positional property-name argument), turning <c>[Scalar&lt;T&gt;(...)]</c> or
	/// <c>[Scalar(typeof(T), ...)]</c> into the manual <c>[Scalar(...)]</c> form, so the declared property
	/// becomes valid instead of merely swapping one error for <c>VO1022</c>.
	/// </summary>
	static async Task<Document> RewriteAttributeAsync(
		Document document,
		AttributeSyntax attribute,
		bool useManualForm,
		CancellationToken cancellationToken
	)
	{
		var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
		if (root is null)
			return document;

		var updatedName = useManualForm ? RemoveGenericArity(attribute.Name) : attribute.Name;
		var arguments = (attribute.ArgumentList?.Arguments ?? default)
			.Where(argument => !IsNullableOption(argument) && !(useManualForm && IsPositional(argument)))
			.ToImmutableArray();

		var argumentList = arguments.IsDefaultOrEmpty
			? null
			: SyntaxFactory.AttributeArgumentList(SyntaxFactory.SeparatedList(arguments));
		var updated = attribute
			.WithName(updatedName)
			.WithArgumentList(argumentList)
			.WithAdditionalAnnotations(Formatter.Annotation);

		var newRoot = root.ReplaceNode(attribute, updated);
		return await FormatAsync(document.WithSyntaxRoot(newRoot), cancellationToken).ConfigureAwait(false);
	}

	static bool IsNullableOption(AttributeArgumentSyntax argument) =>
		string.Equals(argument.NameEquals?.Name.Identifier.ValueText, "Nullable", StringComparison.Ordinal);

	static bool IsPositional(AttributeArgumentSyntax argument) =>
		argument.NameEquals is null && argument.NameColon is null;

	/// <summary>
	/// Removes the generic type argument from an attribute name, preserving any namespace qualification:
	/// <c>Scalar&lt;string&gt;</c> becomes <c>Scalar</c>, and <c>Json.Scalar&lt;string&gt;</c> becomes
	/// <c>Json.Scalar</c>.
	/// </summary>
	static NameSyntax RemoveGenericArity(NameSyntax name) =>
		name switch
		{
			GenericNameSyntax generic => SyntaxFactory.IdentifierName(generic.Identifier),
			QualifiedNameSyntax qualified => qualified.WithRight((SimpleNameSyntax)RemoveGenericArity(qualified.Right)),
			AliasQualifiedNameSyntax alias => alias.WithName((SimpleNameSyntax)RemoveGenericArity(alias.Name)),
			_ => name,
		};

	static async Task<Document> FormatAsync(Document document, CancellationToken cancellationToken)
	{
		var formatted = await Formatter
			.FormatAsync(document, cancellationToken: cancellationToken)
			.ConfigureAwait(false);
		return formatted;
	}
}
