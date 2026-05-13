using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Inshiminator.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ClockAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "INSHIM001";

    private static readonly LocalizableString Title = "Direct system clock usage detected";
    private static readonly LocalizableString MessageFormat = "Use IClock (or TimeProvider when available) instead of {0} so time can be controlled in tests";
    private static readonly LocalizableString Description = "Direct usage of system clock makes code difficult to test.";
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
        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
    }

    private void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;
        var memberName = memberAccess.Name.Identifier.Text;

        if (memberName is not ("UtcNow" or "Now"))
            return;

        var symbol = context.SemanticModel.GetSymbolInfo(memberAccess).Symbol;
        if (symbol is null)
            return;

        var containingType = symbol.ContainingType?.ToDisplayString();
        if (containingType is "System.DateTime" or "System.DateTimeOffset")
        {
            var diagnostic = Diagnostic.Create(Rule, memberAccess.GetLocation(), memberAccess.ToString());
            context.ReportDiagnostic(diagnostic);
        }
    }
}
