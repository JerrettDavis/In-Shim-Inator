using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Inshiminator.Analyzers.Tests.Verifiers;

internal static class CSharpVerifierHelper
{
    internal static ImmutableDictionary<string, ReportDiagnostic> NullableWarnings { get; } = GetNullableWarningsFromCompiler();

    private static ImmutableDictionary<string, ReportDiagnostic> GetNullableWarningsFromCompiler()
    {
        string[] args = { "/warnaserror:nullable" };
        var commandLineArguments = CSharpCommandLineParser.Default.Parse(args, baseDirectory: null, sdkDirectory: null);
        var nullableWarnings = new Dictionary<string, ReportDiagnostic>();
        foreach (var diagnosticId in commandLineArguments.CompilationOptions.SpecificDiagnosticOptions.Keys)
        {
            if (diagnosticId.StartsWith("CS86"))
            {
                nullableWarnings[diagnosticId] = ReportDiagnostic.Error;
            }
        }

        return nullableWarnings.ToImmutableDictionary();
    }
}
