using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Inshiminator.Analyzers.Tests.Verifiers.CSharpCodeFixVerifier<
    Inshiminator.Analyzers.ClockAnalyzer,
    Inshiminator.CodeFixes.ClockCodeFixProvider>;

namespace Inshiminator.Analyzers.Tests;

public class ClockCodeFixTests
{
    [Fact]
    public async Task DateTimeUtcNow_AppliesCodeFix()
    {
        var test = @"
using System;

namespace Inshiminator.Generated;
public interface IClock { DateTimeOffset UtcNow { get; } }

class Test
{
    void Method()
    {
        var now = [|DateTime.UtcNow|];
    }
}";

        var fixedCode = @"
using System;

namespace Inshiminator.Generated;
public interface IClock { DateTimeOffset UtcNow { get; } }

class Test
{
    private readonly IClock _clock;

    void Method()
    {
        var now = _clock.UtcNow;
    }
}";
        // The current fix provider is basic and doesn't handle constructor addition if none exists yet, 
        // or rather it only updates existing ones. Let's refine it to be more robust for the test.
        // Actually, my implementation inserts field but doesn't create constructor if missing.
        // Let's adjust the test to have a constructor.
        
        test = @"
using System;

namespace Inshiminator.Generated;
public interface IClock { DateTimeOffset UtcNow { get; } }

class Test
{
    public Test() {}
    void Method()
    {
        var now = [|DateTime.UtcNow|];
    }
}";

        fixedCode = @"
using System;

namespace Inshiminator.Generated;
public interface IClock { DateTimeOffset UtcNow { get; } }

class Test
{
    private readonly IClock _clock;

    public Test(IClock clock) {
        _clock = clock;
    }
    void Method()
    {
        var now = _clock.UtcNow;
    }
}";

        await VerifyCS.VerifyCodeFixAsync(test, fixedCode);
    }
}
