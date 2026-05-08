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
using Microsoft.CodeAnalysis.Editing;

namespace Inshiminator.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(GuidCodeFixProvider)), Shared]
public class GuidCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("INSHIM002");

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null) return;

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var invocation = root.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().First();
        if (invocation is null) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Use injected IGuidGenerator",
                createChangedDocument: c => UseInjectedGeneratorAsync(context.Document, invocation, c),
                equivalenceKey: nameof(GuidCodeFixProvider)),
            diagnostic);
    }

    private async Task<Document> UseInjectedGeneratorAsync(Document document, InvocationExpressionSyntax invocation, CancellationToken cancellationToken)
    {
        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        var root = editor.OriginalRoot;

        var classDeclaration = invocation.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (classDeclaration is null) return document;

        // 1. Add IGuidGenerator field if it doesn't exist
        var field = classDeclaration.Members.OfType<FieldDeclarationSyntax>()
            .FirstOrDefault(f => f.Declaration.Type.ToString() == "IGuidGenerator");

        string fieldName = "_guidGenerator";
        if (field is null)
        {
            var fieldDecl = (FieldDeclarationSyntax)editor.Generator.FieldDeclaration(
                "_guidGenerator",
                editor.Generator.IdentifierName("IGuidGenerator"),
                Accessibility.Private,
                DeclarationModifiers.ReadOnly);
            editor.InsertBefore(classDeclaration.Members.First(), fieldDecl);
        }
        else
        {
            fieldName = field.Declaration.Variables.First().Identifier.Text;
        }

        // 2. Update constructor
        var constructor = classDeclaration.Members.OfType<ConstructorDeclarationSyntax>().FirstOrDefault();
        if (constructor is not null)
        {
            var hasParam = constructor.ParameterList.Parameters.Any(p => p.Type?.ToString() == "IGuidGenerator");
            if (!hasParam)
            {
                var parameter = (ParameterSyntax)editor.Generator.ParameterDeclaration(
                    "guidGenerator",
                    editor.Generator.IdentifierName("IGuidGenerator"));
                
                var assignment = (ExpressionStatementSyntax)editor.Generator.ExpressionStatement(
                    editor.Generator.AssignmentStatement(
                        editor.Generator.IdentifierName(fieldName),
                        editor.Generator.IdentifierName("guidGenerator")));
                
                var newConstructor = constructor
                    .AddParameterListParameters(parameter)
                    .AddBodyStatements(assignment);
                
                editor.ReplaceNode(constructor, newConstructor);
            }
        }

        // 3. Replace usage
        var replacement = editor.Generator.InvocationExpression(
            editor.Generator.MemberAccessExpression(
                editor.Generator.IdentifierName(fieldName),
                "NewGuid"));

        editor.ReplaceNode(invocation, replacement);

        return editor.GetChangedDocument();
    }
}
