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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ClockCodeFixProvider)), Shared]
public class ClockCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("INSHIM001");

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null) return;

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var memberAccess = root.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf().OfType<MemberAccessExpressionSyntax>().First();
        if (memberAccess is null) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Use injected IClock",
                createChangedDocument: c => UseInjectedClockAsync(context.Document, memberAccess, c),
                equivalenceKey: nameof(ClockCodeFixProvider)),
            diagnostic);

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Use injected TimeProvider",
                createChangedDocument: c => UseInjectedTimeProviderAsync(context.Document, memberAccess, c),
                equivalenceKey: $"{nameof(ClockCodeFixProvider)}_TimeProvider"),
            diagnostic);
    }

    private async Task<Document> UseInjectedClockAsync(Document document, MemberAccessExpressionSyntax memberAccess, CancellationToken cancellationToken)
    {
        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        var root = editor.OriginalRoot;

        var classDeclaration = memberAccess.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (classDeclaration is null) return document;

        // 1. Add IClock field if it doesn't exist
        var clockField = classDeclaration.Members.OfType<FieldDeclarationSyntax>()
            .FirstOrDefault(f => f.Declaration.Type.ToString() == "IClock");

        string fieldName = "_clock";
        if (clockField is null)
        {
            var field = (FieldDeclarationSyntax)editor.Generator.FieldDeclaration(
                "_clock",
                editor.Generator.IdentifierName("IClock"),
                Accessibility.Private,
                DeclarationModifiers.ReadOnly);
            editor.InsertBefore(classDeclaration.Members.First(), field);
        }
        else
        {
            fieldName = clockField.Declaration.Variables.First().Identifier.Text;
        }

        // 2. Update constructor
        var constructor = classDeclaration.Members.OfType<ConstructorDeclarationSyntax>().FirstOrDefault();
        if (constructor is not null)
        {
            var hasClockParam = constructor.ParameterList.Parameters.Any(p => p.Type?.ToString() == "IClock");
            if (!hasClockParam)
            {
                var parameter = (ParameterSyntax)editor.Generator.ParameterDeclaration(
                    "clock",
                    editor.Generator.IdentifierName("IClock"));
                
                var assignment = (ExpressionStatementSyntax)editor.Generator.ExpressionStatement(
                    editor.Generator.AssignmentStatement(
                        editor.Generator.IdentifierName(fieldName),
                        editor.Generator.IdentifierName("clock")));
                
                var newConstructor = constructor
                    .AddParameterListParameters(parameter)
                    .AddBodyStatements(assignment);
                
                editor.ReplaceNode(constructor, newConstructor);
            }
        }

        // 3. Replace usage
        var memberName = memberAccess.Name.Identifier.Text;
        var replacement = editor.Generator.MemberAccessExpression(
            editor.Generator.IdentifierName(fieldName),
            memberName);

        editor.ReplaceNode(memberAccess, replacement);

        return editor.GetChangedDocument();
    }

    private async Task<Document> UseInjectedTimeProviderAsync(Document document, MemberAccessExpressionSyntax memberAccess, CancellationToken cancellationToken)
    {
        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        var classDeclaration = memberAccess.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (classDeclaration is null) return document;

        // 1. Add TimeProvider field if it doesn't exist
        var timeProviderField = classDeclaration.Members.OfType<FieldDeclarationSyntax>()
            .FirstOrDefault(f => IsTimeProviderType(f.Declaration.Type));

        string fieldName = "_timeProvider";
        if (timeProviderField is null)
        {
            var field = SyntaxFactory.FieldDeclaration(
                    SyntaxFactory.VariableDeclaration(
                        SyntaxFactory.ParseTypeName("global::System.TimeProvider"),
                        SyntaxFactory.SingletonSeparatedList(SyntaxFactory.VariableDeclarator(fieldName))))
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword), SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword));
            if (classDeclaration.Members.Count > 0)
            {
                editor.InsertBefore(classDeclaration.Members.First(), field);
            }
            else
            {
                editor.AddMember(classDeclaration, field);
            }
        }
        else
        {
            fieldName = timeProviderField.Declaration.Variables.First().Identifier.Text;
        }

        // 2. Update constructor
        var constructor = classDeclaration.Members.OfType<ConstructorDeclarationSyntax>().FirstOrDefault();
        if (constructor is not null)
        {
            var hasTimeProviderParam = constructor.ParameterList.Parameters.Any(
                p => IsTimeProviderType(p.Type));

            if (!hasTimeProviderParam)
            {
                var parameter = (ParameterSyntax)editor.Generator.ParameterDeclaration(
                    "timeProvider",
                    SyntaxFactory.ParseTypeName("global::System.TimeProvider"));

                var assignment = (ExpressionStatementSyntax)editor.Generator.ExpressionStatement(
                    editor.Generator.AssignmentStatement(
                        editor.Generator.IdentifierName(fieldName),
                        editor.Generator.IdentifierName("timeProvider")));

                var newConstructor = constructor
                    .AddParameterListParameters(parameter)
                    .AddBodyStatements(assignment);

                editor.ReplaceNode(constructor, newConstructor);
            }
        }

        // 3. Replace usage
        var replacement = memberAccess.Name.Identifier.Text switch
        {
            "UtcNow" => editor.Generator.InvocationExpression(
                editor.Generator.MemberAccessExpression(editor.Generator.IdentifierName(fieldName), "GetUtcNow")),
            "Now" => editor.Generator.InvocationExpression(
                editor.Generator.MemberAccessExpression(editor.Generator.IdentifierName(fieldName), "GetLocalNow")),
            _ => memberAccess,
        };

        editor.ReplaceNode(memberAccess, replacement);

        return editor.GetChangedDocument();
    }

    private static bool IsTimeProviderType(TypeSyntax? typeSyntax) =>
        typeSyntax switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText == "TimeProvider",
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText == "TimeProvider",
            AliasQualifiedNameSyntax aliasQualified => aliasQualified.Name.Identifier.ValueText == "TimeProvider",
            _ => false,
        };
}
