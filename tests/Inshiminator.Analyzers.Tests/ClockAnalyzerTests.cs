using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Inshiminator.Analyzers.Tests.Verifiers.CSharpAnalyzerVerifier<Inshiminator.Analyzers.ClockAnalyzer>;

namespace Inshiminator.Analyzers.Tests;

public class ClockAnalyzerTests
{
    [Fact]
    public async Task DateTimeUtcNow_ReportsDiagnostic()
    {
        var test = @"
using System;
class Test
{
    void Method()
    {
        var now = [|DateTime.UtcNow|];
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task DateTimeNow_ReportsDiagnostic()
    {
        var test = @"
using System;
class Test
{
    void Method()
    {
        var now = [|DateTime.Now|];
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task DateTimeOffsetUtcNow_ReportsDiagnostic()
    {
        var test = @"
using System;
class Test
{
    void Method()
    {
        var now = [|DateTimeOffset.UtcNow|];
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task UnrelatedMemberAccess_DoesNotReportDiagnostic()
    {
        var test = @"
class Test
{
    string Now => ""now"";
    void Method()
    {
        var s = this.Now;
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(test);
    }
}
