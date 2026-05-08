using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Inshiminator.Analyzers.Tests.Verifiers.CSharpCodeFixVerifier<
    Inshiminator.Analyzers.GuidAnalyzer,
    Inshiminator.CodeFixes.GuidCodeFixProvider>;

namespace Inshiminator.Analyzers.Tests;

public class GuidTests
{
    [Fact]
    public async Task GuidNewGuid_ReportsDiagnostic()
    {
        var test = @"
using System;
class Test
{
    void Method()
    {
        var guid = [|Guid.NewGuid()|];
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task GuidNewGuid_AppliesCodeFix()
    {
        var test = @"
using System;
namespace Inshiminator.Generated;
public interface IGuidGenerator { Guid NewGuid(); }
class Test
{
    public Test() {}
    void Method()
    {
        var guid = [|Guid.NewGuid()|];
    }
}";

        var fixedCode = @"
using System;
namespace Inshiminator.Generated;
public interface IGuidGenerator { Guid NewGuid(); }
class Test
{
    private readonly IGuidGenerator _guidGenerator;

    public Test(IGuidGenerator guidGenerator) {
        _guidGenerator = guidGenerator;
    }
    void Method()
    {
        var guid = _guidGenerator.NewGuid();
    }
}";
        await VerifyCS.VerifyCodeFixAsync(test, fixedCode);
    }
}
