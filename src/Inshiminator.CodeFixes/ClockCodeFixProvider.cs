using System.Collections.Generic;
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
using Microsoft.CodeAnalysis.Formatting;

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

        var compilation = await context.Document.Project.GetCompilationAsync(context.CancellationToken).ConfigureAwait(false);
        if (compilation is null) return;

        var timeProviderType = compilation.GetTypeByMetadataName("System.TimeProvider");

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

        if (timeProviderType is not null)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Use injected TimeProvider",
                    createChangedDocument: c => UseInjectedTimeProviderAsync(context.Document, memberAccess, c),
                    equivalenceKey: $"{nameof(ClockCodeFixProvider)}_TimeProvider"),
                diagnostic);
        }
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
        var semanticModel = editor.SemanticModel;
        var timeProviderType = semanticModel.Compilation.GetTypeByMetadataName("System.TimeProvider");
        if (timeProviderType is null)
        {
            return document;
        }

        var classDeclaration = memberAccess.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (classDeclaration is null) return document;

        // 1. Add TimeProvider field if it doesn't exist
        var timeProviderField = classDeclaration.Members.OfType<FieldDeclarationSyntax>().FirstOrDefault(
            fieldDeclaration => IsSystemTimeProviderType(semanticModel.GetTypeInfo(fieldDeclaration.Declaration.Type, cancellationToken).Type, timeProviderType));

        string fieldName = "_timeProvider";
        if (timeProviderField is null)
        {
            var field = (FieldDeclarationSyntax)editor.Generator.FieldDeclaration(
                fieldName,
                SyntaxFactory.ParseTypeName("global::System.TimeProvider"),
                Accessibility.Private,
                DeclarationModifiers.ReadOnly);
            field = field.WithAdditionalAnnotations(Formatter.Annotation);
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

        // 2. Update constructors (or create one)
        var constructors = classDeclaration.Members.OfType<ConstructorDeclarationSyntax>().ToList();
        if (constructors.Count == 0)
        {
            var parameter = (ParameterSyntax)editor.Generator.ParameterDeclaration(
                "timeProvider",
                SyntaxFactory.ParseTypeName("global::System.TimeProvider"));
            var assignment = CreateFieldAssignmentStatement(editor, fieldName, "timeProvider");

            var newConstructor = (ConstructorDeclarationSyntax)editor.Generator.ConstructorDeclaration(
                classDeclaration.Identifier.ValueText,
                accessibility: Accessibility.Public,
                parameters: [parameter],
                statements: [assignment]);
            newConstructor = newConstructor.WithAdditionalAnnotations(Formatter.Annotation);

            editor.AddMember(classDeclaration, newConstructor);
        }
        else
        {
            foreach (var constructor in constructors)
            {
                var updatedConstructor = constructor;

                var existingParameter = constructor.ParameterList.Parameters.FirstOrDefault(
                    p => p.Type is not null && IsSystemTimeProviderType(semanticModel.GetTypeInfo(p.Type, cancellationToken).Type, timeProviderType));

                var parameterName = existingParameter?.Identifier.ValueText ?? GetUniqueParameterName(constructor, "timeProvider");
                if (existingParameter is null)
                {
                    var parameter = (ParameterSyntax)editor.Generator.ParameterDeclaration(
                        parameterName,
                        SyntaxFactory.ParseTypeName("global::System.TimeProvider"));
                    updatedConstructor = updatedConstructor.AddParameterListParameters(parameter);
                }

                updatedConstructor = EnsureThisInitializerHasArgument(updatedConstructor, parameterName);

                if (updatedConstructor.Body is null)
                {
                    var body = updatedConstructor.ExpressionBody is null
                        ? SyntaxFactory.Block()
                        : SyntaxFactory.Block(ExpressionToStatement(updatedConstructor.ExpressionBody.Expression));
                    updatedConstructor = updatedConstructor
                        .WithExpressionBody(null)
                        .WithSemicolonToken(default)
                        .WithBody(body);
                }

                var delegatesToThisConstructor = updatedConstructor.Initializer?.IsKind(SyntaxKind.ThisConstructorInitializer) == true;
                if (!delegatesToThisConstructor && !HasFieldAssignment(updatedConstructor, fieldName))
                {
                    var assignment = CreateFieldAssignmentStatement(editor, fieldName, parameterName);
                    updatedConstructor = updatedConstructor.AddBodyStatements(assignment);
                }

                editor.ReplaceNode(constructor, updatedConstructor.WithAdditionalAnnotations(Formatter.Annotation));
            }
        }

        // 3. Replace usage
        var memberSymbol = semanticModel.GetSymbolInfo(memberAccess, cancellationToken).Symbol;
        var containingType = memberSymbol?.ContainingType;
        var isDateTime = containingType?.ToDisplayString() == "System.DateTime";
        var memberName = memberAccess.Name.Identifier.Text;

        var replacement = memberName switch
        {
            "UtcNow" => editor.Generator.InvocationExpression(
                editor.Generator.MemberAccessExpression(editor.Generator.IdentifierName(fieldName), "GetUtcNow")),
            "Now" => editor.Generator.InvocationExpression(
                editor.Generator.MemberAccessExpression(editor.Generator.IdentifierName(fieldName), "GetLocalNow")),
            _ => memberAccess,
        };

        if (isDateTime)
        {
            replacement = memberName switch
            {
                "UtcNow" => editor.Generator.MemberAccessExpression(replacement, "UtcDateTime"),
                "Now" => editor.Generator.MemberAccessExpression(replacement, "LocalDateTime"),
                _ => replacement,
            };
        }

        editor.ReplaceNode(memberAccess, replacement.WithAdditionalAnnotations(Formatter.Annotation));

        return editor.GetChangedDocument();
    }

    private static bool IsSystemTimeProviderType(ITypeSymbol? typeSymbol, INamedTypeSymbol timeProviderType) =>
        typeSymbol is not null && SymbolEqualityComparer.Default.Equals(typeSymbol, timeProviderType);

    private static string GetUniqueParameterName(ConstructorDeclarationSyntax constructor, string baseName)
    {
        var usedNameSet = new HashSet<string>(constructor.ParameterList.Parameters
            .Select(parameter => parameter.Identifier.ValueText));
        if (!usedNameSet.Contains(baseName))
        {
            return baseName;
        }

        var suffix = 1;
        while (usedNameSet.Contains($"{baseName}{suffix}"))
        {
            suffix++;
        }

        return $"{baseName}{suffix}";
    }

    private static ConstructorDeclarationSyntax EnsureThisInitializerHasArgument(ConstructorDeclarationSyntax constructor, string parameterName)
    {
        var initializer = constructor.Initializer;
        if (initializer is null || !initializer.IsKind(SyntaxKind.ThisConstructorInitializer))
        {
            return constructor;
        }

        var alreadyPassed = initializer.ArgumentList.Arguments.Any(
            argument => argument.Expression is IdentifierNameSyntax identifier
                && identifier.Identifier.ValueText == parameterName);
        if (alreadyPassed)
        {
            return constructor;
        }

        return constructor.WithInitializer(
            initializer.WithArgumentList(
                initializer.ArgumentList.AddArguments(SyntaxFactory.Argument(SyntaxFactory.IdentifierName(parameterName)))));
    }

    private static StatementSyntax ExpressionToStatement(ExpressionSyntax expression) =>
        expression is ThrowExpressionSyntax throwExpression
            ? SyntaxFactory.ThrowStatement(throwExpression.Expression)
            : SyntaxFactory.ExpressionStatement(expression);

    private static ExpressionStatementSyntax CreateFieldAssignmentStatement(DocumentEditor editor, string fieldName, string parameterName)
    {
        var needsQualifiedFieldAccess = string.Equals(fieldName.TrimStart('_'), parameterName, System.StringComparison.Ordinal);
        var assignmentTarget = needsQualifiedFieldAccess
            ? editor.Generator.MemberAccessExpression(editor.Generator.ThisExpression(), editor.Generator.IdentifierName(fieldName))
            : editor.Generator.IdentifierName(fieldName);

        return (ExpressionStatementSyntax)editor.Generator.ExpressionStatement(
            editor.Generator.AssignmentStatement(
                assignmentTarget,
                editor.Generator.IdentifierName(parameterName)));
    }

    private static bool HasFieldAssignment(ConstructorDeclarationSyntax constructor, string fieldName)
    {
        if (constructor.Body is null)
        {
            return false;
        }

        return constructor.Body.Statements.OfType<ExpressionStatementSyntax>()
            .Select(statement => statement.Expression)
            .OfType<AssignmentExpressionSyntax>()
            .Any(assignment => IsFieldAssignmentTarget(assignment.Left, fieldName) && assignment.Right is IdentifierNameSyntax);
    }

    private static bool IsFieldAssignmentTarget(ExpressionSyntax expression, string fieldName) =>
        expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText == fieldName,
            MemberAccessExpressionSyntax memberAccess
                when memberAccess.Expression is ThisExpressionSyntax
                && memberAccess.Name.Identifier.ValueText == fieldName => true,
            _ => false,
        };

}
