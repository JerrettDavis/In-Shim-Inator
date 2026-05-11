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

        var memberAccess = root.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf().OfType<MemberAccessExpressionSyntax>().FirstOrDefault();
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
            fieldDeclaration => IsCompatibleTimeProviderType(semanticModel.GetTypeInfo(fieldDeclaration.Declaration.Type, cancellationToken).Type, timeProviderType));

        var fieldName = "_timeProvider";
        TypeSyntax fieldTypeSyntax = SyntaxFactory.ParseTypeName("global::System.TimeProvider");
        ITypeSymbol fieldTypeSymbol = timeProviderType;
        if (timeProviderField is null)
        {
            fieldName = GetUniqueFieldName(classDeclaration, fieldName);
            var field = (FieldDeclarationSyntax)editor.Generator.FieldDeclaration(
                fieldName,
                fieldTypeSyntax,
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
            fieldTypeSyntax = timeProviderField.Declaration.Type;
            fieldTypeSymbol = semanticModel.GetTypeInfo(fieldTypeSyntax, cancellationToken).Type ?? timeProviderType;
        }

        // 2. Update constructors (or create one)
        var constructors = classDeclaration.Members.OfType<ConstructorDeclarationSyntax>().ToList();
        if (constructors.Count == 0)
        {
            var parameter = (ParameterSyntax)editor.Generator.ParameterDeclaration(
                "timeProvider",
                fieldTypeSyntax);
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
            var constructorSymbols = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
            foreach (var constructor in constructors)
            {
                var constructorSymbol = semanticModel.GetDeclaredSymbol(constructor, cancellationToken);
                if (constructorSymbol is not null)
                {
                    constructorSymbols.Add(constructorSymbol);
                }
            }

            foreach (var constructor in constructors)
            {
                var updatedConstructor = constructor;

                var existingParameter = constructor.ParameterList.Parameters.FirstOrDefault(
                    p =>
                    {
                        if (p.Type is null)
                        {
                            return false;
                        }

                        var parameterType = semanticModel.GetTypeInfo(p.Type, cancellationToken).Type;
                        return IsCompatibleTimeProviderType(parameterType, timeProviderType)
                            && CanAssignType(parameterType, fieldTypeSymbol, semanticModel.Compilation);
                    });

                var parameterName = existingParameter?.Identifier.ValueText ?? GetUniqueParameterName(constructor, "timeProvider");
                if (existingParameter is null)
                {
                    var parameter = (ParameterSyntax)editor.Generator.ParameterDeclaration(
                        parameterName,
                        fieldTypeSyntax);
                    updatedConstructor = updatedConstructor.AddParameterListParameters(parameter);
                }

                var canPassToThisInitializer = CanPassToThisInitializerArgument(constructor, semanticModel, constructorSymbols, cancellationToken);
                var initializerResult = EnsureThisInitializerHasArgument(updatedConstructor, parameterName, canPassToThisInitializer);
                updatedConstructor = initializerResult.UpdatedConstructor;

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

                if (!initializerResult.PassesParameterToThisInitializer)
                {
                    updatedConstructor = EnsureFieldAssignmentFromParameter(editor, updatedConstructor, fieldName, parameterName);
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

    private static bool IsCompatibleTimeProviderType(ITypeSymbol? typeSymbol, INamedTypeSymbol timeProviderType)
    {
        if (typeSymbol is null)
        {
            return false;
        }

        for (var currentType = typeSymbol; currentType is not null; currentType = currentType.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(currentType, timeProviderType))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetUniqueFieldName(ClassDeclarationSyntax classDeclaration, string baseName)
    {
        var usedNameSet = new HashSet<string>();
        foreach (var member in classDeclaration.Members)
        {
            switch (member)
            {
                case FieldDeclarationSyntax field:
                    foreach (var variable in field.Declaration.Variables)
                    {
                        usedNameSet.Add(variable.Identifier.ValueText);
                    }
                    break;
                case EventFieldDeclarationSyntax eventField:
                    foreach (var variable in eventField.Declaration.Variables)
                    {
                        usedNameSet.Add(variable.Identifier.ValueText);
                    }
                    break;
                case PropertyDeclarationSyntax property:
                    usedNameSet.Add(property.Identifier.ValueText);
                    break;
                case MethodDeclarationSyntax method:
                    usedNameSet.Add(method.Identifier.ValueText);
                    break;
                case EventDeclarationSyntax @event:
                    usedNameSet.Add(@event.Identifier.ValueText);
                    break;
                case BaseTypeDeclarationSyntax nestedType:
                    usedNameSet.Add(nestedType.Identifier.ValueText);
                    break;
                case DelegateDeclarationSyntax @delegate:
                    usedNameSet.Add(@delegate.Identifier.ValueText);
                    break;
            }
        }

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

    private static bool CanAssignType(ITypeSymbol? sourceType, ITypeSymbol destinationType, Compilation compilation) =>
        sourceType is not null && compilation.ClassifyConversion(sourceType, destinationType).IsImplicit;

    private static string GetUniqueParameterName(ConstructorDeclarationSyntax constructor, string baseName)
    {
        var usedNameSet = new HashSet<string>(constructor.ParameterList.Parameters
            .Select(parameter => parameter.Identifier.ValueText));
        foreach (var bodyIdentifier in GetDeclaredBodyIdentifiers(constructor))
        {
            usedNameSet.Add(bodyIdentifier);
        }

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

    private static IEnumerable<string> GetDeclaredBodyIdentifiers(ConstructorDeclarationSyntax constructor)
    {
        if (constructor.Body is null)
        {
            yield break;
        }

        foreach (var variable in constructor.Body.DescendantNodes().OfType<VariableDeclaratorSyntax>())
        {
            yield return variable.Identifier.ValueText;
        }

        foreach (var @foreach in constructor.Body.DescendantNodes().OfType<ForEachStatementSyntax>())
        {
            yield return @foreach.Identifier.ValueText;
        }

        foreach (var @catch in constructor.Body.DescendantNodes().OfType<CatchDeclarationSyntax>())
        {
            if (@catch.Identifier != default)
            {
                yield return @catch.Identifier.ValueText;
            }
        }

        foreach (var designation in constructor.Body.DescendantNodes().OfType<SingleVariableDesignationSyntax>())
        {
            yield return designation.Identifier.ValueText;
        }
    }

    private static bool CanPassToThisInitializerArgument(
        ConstructorDeclarationSyntax constructor,
        SemanticModel semanticModel,
        ISet<IMethodSymbol> constructorSymbols,
        CancellationToken cancellationToken)
    {
        var initializer = constructor.Initializer;
        if (initializer is null || !initializer.IsKind(SyntaxKind.ThisConstructorInitializer))
        {
            return false;
        }

        var targetConstructorSymbol = semanticModel.GetSymbolInfo(initializer, cancellationToken).Symbol as IMethodSymbol;
        if (targetConstructorSymbol is null || !constructorSymbols.Contains(targetConstructorSymbol))
        {
            return false;
        }

        return true;
    }

    private static (ConstructorDeclarationSyntax UpdatedConstructor, bool PassesParameterToThisInitializer) EnsureThisInitializerHasArgument(
        ConstructorDeclarationSyntax constructor,
        string parameterName,
        bool canPassToThisInitializer)
    {
        var initializer = constructor.Initializer;
        if (!canPassToThisInitializer || initializer is null || !initializer.IsKind(SyntaxKind.ThisConstructorInitializer))
        {
            return (constructor, false);
        }

        var alreadyPassed = initializer.ArgumentList.Arguments.Any(
            argument => argument.Expression is IdentifierNameSyntax identifier
                && identifier.Identifier.ValueText == parameterName);
        if (alreadyPassed)
        {
            return (constructor, true);
        }

        return (
            constructor.WithInitializer(
                initializer.WithArgumentList(
                    initializer.ArgumentList.AddArguments(SyntaxFactory.Argument(SyntaxFactory.IdentifierName(parameterName))))),
            true);
    }

    private static StatementSyntax ExpressionToStatement(ExpressionSyntax expression) =>
        expression is ThrowExpressionSyntax throwExpression
            ? SyntaxFactory.ThrowStatement(throwExpression.Expression)
            : SyntaxFactory.ExpressionStatement(expression);

    private static ExpressionStatementSyntax CreateFieldAssignmentStatement(DocumentEditor editor, string fieldName, string parameterName)
    {
        var needsQualifiedFieldAccess = string.Equals(fieldName, parameterName, System.StringComparison.Ordinal);
        var assignmentTarget = needsQualifiedFieldAccess
            ? editor.Generator.MemberAccessExpression(editor.Generator.ThisExpression(), editor.Generator.IdentifierName(fieldName))
            : editor.Generator.IdentifierName(fieldName);

        return (ExpressionStatementSyntax)editor.Generator.ExpressionStatement(
            editor.Generator.AssignmentStatement(
                assignmentTarget,
                editor.Generator.IdentifierName(parameterName)));
    }

    private static ConstructorDeclarationSyntax EnsureFieldAssignmentFromParameter(
        DocumentEditor editor,
        ConstructorDeclarationSyntax constructor,
        string fieldName,
        string parameterName)
    {
        if (constructor.Body is null)
        {
            return constructor;
        }

        var fieldAssignmentStatement = constructor.Body.Statements
            .OfType<ExpressionStatementSyntax>()
            .FirstOrDefault(statement => statement.Expression is AssignmentExpressionSyntax assignment && IsFieldAssignmentTarget(assignment.Left, fieldName));
        if (fieldAssignmentStatement is null)
        {
            var assignment = CreateFieldAssignmentStatement(editor, fieldName, parameterName);
            return constructor.WithBody(constructor.Body.WithStatements(constructor.Body.Statements.Insert(0, assignment)));
        }

        var fieldAssignment = (AssignmentExpressionSyntax)fieldAssignmentStatement.Expression;
        if (fieldAssignment.Right is IdentifierNameSyntax identifier && identifier.Identifier.ValueText == parameterName)
        {
            return constructor;
        }

        var updatedStatement = fieldAssignmentStatement.WithExpression(fieldAssignment.WithRight(SyntaxFactory.IdentifierName(parameterName)));
        return constructor.ReplaceNode(fieldAssignmentStatement, updatedStatement);
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
