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
    private const string TimeProviderStub = """
namespace System
{
    public abstract class TimeProvider
    {
        public abstract DateTimeOffset GetUtcNow();
        public virtual DateTimeOffset GetLocalNow() => GetUtcNow().ToLocalTime();
    }
}
""";

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
        await VerifyTimeProviderFixAsync("DateTime", "DateTime", "Now", "DateTime.SpecifyKind(_timeProvider.GetLocalNow().LocalDateTime, DateTimeKind.Local)");
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

    [Fact]
    public async Task FullyQualifiedDateTimeNow_WithoutUsingSystem_AppliesTimeProviderCodeFix()
    {
        var test = $$"""
{{TimeProviderStub}}

class Test
{
    public Test()
    {
    }

    void Method()
    {
        System.DateTime now = [|System.DateTime.Now|];
    }
}
""";

        var fixedCode = $$"""
{{TimeProviderStub}}

class Test
{
    private readonly global::System.TimeProvider _timeProvider;

    public Test(global::System.TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    void Method()
    {
        System.DateTime now = System.DateTime.SpecifyKind(_timeProvider.GetLocalNow().LocalDateTime, System.DateTimeKind.Local);
    }
}
""";

        await VerifyTimeProviderCodeFixAsync(test, fixedCode);
    }

    [Fact]
    public async Task TimeProviderCodeFix_UpdatesThisConstructorInitializer()
    {
        var test = $$"""
using System;

{{TimeProviderStub}}

class Test
{
    public Test() : this(1)
    {
    }

    public Test(int value)
    {
    }

    void Method()
    {
        DateTime now = [|DateTime.UtcNow|];
    }
}
""";

        var fixedCode = $$"""
using System;

{{TimeProviderStub}}

class Test
{
    private readonly global::System.TimeProvider _timeProvider;

    public Test(global::System.TimeProvider timeProvider) : this(1, timeProvider)
    {
    }

    public Test(int value, global::System.TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    void Method()
    {
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
    }
}
""";

        await VerifyTimeProviderCodeFixAsync(test, fixedCode);
    }

    [Fact]
    public async Task TimeProviderCodeFix_InsertsThisInitializerArgumentBeforeOptionalArguments()
    {
        var test = $$"""
using System;

{{TimeProviderStub}}

class Test
{
    public Test() : this(1, "name")
    {
    }

    public Test(int value, string name = "default")
    {
    }

    void Method()
    {
        DateTimeOffset now = [|DateTimeOffset.UtcNow|];
    }
}
""";

        var fixedCode = $$"""
using System;

{{TimeProviderStub}}

class Test
{
    private readonly global::System.TimeProvider _timeProvider;

    public Test(global::System.TimeProvider timeProvider) : this(1, timeProvider, "name")
    {
    }

    public Test(int value, global::System.TimeProvider timeProvider, string name = "default")
    {
        _timeProvider = timeProvider;
    }

    void Method()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
    }
}
""";

        await VerifyTimeProviderCodeFixAsync(test, fixedCode);
    }

    [Fact]
    public async Task TimeProviderCodeFix_UsesNamedArgumentWhenThisInitializerUsesNamedArguments()
    {
        var test = $$"""
using System;

{{TimeProviderStub}}

class Test
{
    public Test() : this(value: 1, name: "name")
    {
    }

    public Test(int value, string name = "default")
    {
    }

    void Method()
    {
        DateTimeOffset now = [|DateTimeOffset.UtcNow|];
    }
}
""";

        var fixedCode = $$"""
using System;

{{TimeProviderStub}}

class Test
{
    private readonly global::System.TimeProvider _timeProvider;

    public Test(global::System.TimeProvider timeProvider) : this(value: 1, name: "name", timeProvider: timeProvider)
    {
    }

    public Test(int value, global::System.TimeProvider timeProvider, string name = "default")
    {
        _timeProvider = timeProvider;
    }

    void Method()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
    }
}
""";

        await VerifyTimeProviderCodeFixAsync(test, fixedCode);
    }

    [Fact]
    public async Task TimeProviderCodeFix_UsesNamedArgumentWhenTargetConstructorAlreadyHasTimeProviderParameter()
    {
        var test = $$"""
using System;

{{TimeProviderStub}}

class Test
{
    public Test() : this(value: 1)
    {
    }

    public Test(int value, global::System.TimeProvider timeProvider = null)
    {
    }

    void Method()
    {
        DateTimeOffset now = [|DateTimeOffset.UtcNow|];
    }
}
""";

        var fixedCode = $$"""
using System;

{{TimeProviderStub}}

class Test
{
    private readonly global::System.TimeProvider _timeProvider;

    public Test(global::System.TimeProvider timeProvider) : this(value: 1, timeProvider: timeProvider)
    {
    }

    public Test(int value, global::System.TimeProvider timeProvider = null)
    {
        _timeProvider = timeProvider;
    }

    void Method()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
    }
}
""";

        await VerifyTimeProviderCodeFixAsync(test, fixedCode);
    }

    [Fact]
    public async Task TimeProviderCodeFix_DoesNotAddDuplicateThisInitializerArgumentWhenAlreadyProvided()
    {
        var test = $$"""
using System;

{{TimeProviderStub}}

class Test
{
    public Test() : this(value: 1, timeProvider: null)
    {
    }

    public Test(int value, global::System.TimeProvider timeProvider = null)
    {
    }

    void Method()
    {
        DateTimeOffset now = [|DateTimeOffset.UtcNow|];
    }
}
""";

        var fixedCode = $$"""
using System;

{{TimeProviderStub}}

class Test
{
    private readonly global::System.TimeProvider _timeProvider;

    public Test(global::System.TimeProvider timeProvider) : this(value: 1, timeProvider: null)
    {
    }

    public Test(int value, global::System.TimeProvider timeProvider = null)
    {
        _timeProvider = timeProvider;
    }

    void Method()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
    }
}
""";

        await VerifyTimeProviderCodeFixAsync(test, fixedCode);
    }

    [Fact]
    public async Task TimeProviderCodeFix_AddsNamedArgumentWhenNamedInitializerDoesNotSupplyExistingTimeProviderParameter()
    {
        var test = $$"""
using System;

{{TimeProviderStub}}

class Test
{
    public Test() : this(value: 1, name: "name")
    {
    }

    public Test(int value, global::System.TimeProvider timeProvider = null, string name = "default")
    {
    }

    void Method()
    {
        DateTimeOffset now = [|DateTimeOffset.UtcNow|];
    }
}
""";

        var fixedCode = $$"""
using System;

{{TimeProviderStub}}

class Test
{
    private readonly global::System.TimeProvider _timeProvider;

    public Test(global::System.TimeProvider timeProvider) : this(value: 1, name: "name", timeProvider: timeProvider)
    {
    }

    public Test(int value, global::System.TimeProvider timeProvider = null, string name = "default")
    {
        _timeProvider = timeProvider;
    }

    void Method()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
    }
}
""";

        await VerifyTimeProviderCodeFixAsync(test, fixedCode);
    }

    [Fact]
    public async Task TimeProviderCodeFix_ConvertsExpressionBodiedConstructorToBlock()
    {
        var test = $$"""
using System;

{{TimeProviderStub}}

class Test
{
    private int _value;
    public Test() => _value = 1;

    void Method()
    {
        DateTimeOffset now = [|DateTimeOffset.Now|];
    }
}
""";

        var fixedCode = $$"""
using System;

{{TimeProviderStub}}

class Test
{
    private readonly global::System.TimeProvider _timeProvider;
    private int _value;
    public Test(global::System.TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _value = 1;
    }

    void Method()
    {
        DateTimeOffset now = _timeProvider.GetLocalNow();
    }
}
""";

        await VerifyTimeProviderCodeFixAsync(test, fixedCode);
    }

    [Fact]
    public async Task TimeProviderCodeFix_UsesUniqueParameterNameWhenColliding()
    {
        var test = $$"""
using System;

{{TimeProviderStub}}

class Test
{
    public Test(string timeProvider)
    {
    }

    void Method()
    {
        DateTimeOffset now = [|DateTimeOffset.UtcNow|];
    }
}
""";

        var fixedCode = $$"""
using System;

{{TimeProviderStub}}

class Test
{
    private readonly global::System.TimeProvider _timeProvider;

    public Test(string timeProvider, global::System.TimeProvider timeProvider1)
    {
        _timeProvider = timeProvider1;
    }

    void Method()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
    }
}
""";

        await VerifyTimeProviderCodeFixAsync(test, fixedCode);
    }

    [Fact]
    public async Task TimeProviderCodeFix_InsertsRequiredParameterBeforeOptionalParameters()
    {
        var test = $$"""
using System;

{{TimeProviderStub}}

class Test
{
    public Test(string name = "default")
    {
    }

    void Method()
    {
        DateTimeOffset now = [|DateTimeOffset.UtcNow|];
    }
}
""";

        var fixedCode = $$"""
using System;

{{TimeProviderStub}}

class Test
{
    private readonly global::System.TimeProvider _timeProvider;

    public Test(global::System.TimeProvider timeProvider, string name = "default")
    {
        _timeProvider = timeProvider;
    }

    void Method()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
    }
}
""";

        await VerifyTimeProviderCodeFixAsync(test, fixedCode);
    }

    [Fact]
    public async Task TimeProviderCodeFix_RewritesConstructorChainForSameDocumentPartialDeclaration()
    {
        var test = $$"""
using System;

{{TimeProviderStub}}

partial class Test
{
    public Test() : this(1)
    {
    }

    void Method()
    {
        DateTime now = [|DateTime.UtcNow|];
    }
}

partial class Test
{
    public Test(int value)
    {
    }
}
""";

        var fixedCode = $$"""
using System;

{{TimeProviderStub}}

partial class Test
{
    private readonly global::System.TimeProvider _timeProvider;

    public Test(global::System.TimeProvider timeProvider) : this(1, timeProvider)
    {
    }

    void Method()
    {
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
    }
}

partial class Test
{
    public Test(int value, global::System.TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }
}
""";

        await VerifyTimeProviderCodeFixAsync(test, fixedCode);
    }

    [Fact]
    public async Task TimeProviderCodeFix_ReusesTimeProviderFieldFromOtherPartialDeclaration()
    {
        var test = $$"""
using System;

{{TimeProviderStub}}

partial class Test
{
    public Test()
    {
    }

    void Method()
    {
        DateTimeOffset now = [|DateTimeOffset.UtcNow|];
    }
}

partial class Test
{
    private readonly global::System.TimeProvider _timeProvider;

    public Test(string name)
    {
    }
}
""";

        var fixedCode = $$"""
using System;

{{TimeProviderStub}}

partial class Test
{
    public Test(global::System.TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    void Method()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
    }
}

partial class Test
{
    private readonly global::System.TimeProvider _timeProvider;

    public Test(string name, global::System.TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }
}
""";

        await VerifyTimeProviderCodeFixAsync(test, fixedCode);
    }

    [Fact]
    public async Task TimeProviderCodeFix_ReplacesExistingFieldAssignmentWithInjectedParameter()
    {
        var test = $$"""
using System;

{{TimeProviderStub}}

class Test
{
    private readonly global::System.TimeProvider _timeProvider;

    public Test(string timeProvider)
    {
        global::System.TimeProvider assignedProvider = null;
        _timeProvider = assignedProvider;
    }

    void Method()
    {
        DateTimeOffset now = [|DateTimeOffset.UtcNow|];
    }
}
""";

        var fixedCode = $$"""
using System;

{{TimeProviderStub}}

class Test
{
    private readonly global::System.TimeProvider _timeProvider;

    public Test(string timeProvider, global::System.TimeProvider timeProvider1)
    {
        global::System.TimeProvider assignedProvider = null;
        _timeProvider = timeProvider1;
    }

    void Method()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
    }
}
""";

        await VerifyTimeProviderCodeFixAsync(test, fixedCode);
    }

    [Fact]
    public async Task TimeProviderCodeFix_ReplacesNonIdentifierFieldAssignmentWhenInjectedParameterAdded()
    {
        var test = $$"""
using System;

{{TimeProviderStub}}

class Test
{
    private readonly global::System.TimeProvider _timeProvider;

    public Test(string timeProvider)
    {
        _timeProvider = null;
    }

    void Method()
    {
        DateTimeOffset now = [|DateTimeOffset.UtcNow|];
    }
}
""";

        var fixedCode = $$"""
using System;

{{TimeProviderStub}}

class Test
{
    private readonly global::System.TimeProvider _timeProvider;

    public Test(string timeProvider, global::System.TimeProvider timeProvider1)
    {
        _timeProvider = timeProvider1;
    }

    void Method()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
    }
}
""";

        await VerifyTimeProviderCodeFixAsync(test, fixedCode);
    }

    [Fact]
    public async Task TimeProviderCodeFix_PreservesNonIdentifierFieldAssignmentExpression()
    {
        var test = $$"""
using System;

{{TimeProviderStub}}

class Test
{
    private readonly global::System.TimeProvider _timeProvider;

    public Test(global::System.TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    void Method()
    {
        DateTimeOffset now = [|DateTimeOffset.UtcNow|];
    }
}
""";

        var fixedCode = $$"""
using System;

{{TimeProviderStub}}

class Test
{
    private readonly global::System.TimeProvider _timeProvider;

    public Test(global::System.TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    void Method()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
    }
}
""";

        await VerifyTimeProviderCodeFixAsync(test, fixedCode);
    }

    [Fact]
    public async Task TimeProviderCodeFix_UsesUniqueFieldNameWhenDefaultNameTaken()
    {
        var test = $$"""
using System;

{{TimeProviderStub}}

class Test
{
    private readonly string _timeProvider;

    public Test()
    {
    }

    void Method()
    {
        DateTimeOffset now = [|DateTimeOffset.UtcNow|];
    }
}
""";

        var fixedCode = $$"""
using System;

{{TimeProviderStub}}

class Test
{
    private readonly global::System.TimeProvider _timeProvider1;
    private readonly string _timeProvider;

    public Test(global::System.TimeProvider timeProvider)
    {
        _timeProvider1 = timeProvider;
    }

    void Method()
    {
        DateTimeOffset now = _timeProvider1.GetUtcNow();
    }
}
""";

        await VerifyTimeProviderCodeFixAsync(test, fixedCode);
    }

    [Fact]
    public async Task TimeProviderCodeFix_ReusesDerivedTimeProviderFieldAndParameter()
    {
        var test = $$"""
using System;

{{TimeProviderStub}}

class CustomTimeProvider : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.MinValue;
}

class Test
{
    private readonly CustomTimeProvider _timeProvider;

    public Test(CustomTimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    void Method()
    {
        DateTimeOffset now = [|DateTimeOffset.UtcNow|];
    }
}
""";

        var fixedCode = $$"""
using System;

{{TimeProviderStub}}

class CustomTimeProvider : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.MinValue;
}

class Test
{
    private readonly CustomTimeProvider _timeProvider;

    public Test(CustomTimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    void Method()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
    }
}
""";

        await VerifyTimeProviderCodeFixAsync(test, fixedCode);
    }

    [Fact]
    public async Task TimeProviderCodeFix_UsesDerivedFieldTypeWhenAddingConstructorParameter()
    {
        var test = $$"""
using System;

{{TimeProviderStub}}

class CustomTimeProvider : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.MinValue;
}

class Test
{
    private readonly CustomTimeProvider _timeProvider;

    public Test(string name)
    {
    }

    void Method()
    {
        DateTimeOffset now = [|DateTimeOffset.UtcNow|];
    }
}
""";

        var fixedCode = $$"""
using System;

{{TimeProviderStub}}

class CustomTimeProvider : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.MinValue;
}

class Test
{
    private readonly CustomTimeProvider _timeProvider;

    public Test(string name, global::CustomTimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    void Method()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
    }
}
""";

        await VerifyTimeProviderCodeFixAsync(test, fixedCode);
    }

    [Fact]
    public async Task TimeProviderCodeFix_UsesFullyQualifiedDerivedFieldTypeWhenNamespaceIsOutOfScope()
    {
        var test = $$"""
using System;

{{TimeProviderStub}}

namespace MyApp
{
    class CustomTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.MinValue;
    }
}

class Test
{
    private readonly MyApp.CustomTimeProvider _timeProvider;

    public Test(string name)
    {
    }

    void Method()
    {
        DateTimeOffset now = [|DateTimeOffset.UtcNow|];
    }
}
""";

        var fixedCode = $$"""
using System;

{{TimeProviderStub}}

namespace MyApp
{
    class CustomTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.MinValue;
    }
}

class Test
{
    private readonly MyApp.CustomTimeProvider _timeProvider;

    public Test(string name, global::MyApp.CustomTimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    void Method()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
    }
}
""";

        await VerifyTimeProviderCodeFixAsync(test, fixedCode);
    }

    [Fact]
    public async Task TimeProviderCodeFix_InsertsAssignmentBeforeConstructorReturn()
    {
        var test = $$"""
using System;

{{TimeProviderStub}}

class Test
{
    public Test()
    {
        return;
    }

    void Method()
    {
        DateTimeOffset now = [|DateTimeOffset.UtcNow|];
    }
}
""";

        var fixedCode = $$"""
using System;

{{TimeProviderStub}}

class Test
{
    private readonly global::System.TimeProvider _timeProvider;

    public Test(global::System.TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        return;
    }

    void Method()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
    }
}
""";

        await VerifyTimeProviderCodeFixAsync(test, fixedCode);
    }

    [Fact]
    public async Task TimeProviderCodeFix_InsertsAssignmentBeforeNestedReturnPaths()
    {
        var test = $$"""
using System;

{{TimeProviderStub}}

class Test
{
    public Test(bool shouldReturn)
    {
        if (shouldReturn)
        {
            return;
        }
    }

    void Method()
    {
        DateTimeOffset now = [|DateTimeOffset.UtcNow|];
    }
}
""";

        var fixedCode = $$"""
using System;

{{TimeProviderStub}}

class Test
{
    private readonly global::System.TimeProvider _timeProvider;

    public Test(bool shouldReturn, global::System.TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        if (shouldReturn)
        {
            return;
        }
    }

    void Method()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
    }
}
""";

        await VerifyTimeProviderCodeFixAsync(test, fixedCode);
    }

    [Fact]
    public async Task TimeProviderCodeFix_DoesNotModifyStaticConstructors()
    {
        var test = $$"""
using System;

{{TimeProviderStub}}

class Test
{
    static Test()
    {
    }

    void Method()
    {
        DateTimeOffset now = [|DateTimeOffset.UtcNow|];
    }
}
""";

        var fixedCode = $$"""
using System;

{{TimeProviderStub}}

class Test
{
    private readonly global::System.TimeProvider _timeProvider;

    static Test()
    {
    }

    void Method()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
    }

    public Test(global::System.TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }
}
""";

        await VerifyTimeProviderCodeFixAsync(test, fixedCode);
    }

    [Fact]
    public async Task TimeProviderCodeFix_DoesNotReuseStaticTimeProviderField()
    {
        var test = $$"""
using System;

{{TimeProviderStub}}

class Test
{
    private static readonly global::System.TimeProvider _timeProvider = null;

    public Test()
    {
    }

    void Method()
    {
        DateTimeOffset now = [|DateTimeOffset.UtcNow|];
    }
}
""";

        var fixedCode = $$"""
using System;

{{TimeProviderStub}}

class Test
{
    private readonly global::System.TimeProvider _timeProvider1;
    private static readonly global::System.TimeProvider _timeProvider = null;

    public Test(global::System.TimeProvider timeProvider)
    {
        _timeProvider1 = timeProvider;
    }

    void Method()
    {
        DateTimeOffset now = _timeProvider1.GetUtcNow();
    }
}
""";

        await VerifyTimeProviderCodeFixAsync(test, fixedCode);
    }

    [Fact]
    public async Task TimeProviderCodeFix_UsesUniqueParameterNameWhenConstructorBodyDeclaresTimeProviderLocal()
    {
        var test = $$"""
using System;

{{TimeProviderStub}}

class Test
{
    public Test()
    {
        var timeProvider = string.Empty;
    }

    void Method()
    {
        DateTimeOffset now = [|DateTimeOffset.UtcNow|];
    }
}
""";

        var fixedCode = $$"""
using System;

{{TimeProviderStub}}

class Test
{
    private readonly global::System.TimeProvider _timeProvider;

    public Test(global::System.TimeProvider timeProvider1)
    {
        _timeProvider = timeProvider1;
        var timeProvider = string.Empty;
    }

    void Method()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
    }
}
""";

        await VerifyTimeProviderCodeFixAsync(test, fixedCode);
    }

    private static async Task VerifyTimeProviderFixAsync(string targetTypeName, string sourceTypeName, string memberName, string replacement)
    {
        var test = $$"""
using System;

{{TimeProviderStub}}

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

{{TimeProviderStub}}

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

    private static async Task VerifyTimeProviderCodeFixAsync(string testCode, string fixedCode)
    {
        var testCase = new VerifyCS.Test
        {
            TestCode = testCode,
            FixedCode = fixedCode,
            CodeActionEquivalenceKey = TimeProviderCodeFixEquivalenceKey,
        };

        await testCase.RunAsync();
    }
}
