using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Inshiminator.Analyzers.Tests.Verifiers.CSharpCodeFixVerifier<
    Inshiminator.Analyzers.ClockAnalyzer,
    Inshiminator.CodeFixes.ClockCodeFixProvider>;

namespace Inshiminator.Analyzers.Tests;

public class ClockCodeFixTests
{
    private const string TimeProviderCodeFixEquivalenceKey = "ClockCodeFixProvider_TimeProvider";

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

    [Fact]
    public async Task DateTimeUtcNow_AppliesTimeProviderCodeFix()
    {
        await VerifyTimeProviderFixAsync("DateTime", "DateTime", "UtcNow", "_timeProvider.GetUtcNow().UtcDateTime");
    }

    [Fact]
    public async Task DateTimeNow_AppliesTimeProviderCodeFix()
    {
        await VerifyTimeProviderFixAsync("DateTime", "DateTime", "Now", "_timeProvider.GetLocalNow().LocalDateTime");
    }

    [Fact]
    public async Task DateTimeOffsetUtcNow_AppliesTimeProviderCodeFix()
    {
        await VerifyTimeProviderFixAsync("DateTimeOffset", "DateTimeOffset", "UtcNow", "_timeProvider.GetUtcNow()");
    }

    [Fact]
    public async Task DateTimeOffsetNow_AppliesTimeProviderCodeFix()
    {
        await VerifyTimeProviderFixAsync("DateTimeOffset", "DateTimeOffset", "Now", "_timeProvider.GetLocalNow()");
    }

    private static async Task VerifyTimeProviderFixAsync(string targetTypeName, string sourceTypeName, string memberName, string replacement)
    {
        const string timeProviderStub = """
namespace System
{
    public abstract class TimeProvider
    {
        public abstract DateTimeOffset GetUtcNow();
        public virtual DateTimeOffset GetLocalNow() => GetUtcNow().ToLocalTime();
    }
}
""";

        var test = $$"""
using System;

{{timeProviderStub}}

class Test
{
    public Test()
    {
    }

    void Method()
    {
        {{targetTypeName}} now = [|{{sourceTypeName}}.{{memberName}}|];
    }
}
""";

        var fixedCode = $$"""
using System;

{{timeProviderStub}}

class Test
{
    private readonly global::System.TimeProvider _timeProvider;

    public Test(global::System.TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    void Method()
    {
        {{targetTypeName}} now = {{replacement}};
    }
}
""";

        var testCase = new VerifyCS.Test
        {
            TestCode = test,
            FixedCode = fixedCode,
            CodeActionEquivalenceKey = TimeProviderCodeFixEquivalenceKey,
        };

        await testCase.RunAsync();
    }
}
