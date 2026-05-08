using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Inshiminator.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class GuidAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "INSHIM002";

    private static readonly LocalizableString Title = "Direct GUID generation detected";
    private static readonly LocalizableString MessageFormat = "Use IGuidGenerator instead of Guid.NewGuid()";
    private static readonly LocalizableString Description = "Direct usage of Guid.NewGuid() makes code difficult to test.";
    private const string Category = "Design";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return;

        var memberName = memberAccess.Name.Identifier.Text;
        if (memberName != "NewGuid")
            return;

        var symbol = context.SemanticModel.GetSymbolInfo(invocation).Symbol;
        if (symbol is null)
            return;

        var containingType = symbol.ContainingType?.ToDisplayString();
        if (containingType is "System.Guid")
        {
            var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }
    }
}
