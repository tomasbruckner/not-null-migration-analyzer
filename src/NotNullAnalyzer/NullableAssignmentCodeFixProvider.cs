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

        // If there are additional locations, this is an interface property with implementations
        if (diagnostic.AdditionalLocations.Count > 0)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Remove nullable annotation from interface and implementations",
                    createChangedSolution: ct => RemoveNullableFromInterfaceAndImplsAsync(
                        context.Document, diagnostic, ct),
                    equivalenceKey: "RemoveNullableAnnotation"),
                diagnostic);
        }
        else
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Remove nullable annotation",
                    createChangedDocument: ct => RemoveNullableAnnotationAsync(context.Document, root, declaration, ct),
                    equivalenceKey: "RemoveNullableAnnotation"),
                diagnostic);
        }
    }

    private static async Task<Solution> RemoveNullableFromInterfaceAndImplsAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var solution = document.Project.Solution;

        // Collect all locations: primary (interface) + additional (implementations)
        var allLocations = new[] { diagnostic.Location }
            .Concat(diagnostic.AdditionalLocations)
            .ToList();

        // Group by document to batch changes per file
        foreach (var locationGroup in allLocations.GroupBy(l => l.SourceTree?.FilePath))
        {
            var filePath = locationGroup.Key;
            if (filePath == null) continue;

            var doc = solution.Projects
                .SelectMany(p => p.Documents)
                .FirstOrDefault(d => d.FilePath == filePath);

            // Fallback: if file path doesn't match, try matching by syntax tree
            if (doc == null)
            {
                foreach (var loc in locationGroup)
                {
                    var tree = loc.SourceTree;
                    if (tree == null) continue;

                    doc = solution.GetDocument(tree);
                    if (doc != null) break;
                }
            }

            if (doc == null) continue;

            var root = await doc.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root == null) continue;

            var newRoot = root;
            var nodesToReplace = new System.Collections.Generic.Dictionary<SyntaxNode, SyntaxNode>();

            foreach (var location in locationGroup)
            {
                var node = newRoot.FindNode(location.SourceSpan);
                var decl = node.AncestorsAndSelf()
                    .FirstOrDefault(n => n is PropertyDeclarationSyntax or FieldDeclarationSyntax);

                if (decl is PropertyDeclarationSyntax prop)
                {
                    nodesToReplace[prop] = MakePropertyNonNullable(prop);
                }
                else if (decl is FieldDeclarationSyntax field)
                {
                    nodesToReplace[field] = MakeFieldNonNullable(field);
                }
            }

            newRoot = newRoot.ReplaceNodes(
                nodesToReplace.Keys,
                (original, _) => nodesToReplace[original]);

            solution = solution.WithDocumentSyntaxRoot(doc.Id, newRoot);
        }

        return solution;
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
                newRoot = root.ReplaceNode(property, MakePropertyNonNullable(property));
                break;
            case FieldDeclarationSyntax field:
                newRoot = root.ReplaceNode(field, MakeFieldNonNullable(field));
                break;
            default:
                var parentField = declaration.AncestorsAndSelf().OfType<FieldDeclarationSyntax>().FirstOrDefault();
                if (parentField != null)
                    newRoot = root.ReplaceNode(parentField, MakeFieldNonNullable(parentField));
                else
                    return Task.FromResult(document);
                break;
        }

        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private static PropertyDeclarationSyntax MakePropertyNonNullable(PropertyDeclarationSyntax property)
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

        return newProperty;
    }

    private static FieldDeclarationSyntax MakeFieldNonNullable(FieldDeclarationSyntax field)
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

            return field.WithDeclaration(newDeclaration);
        }

        return field;
    }
}
