using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NotNullAnalyzer;

[ExportCodeFixProvider(LanguageNames.CSharp), Shared]
public sealed class NullableAssignmentCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(DiagnosticDescriptors.NOTNULL001.Id);

    public override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null) return;

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;
        var node = root.FindNode(diagnosticSpan);

        var declaration = node.AncestorsAndSelf()
            .FirstOrDefault(n => n is PropertyDeclarationSyntax or FieldDeclarationSyntax or VariableDeclaratorSyntax);

        if (declaration == null) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Remove nullable annotation",
                createChangedDocument: ct => RemoveNullableAnnotationAsync(context.Document, root, declaration, ct),
                equivalenceKey: "RemoveNullableAnnotation"),
            diagnostic);
    }

    private static Task<Document> RemoveNullableAnnotationAsync(
        Document document,
        SyntaxNode root,
        SyntaxNode declaration,
        CancellationToken cancellationToken)
    {
        SyntaxNode newRoot;

        switch (declaration)
        {
            case PropertyDeclarationSyntax property:
                newRoot = HandleProperty(root, property);
                break;
            case FieldDeclarationSyntax field:
                newRoot = HandleField(root, field);
                break;
            default:
                var parentField = declaration.AncestorsAndSelf().OfType<FieldDeclarationSyntax>().FirstOrDefault();
                if (parentField != null)
                    newRoot = HandleField(root, parentField);
                else
                    return Task.FromResult(document);
                break;
        }

        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private static SyntaxNode HandleProperty(SyntaxNode root, PropertyDeclarationSyntax property)
    {
        var newProperty = property;

        // Remove nullable annotation from type
        if (property.Type is NullableTypeSyntax nullableType)
        {
            newProperty = newProperty.WithType(
                nullableType.ElementType.WithTriviaFrom(nullableType));
        }

        // Remove `= null` initializer if present
        if (newProperty.Initializer != null &&
            newProperty.Initializer.Value is LiteralExpressionSyntax literal &&
            literal.IsKind(SyntaxKind.NullLiteralExpression))
        {
            newProperty = newProperty
                .WithInitializer(null)
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.None))
                .WithTrailingTrivia(newProperty.SemicolonToken.TrailingTrivia);

            if (newProperty.AccessorList != null)
            {
                newProperty = newProperty.WithAccessorList(
                    newProperty.AccessorList.WithTrailingTrivia(
                        property.SemicolonToken.TrailingTrivia));
            }
        }

        return root.ReplaceNode(property, newProperty);
    }

    private static SyntaxNode HandleField(SyntaxNode root, FieldDeclarationSyntax field)
    {
        var declaration = field.Declaration;

        if (declaration.Type is NullableTypeSyntax nullableType)
        {
            var newDeclaration = declaration.WithType(
                nullableType.ElementType.WithTriviaFrom(nullableType));

            var newVariables = newDeclaration.Variables.Select(v =>
            {
                if (v.Initializer != null &&
                    v.Initializer.Value is LiteralExpressionSyntax literal &&
                    literal.IsKind(SyntaxKind.NullLiteralExpression))
                {
                    return v.WithInitializer(null);
                }
                return v;
            });

            newDeclaration = newDeclaration.WithVariables(
                SyntaxFactory.SeparatedList(newVariables));

            var newField = field.WithDeclaration(newDeclaration);
            return root.ReplaceNode(field, newField);
        }

        return root;
    }
}
