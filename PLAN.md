# PLAN.md

# Inshiminator

## Analyzer-guided shim generation for .NET applications

Inshiminator is a .NET-first developer toolkit that uses **Roslyn analyzers, incremental source generators, and optional code fixes** to insert clean shims around hard dependencies without forcing teams into invasive rewrites.

The premise is simple: most applications have business logic directly touching time, files, randomness, environment variables, HTTP, process execution, vendor SDKs, and other things that make code hard to test or modernize. Inshiminator finds those dependency seams, generates strongly typed abstractions and implementations, and helps teams move from direct dependency usage to controlled boundaries.

The name works because the tool literally puts shims in things.

---

## 1. Product Summary

### 1.1 One-line Description

Inshiminator uses Roslyn analyzers and source generators to detect hard dependencies in .NET code and generate shims, fakes, adapters, diagnostics, and guardrails around them.

### 1.2 Serious Pitch

Inshiminator is an analyzer-guided shim generation toolkit for .NET. It detects direct usage of unstable or external dependencies, emits diagnostics with actionable fixes, generates compile-time shim abstractions and implementations, and helps teams enforce dependency-boundary rules incrementally.

### 1.3 Dumb Pitch

It sees your code raw-dogging `DateTime.UtcNow` and quietly slides a shim into the situation.

### 1.4 Primary Value Proposition

Instead of manually hunting every untestable dependency, writing repetitive interfaces, wiring implementations, and building fakes, teams can let Inshiminator generate most of the boring boundary code while analyzers keep new violations from leaking in.

### 1.5 Core Promise

Inshiminator turns hard dependencies into generated, governed, testable boundaries.

---

## 2. Architectural Thesis

The elegant version of Inshiminator should not behave like a blunt CLI refactoring hammer. It should behave like a compiler-adjacent assistant.

The best architecture is:

1. **Analyzers detect dependency boundary smells.**
2. **Diagnostics explain the problem and suggest the proper shim.**
3. **Source generators generate the shim surface.**
4. **Code fixes optionally update safe call sites.**
5. **MSBuild integration makes the generated boundary part of normal builds.**
6. **CLI tooling summarizes, baselines, and governs the system in CI.**

This means Inshiminator can work continuously while developers build, test, and edit code. The CLI remains useful, but the core product is not a one-time migration tool. It becomes a development-time boundary system.

---

## 3. Product Pillars

### 3.1 Detect

Roslyn analyzers detect direct dependency usage and classify it by category, severity, layer, and fixability.

### 3.2 Generate

Incremental source generators emit strongly typed shims, default implementations, fakes, test helpers, DI extension methods, and optional decorators.

### 3.3 Guide

Diagnostics and code fixes guide developers from direct usage toward generated shims.

### 3.4 Govern

Analyzer severity, baselines, CI guardrails, and SARIF output enforce standards over time.

### 3.5 Extend

The system supports custom shim providers, organization policy packs, templates, and category-specific generators.

---

## 4. Core Concept

### 4.1 What Is a Shim?

A shim is a generated boundary between application code and a dependency that is difficult to control directly.

A shim may include:

* An abstraction interface
* A default production implementation
* A fake implementation for tests
* A deterministic test builder
* A DI registration method
* A telemetry decorator
* A resilience decorator
* A record/replay adapter
* A compatibility adapter
* Analyzer rules that discourage bypassing the shim

### 4.2 What Is a Hard Dependency?

A hard dependency is direct usage of a system, runtime, external service, or concrete library that makes code difficult to test, observe, replace, or migrate.

Examples:

```csharp
DateTime.UtcNow
Guid.NewGuid()
Random.Shared
File.ReadAllText(path)
Environment.GetEnvironmentVariable("API_KEY")
new HttpClient()
Process.Start(...)
Thread.Sleep(...)
new SqlConnection(connectionString)
VendorSdk.StaticClient.DoThing(...)
```

### 4.3 Why Source Generators Matter

Many shims are repetitive. Developers should not need to hand-write a new `ISystemClock`, `SystemClock`, `FakeSystemClock`, and service registration in every project.

Source generators let Inshiminator provide this boundary code at compile time, consistently and with minimal friction.

---

## 5. System Architecture

### 5.1 Package Layout

Recommended package structure:

```text
Inshiminator.Abstractions
Inshiminator.Analyzers
Inshiminator.Generators
Inshiminator.CodeFixes
Inshiminator.Testing
Inshiminator.DependencyInjection
Inshiminator.Cli
Inshiminator.MSBuild
Inshiminator.PolicyPacks.Default
Inshiminator.PolicyPacks.CleanArchitecture
```

### 5.2 Package Responsibilities

#### `Inshiminator.Abstractions`

Contains stable contracts used by generated code and optional runtime helpers.

This package should be small, dependency-light, and safe for production projects.

Potential contents:

```csharp
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public interface IGuidGenerator
{
    Guid NewGuid();
}

public interface IRandomSource
{
    int Next();
    int Next(int maxValue);
    int Next(int minValue, int maxValue);
}
```

Design note: some shims can be generated without requiring this package, but a shared abstractions package gives teams stable common contracts.

#### `Inshiminator.Analyzers`

Contains Roslyn analyzers that detect direct hard dependency usage.

Responsibilities:

* Detect direct dependency calls
* Emit diagnostics
* Apply configurable severity
* Understand project/layer context where possible
* Suppress generated code
* Support `.editorconfig` configuration
* Support baseline metadata

#### `Inshiminator.Generators`

Contains incremental source generators that generate shim code based on configuration, attributes, and detected project capabilities.

Responsibilities:

* Generate interfaces
* Generate production implementations
* Generate fakes where appropriate
* Generate DI registration extensions
* Generate test helpers
* Generate optional decorators
* Emit source in deterministic form

#### `Inshiminator.CodeFixes`

Contains code fix providers for safe and local transformations.

Responsibilities:

* Replace direct `DateTime.UtcNow` with injected `IClock.UtcNow` where constructor injection is straightforward
* Replace `Guid.NewGuid()` with injected `IGuidGenerator.NewGuid()` where safe
* Replace `new HttpClient()` with an injected client or generated client boundary only where safe
* Add constructor parameters
* Add private fields
* Add using directives
* Avoid risky wide refactors

#### `Inshiminator.Testing`

Contains test support utilities and generated fake conventions.

Potential contents:

```csharp
FakeClock
FakeGuidGenerator
FakeRandomSource
FakeEnvironmentReader
FakeFileSystem
InshimTestHarness
```

#### `Inshiminator.DependencyInjection`

Contains optional DI helpers for Microsoft.Extensions.DependencyInjection.

Potential contents:

```csharp
services.AddInshiminatorClock();
services.AddInshiminatorGuidGenerator();
services.AddInshiminatorDefaults();
```

The source generator may also generate application-specific extension methods.

#### `Inshiminator.Cli`

Provides reporting, baselining, CI guard mode, and project-wide analysis.

Responsibilities:

* Run analyzers from command line
* Generate reports
* Create baselines
* Enforce no-new-violations gates
* Print shim coverage metrics
* Export SARIF/JSON/Markdown

#### `Inshiminator.MSBuild`

Provides build integration, defaults, generated file materialization options, and package ergonomics.

Responsibilities:

* Add analyzer/generator references
* Configure generated output behavior
* Support `EmitCompilerGeneratedFiles`
* Surface reports in artifacts
* Support central package configuration

#### `Inshiminator.PolicyPacks.*`

Provides prebuilt rule configurations and conventions.

Examples:

* Default policy pack
* Clean Architecture policy pack
* Strict testability policy pack
* Legacy migration policy pack
* Minimal API policy pack

---

## 6. Analyzer-first Workflow

### 6.1 Developer Experience

A developer installs Inshiminator into a project:

```bash
dotnet add package Inshiminator.Analyzers
dotnet add package Inshiminator.Generators
```

Then builds:

```bash
dotnet build
```

The compiler emits diagnostics:

```text
INSHIM001 Direct system clock usage detected.
OrderService.cs(42,21): DateTime.UtcNow should be accessed through IClock.
Suggested shim: clock
Code fix available: Inject IClock and replace usage.
```

The generator emits the required shim code:

```csharp
namespace MyApp.Generated.Shims;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
```

The developer applies a code fix, reviews the change, and commits.

### 6.2 Why This Is Better Than a CLI-only Refactor

A CLI refactor is episodic. Developers run it once, then the codebase drifts.

Analyzer/generator integration is continuous. Every build can detect violations, generate supporting code, and enforce boundaries.

This gives Inshiminator three major advantages:

1. **Lower friction:** generated shims appear as part of the normal build.
2. **Incremental adoption:** existing violations can be baselined while new ones are blocked.
3. **Long-term governance:** analyzers keep the codebase from regressing.

---

## 7. Source Generator Strategy

### 7.1 Generator Type

Use **incremental source generators**.

Reasons:

* Better performance
* Deterministic pipeline
* Works well with IDEs
* Supports incremental updates
* Avoids full-project regeneration on every edit

### 7.2 Inputs

Generator inputs may include:

* MSBuild properties
* Additional files
* `.editorconfig` options
* Attributes in source code
* Referenced package detection
* Project target framework
* Nullable context
* Existing user-defined abstractions

### 7.3 Configuration Sources

Support multiple configuration paths in order of precedence:

1. `.editorconfig`
2. MSBuild properties
3. `inshiminator.json` as an additional file
4. Attributes in source code
5. Built-in defaults

### 7.4 Generated Code Modes

#### Shared Abstraction Mode

Generated code references `Inshiminator.Abstractions`.

Pros:

* Stable contracts
* Easy cross-project use
* Less generated code

Cons:

* Runtime package dependency

#### Self-contained Mode

Generated code emits all required abstractions directly into the target project.

Pros:

* No runtime dependency
* Good for libraries and strict environments

Cons:

* More generated code
* Cross-project type sharing requires care

#### Hybrid Mode

Use shared abstractions for common shims and generated application-specific wrappers for specialized boundaries.

This should be the recommended default.

### 7.5 Generated Code Categories

MVP generated categories:

* Clock
* GUID generation
* Random source
* Environment reader
* File system facade
* Process runner
* Delay provider
* HTTP client boundary helpers

Future generated categories:

* Vendor SDK adapters
* Queue clients
* Database connection factories
* API compatibility adapters
* Record/replay wrappers
* OpenAPI client shims

---

## 8. Attribute Model

Attributes allow developers to opt into generation explicitly.

### 8.1 Assembly-level Generation

Example:

```csharp
[assembly: GenerateInshim("clock")]
[assembly: GenerateInshim("guid")]
[assembly: GenerateInshim("environment")]
```

This tells the generator to emit selected shim categories.

### 8.2 Type-level Boundaries

Example:

```csharp
[GenerateShimFor(typeof(StripeClient))]
public partial interface IStripeGateway;
```

The generator could inspect `StripeClient` and scaffold a wrapper pattern.

This should be future scope because arbitrary SDK wrapping gets complicated quickly.

### 8.3 Method-level Boundary Capture

Example:

```csharp
[ShimBoundary("payments")]
public partial class PaymentGateway;
```

Could be used later to generate decorators, recording wrappers, or telemetry layers.

### 8.4 Generated Dependency Injection

Example:

```csharp
[assembly: GenerateInshimDependencyInjection]
```

Generator emits:

```csharp
public static partial class InshiminatorServiceCollectionExtensions
{
    public static IServiceCollection AddGeneratedShims(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IGuidGenerator, GuidGenerator>();
        return services;
    }
}
```

---

## 9. `.editorconfig` Configuration

Analyzer and generator options should work naturally through `.editorconfig`.

Example:

```ini
# Inshiminator
inshiminator_generation_mode = hybrid
inshiminator_namespace = MyApp.Generated.Shims
inshiminator_generate_clock = true
inshiminator_generate_guid = true
inshiminator_generate_random = true
inshiminator_generate_environment = true
inshiminator_generate_filesystem = true
inshiminator_generate_di = true

# Analyzer severity
dotnet_diagnostic.INSHIM001.severity = warning
dotnet_diagnostic.INSHIM002.severity = warning
dotnet_diagnostic.INSHIM006.severity = error

# Layer-specific conventions
inshiminator_domain_projects = **/*.Domain.csproj
inshiminator_application_projects = **/*.Application.csproj
inshiminator_infrastructure_projects = **/*.Infrastructure.csproj
inshiminator_tests_projects = **/*Tests.csproj;**/*.Tests.csproj
```

Potential issue: `.editorconfig` globbing and MSBuild project awareness can get awkward. Use it for analyzer options, but allow richer project structure config in `inshiminator.json`.

---

## 10. MSBuild Configuration

Support MSBuild properties for package ergonomics.

Example:

```xml
<PropertyGroup>
  <InshiminatorGenerationMode>Hybrid</InshiminatorGenerationMode>
  <InshiminatorNamespace>MyApp.Generated.Shims</InshiminatorNamespace>
  <InshiminatorGenerateClock>true</InshiminatorGenerateClock>
  <InshiminatorGenerateGuid>true</InshiminatorGenerateGuid>
  <InshiminatorGenerateDependencyInjection>true</InshiminatorGenerateDependencyInjection>
  <InshiminatorEmitReports>true</InshiminatorEmitReports>
</PropertyGroup>
```

Support item-based configuration:

```xml
<ItemGroup>
  <InshiminatorShim Include="Clock" />
  <InshiminatorShim Include="Guid" />
  <InshiminatorShim Include="FileSystem" />
</ItemGroup>
```

This fits well with enterprise repo standards and central `Directory.Build.props` usage.

---

## 11. Code Fix Strategy

### 11.1 Code Fix Philosophy

Code fixes should be conservative.

The tool should only auto-transform code when the change is highly predictable. Risky changes should produce guidance, not surprise rewrites.

### 11.2 Safe Code Fix Example: Clock

Original:

```csharp
public sealed class OrderService
{
    public Order CreateOrder()
    {
        return new Order
        {
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
```

Fixed:

```csharp
public sealed class OrderService
{
    private readonly IClock _clock;

    public OrderService(IClock clock)
    {
        _clock = clock;
    }

    public Order CreateOrder()
    {
        return new Order
        {
            CreatedAt = _clock.UtcNow
        };
    }
}
```

### 11.3 Code Fix Safety Rules

A code fix may be applied automatically when:

* The containing type is instantiable through constructor injection
* The type already uses dependency injection patterns
* Adding a constructor parameter does not conflict with existing constructors
* The generated shim type is available
* The replacement expression is semantically straightforward

A code fix should not be automatically applied when:

* The usage occurs in a static method without clear injection path
* The type has many constructors and no obvious primary path
* The usage occurs in generated code
* The dependency is security-sensitive
* The replacement requires broad architectural changes

### 11.4 Fix-all Support

Support fix-all only for narrow categories:

* Clock
* GUID generation
* Simple environment reader usage

Avoid fix-all for:

* File system
* HTTP
* Database connections
* Vendor SDKs
* Process execution

Those changes often require intent, not just syntax.

---

## 12. Diagnostics Design

### 12.1 Diagnostic ID Scheme

Use stable diagnostic IDs.

```text
INSHIM001 Direct system clock usage
INSHIM002 Direct GUID generation
INSHIM003 Direct randomness usage
INSHIM004 Direct file system usage
INSHIM005 Direct environment usage
INSHIM006 Direct HttpClient construction
INSHIM007 Direct process execution
INSHIM008 Blocking sleep detected
INSHIM009 Direct console I/O usage
INSHIM010 Direct database connection construction
INSHIM011 Direct vendor SDK construction
INSHIM012 Shim generated but not registered
INSHIM013 Shim generated but unused
INSHIM014 Direct dependency usage violates configured layer policy
INSHIM015 Generated shim conflicts with existing type
```

### 12.2 Diagnostic Message Format

Diagnostics should be direct and actionable.

Example:

```text
INSHIM001: Direct system clock usage detected.
Use IClock instead of DateTimeOffset.UtcNow so time can be controlled in tests.
```

### 12.3 Diagnostic Properties

Each diagnostic should include:

* Category
* Suggested shim
* Safety level
* Code fix availability
* Project layer if known
* Documentation URL
* Whether baseline suppression is active

---

## 13. Generated Shim Designs

### 13.1 Clock Shim

Generated abstraction:

```csharp
public interface IClock
{
    DateTimeOffset UtcNow { get; }
    DateTimeOffset Now { get; }
}
```

Generated implementation:

```csharp
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    public DateTimeOffset Now => DateTimeOffset.Now;
}
```

Generated fake:

```csharp
public sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; private set; }
    public DateTimeOffset Now => UtcNow.ToLocalTime();

    public FakeClock(DateTimeOffset? utcNow = null)
    {
        UtcNow = utcNow ?? DateTimeOffset.UnixEpoch;
    }

    public void SetUtcNow(DateTimeOffset value)
    {
        UtcNow = value;
    }

    public void Advance(TimeSpan value)
    {
        UtcNow = UtcNow.Add(value);
    }
}
```

### 13.2 GUID Shim

```csharp
public interface IGuidGenerator
{
    Guid NewGuid();
}

public sealed class GuidGenerator : IGuidGenerator
{
    public Guid NewGuid() => Guid.NewGuid();
}
```

Fake:

```csharp
public sealed class FakeGuidGenerator : IGuidGenerator
{
    private readonly Queue<Guid> _values = new();

    public FakeGuidGenerator Enqueue(Guid value)
    {
        _values.Enqueue(value);
        return this;
    }

    public Guid NewGuid()
    {
        if (_values.Count == 0)
            throw new InvalidOperationException("No fake GUID values were configured.");

        return _values.Dequeue();
    }
}
```

### 13.3 Environment Shim

```csharp
public interface IEnvironmentReader
{
    string? GetEnvironmentVariable(string variable);
    string MachineName { get; }
    string CurrentDirectory { get; }
}
```

### 13.4 File System Shim

File system shims can become large. The MVP should generate a focused facade rather than clone all `System.IO` APIs.

Recommended MVP abstraction:

```csharp
public interface IFileSystem
{
    bool FileExists(string path);
    string ReadAllText(string path);
    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);
    void WriteAllText(string path, string contents);
    Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default);
    IEnumerable<string> EnumerateFiles(string path, string searchPattern = "*");
}
```

Future option: integrate with or generate adapters for `System.IO.Abstractions`.

### 13.5 Delay Shim

```csharp
public interface IDelayProvider
{
    Task Delay(TimeSpan delay, CancellationToken cancellationToken = default);
}

public sealed class SystemDelayProvider : IDelayProvider
{
    public Task Delay(TimeSpan delay, CancellationToken cancellationToken = default)
        => Task.Delay(delay, cancellationToken);
}
```

This helps replace direct `Task.Delay` and `Thread.Sleep` in testable workflows.

### 13.6 Process Runner Shim

```csharp
public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default);
}
```

This category should be generated but not aggressively auto-applied.

---

## 14. Dependency Injection Generation

### 14.1 Generated DI Extension

When enabled, the generator emits:

```csharp
public static class InshiminatorGeneratedServiceCollectionExtensions
{
    public static IServiceCollection AddInshiminatorGeneratedShims(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IGuidGenerator, GuidGenerator>();
        services.AddSingleton<IEnvironmentReader, SystemEnvironmentReader>();
        services.AddSingleton<IDelayProvider, SystemDelayProvider>();
        return services;
    }
}
```

### 14.2 DI Detection Analyzer

Analyzer should detect when a generated shim exists but is not registered in known DI startup flows.

Possible diagnostic:

```text
INSHIM012 Generated shim IClock does not appear to be registered in IServiceCollection.
```

This diagnostic should be informational by default because DI registration can be dynamic.

### 14.3 Host Integration

Support common patterns:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddInshiminatorGeneratedShims();
```

```csharp
services.AddInshiminatorGeneratedShims();
```

---

## 15. CLI Role in an Analyzer/Generator Architecture

The CLI still matters, but it should not be the center of the product.

### 15.1 CLI Responsibilities

The CLI should:

* Run project-wide analysis
* Generate reports
* Create and update baselines
* Produce architecture summaries
* Export SARIF
* Support CI guard mode
* Help initialize configuration
* Materialize generated code if requested

### 15.2 CLI Commands

```bash
inshim init
inshim analyze ./src
inshim report ./src --format markdown
inshim baseline create ./src
inshim guard ./src --baseline inshim.baseline.json
inshim doctor
```

### 15.3 CLI Should Not Own Refactoring

The CLI should not be the main refactoring engine. Refactorings should live in analyzers and code fixes so they work inside IDEs and normal builds.

---

## 16. Baseline and Guard Strategy

### 16.1 Why Baselines Matter

Brownfield codebases may have hundreds or thousands of existing direct dependency usages. Blocking all violations on day one guarantees adoption will die in a Jira comment thread.

Baselines allow teams to say:

> We know the old violations exist. Do not allow new ones.

### 16.2 Baseline File

Example:

```json
{
  "version": 1,
  "createdAt": "2026-05-07T00:00:00Z",
  "violations": [
    {
      "id": "INSHIM001",
      "file": "src/MyApp/Orders/OrderService.cs",
      "line": 42,
      "category": "clock",
      "fingerprint": "abc123"
    }
  ]
}
```

### 16.3 Fingerprinting

Violation fingerprints should be stable enough to survive line movement where possible.

Fingerprint inputs may include:

* Diagnostic ID
* Symbol name
* Containing type
* Containing member
* Syntax kind
* Normalized expression
* File path

Avoid line number as the primary identity.

### 16.4 Guard Modes

```bash
inshim guard --mode warn
inshim guard --mode fail-on-new
inshim guard --mode fail-on-any
inshim guard --mode ratchet
```

Ratchet mode should fail if the total violation count increases and pass if it decreases or remains the same.

---

## 17. Reporting

### 17.1 Report Formats

Required:

* Console
* JSON
* Markdown
* SARIF

Future:

* HTML
* Mermaid graphs
* DocFX integration
* GitHub PR comments
* Azure DevOps annotations

### 17.2 Markdown Report Structure

```markdown
# Inshiminator Boundary Report

## Summary

- Total findings: 128
- New findings: 3
- Baseline findings: 125
- Shim coverage: 34%

## Findings by Category

| Category | Findings | Shimmed | Coverage |
|---|---:|---:|---:|
| Clock | 22 | 7 | 24% |
| File system | 41 | 3 | 7% |
| HTTP | 8 | 4 | 50% |

## Highest Risk Findings

...

## Recommended Next Shims

...
```

### 17.3 Shim Coverage Report

Shim coverage should help teams measure progress.

Example:

```text
Clock
- Direct usages: 22
- Shimmed usages: 17
- Coverage: 43%
- Recommended next action: Apply clock shim code fix in Application project
```

---

## 18. Project and Layer Awareness

### 18.1 Why Layer Awareness Matters

A direct file read inside an infrastructure adapter may be fine. A direct file read inside a domain service is a smell wearing a trench coat.

Severity should depend on where usage occurs.

### 18.2 Layer Detection

Inshiminator should infer layers from:

* Project name
* Assembly name
* Namespace
* Folder path
* Configuration
* Attributes
* MSBuild properties

Examples:

```text
*.Domain
*.Application
*.Infrastructure
*.Web
*.Api
*.Worker
*.Tests
```

### 18.3 Layer Policy Example

```json
{
  "layers": {
    "domain": {
      "projects": ["**/*.Domain.csproj"],
      "disallowedCategories": ["filesystem", "environment", "http", "database", "process"]
    },
    "application": {
      "projects": ["**/*.Application.csproj"],
      "disallowedCategories": ["filesystem", "environment", "process"]
    },
    "infrastructure": {
      "projects": ["**/*.Infrastructure.csproj"],
      "allowedCategories": ["filesystem", "environment", "http", "database", "process"]
    }
  }
}
```

---

## 19. Extensibility Model

### 19.1 Shim Provider Interface

Future extensibility should allow custom providers.

Conceptual shape:

```csharp
public interface IShimProvider
{
    string Category { get; }
    ImmutableArray<DiagnosticDescriptor> Diagnostics { get; }
    void Analyze(ShimAnalysisContext context);
    void Generate(ShimGenerationContext context);
}
```

Roslyn analyzers and generators have their own constraints, so the actual implementation may require separate analyzer and generator extension points.

### 19.2 Provider Types

* Analyzer provider
* Generator provider
* Code fix provider
* Report provider
* Policy provider
* Template provider

### 19.3 Distribution

Providers should be distributable as NuGet packages.

Example:

```text
Inshiminator.Provider.AzureStorage
Inshiminator.Provider.Stripe
Inshiminator.Provider.SendGrid
Inshiminator.Provider.AmazonS3
Inshiminator.Provider.LegacySoap
```

---

## 20. Vendor SDK Shim Future

### 20.1 Problem

Vendor SDKs often enter application code directly:

```csharp
var client = new StripeClient(apiKey);
await client.Charges.CreateAsync(...);
```

This creates coupling, test complexity, and migration pain.

### 20.2 Future Capability

Inshiminator could generate vendor-specific gateway wrappers:

```csharp
public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken cancellationToken = default);
}
```

But this should not be blindly generated from every SDK method. Useful vendor shims require domain intent.

### 20.3 Recommended Approach

Start with detection and scaffolding:

```text
Detected direct StripeClient usage.
Suggested action: Create a payment gateway boundary.
Generated scaffold available.
```

Let the developer choose the domain-specific shape.

---

## 21. Runtime Record/Replay Future

### 21.1 Concept

Once calls are routed through shims, Inshiminator can support runtime record/replay.

Example:

```bash
inshim record -- dotnet test
inshim replay -- dotnet test
```

### 21.2 Use Cases

* Record HTTP responses
* Replay vendor API calls
* Capture file reads
* Capture message queue interactions
* Make integration tests deterministic
* Debug production-like workflows locally

### 21.3 Requirement

Runtime record/replay depends on shims existing first. That means the analyzer/generator foundation is the right first move.

---

## 22. Developer Experience Examples

### 22.1 Install

```bash
dotnet add package Inshiminator.Analyzers
dotnet add package Inshiminator.Generators
dotnet add package Inshiminator.DependencyInjection
```

### 22.2 Configure

```xml
<PropertyGroup>
  <InshiminatorGenerateClock>true</InshiminatorGenerateClock>
  <InshiminatorGenerateGuid>true</InshiminatorGenerateGuid>
  <InshiminatorGenerateDependencyInjection>true</InshiminatorGenerateDependencyInjection>
</PropertyGroup>
```

### 22.3 Build

```bash
dotnet build
```

### 22.4 See Diagnostic

```text
INSHIM001 Direct system clock usage detected.
Use IClock instead of DateTimeOffset.UtcNow.
```

### 22.5 Apply Code Fix

IDE action:

```text
Inject IClock and replace DateTimeOffset.UtcNow
```

### 22.6 Register Generated Shims

```csharp
builder.Services.AddInshiminatorGeneratedShims();
```

### 22.7 Test

```csharp
var clock = new FakeClock(DateTimeOffset.Parse("2026-05-07T12:00:00Z"));
var service = new OrderService(clock);
```

---

## 23. MVP Implementation Plan

### Phase 0: Research Spike

Goals:

* Validate Roslyn analyzer/generator architecture
* Confirm code fix feasibility for constructor injection
* Confirm config options through `.editorconfig` and MSBuild properties
* Confirm generated DI extension strategy

Deliverables:

* Minimal analyzer detecting `DateTimeOffset.UtcNow`
* Minimal generator emitting `IClock` and `SystemClock`
* Minimal code fix replacing one usage in a simple class
* Test project validating analyzer and generator behavior

Acceptance criteria:

* Analyzer emits `INSHIM001` for direct clock usage
* Generator emits compiling clock shim code
* Code fix updates a simple constructor-injected class
* Analyzer does not report diagnostics in generated code
* Tests verify all of the above

### Phase 1: Analyzer Foundation

Build analyzers for MVP categories:

* Clock
* GUID
* Random
* Environment
* File system
* HTTP construction
* Process execution
* Blocking sleep

Acceptance criteria:

* Each category has a stable diagnostic ID
* Each diagnostic has documentation metadata
* Analyzer tests cover positive and negative cases
* Generated code is ignored
* Severity can be configured

### Phase 2: Generator Foundation

Build incremental generator for common shims:

* Clock
* GUID generator
* Random source
* Environment reader
* Delay provider
* Process runner scaffold
* DI extension generation

Acceptance criteria:

* Generator supports MSBuild property configuration
* Generator supports `.editorconfig` configuration where appropriate
* Generated code is deterministic
* Generated code compiles under nullable enabled
* Generated code supports netstandard2.0-compatible output where possible
* Tests validate generated source snapshots

### Phase 3: Code Fixes

Build conservative code fixes:

* Clock replacement
* GUID replacement
* Delay provider replacement for simple `Task.Delay`
* Environment variable replacement for simple reads

Acceptance criteria:

* Code fixes work in simple constructor-injected classes
* Code fixes avoid static classes unless explicitly supported
* Code fixes add fields and constructor parameters safely
* Fix-all is supported only for safe categories
* Risky changes produce diagnostics without fix-all

### Phase 4: CLI and Reports

Build CLI for project-wide use:

```bash
inshim analyze
inshim report
inshim baseline create
inshim guard
inshim doctor
```

Acceptance criteria:

* CLI can analyze a solution
* CLI can emit JSON report
* CLI can emit Markdown report
* CLI can emit SARIF report
* CLI can create a baseline
* CLI can fail on new violations

### Phase 5: Policy Packs

Build default and Clean Architecture policy packs.

Acceptance criteria:

* Default pack works for general projects
* Clean Architecture pack treats Domain/Application/Infrastructure differently
* Policy can be applied through MSBuild or config file
* CI guard respects policy pack severity

### Phase 6: Documentation and Samples

Build docs and examples:

* README
* Getting started
* Analyzer rule docs
* Generator configuration docs
* Code fix examples
* CI examples
* Clean Architecture sample
* Legacy app sample
* Minimal API sample

Acceptance criteria:

* Docs include before/after examples
* Each diagnostic has documentation
* Samples compile and test in CI
* README clearly explains analyzer/generator workflow

---

## 24. Testing Strategy

### 24.1 Analyzer Tests

Use Roslyn analyzer testing packages.

Test cases:

* Direct usage detected
* Allowed usage ignored
* Generated code ignored
* Severity config respected
* Layer policy respected
* Suppression works

### 24.2 Generator Tests

Test cases:

* Generated source matches snapshot
* Generated source compiles
* MSBuild properties affect output
* `.editorconfig` options affect output
* Nullable output is correct
* Duplicate generation is avoided

### 24.3 Code Fix Tests

Test cases:

* Before/after source transformation
* Constructor injection added
* Existing constructor updated
* Existing field naming conflicts handled
* Unsafe contexts are not fixed
* Fix-all behavior is constrained

### 24.4 CLI Tests

Test cases:

* Analyze sample solution
* Emit report formats
* Create baseline
* Guard new violation
* Ratchet mode
* Exit codes

### 24.5 Integration Tests

Create sample apps:

```text
samples/MinimalApiWithClock
samples/CleanArchitectureApp
samples/LegacyConsoleApp
samples/WorkerServiceWithDelays
samples/HttpClientUsageApp
```

Each sample should validate realistic usage.

---

## 25. Acceptance Criteria

### 25.1 Analyzer Acceptance Criteria

* Inshiminator detects direct system clock usage.
* Inshiminator detects direct GUID generation.
* Inshiminator detects direct randomness usage.
* Inshiminator detects direct file system usage.
* Inshiminator detects direct environment variable usage.
* Inshiminator detects direct `new HttpClient()` usage.
* Inshiminator detects direct process execution.
* Inshiminator detects blocking sleeps.
* Inshiminator suppresses diagnostics for generated code.
* Inshiminator diagnostics include actionable messages.
* Inshiminator diagnostics include stable IDs.
* Inshiminator severities are configurable.

### 25.2 Generator Acceptance Criteria

* Inshiminator generates clock shims.
* Inshiminator generates GUID shims.
* Inshiminator generates random source shims.
* Inshiminator generates environment reader shims.
* Inshiminator generates delay provider shims.
* Inshiminator generates DI registration extensions.
* Generated code compiles without manual edits.
* Generated code is deterministic.
* Generated code supports nullable reference types.
* Generated code avoids duplicate type conflicts where possible.

### 25.3 Code Fix Acceptance Criteria

* Inshiminator can replace simple `DateTimeOffset.UtcNow` usage with `IClock.UtcNow`.
* Inshiminator can replace simple `Guid.NewGuid()` usage with `IGuidGenerator.NewGuid()`.
* Inshiminator can add constructor parameters for simple injectable classes.
* Inshiminator does not apply unsafe transformations automatically.
* Inshiminator supports fix-all only for safe categories.

### 25.4 CLI Acceptance Criteria

* Inshiminator CLI can analyze a solution.
* Inshiminator CLI can generate JSON reports.
* Inshiminator CLI can generate Markdown reports.
* Inshiminator CLI can generate SARIF reports.
* Inshiminator CLI can create a baseline.
* Inshiminator CLI can fail CI on new violations.
* Inshiminator CLI can run in warning-only mode.

### 25.5 Governance Acceptance Criteria

* Existing violations can be baselined.
* New violations can fail CI.
* Violation counts can be ratcheted downward.
* Policy packs can alter severity by layer.
* Reports show category counts and shim coverage.

---

## 26. Risks and Mitigations

### 26.1 Risk: Source Generators Cannot Modify Existing Code

Source generators can only add code. They cannot change call sites.

Mitigation:

* Use analyzers and code fixes for call-site changes.
* Use source generators for boundary code.
* Use CLI reporting for larger migration plans.

### 26.2 Risk: Constructor Injection Refactors Get Complicated

Updating constructors is easy in simple classes and messy in real applications.

Mitigation:

* Keep code fixes conservative.
* Support safe cases first.
* Emit guidance for complex cases.
* Avoid pretending every refactor is mechanical.

### 26.3 Risk: Generated Interfaces Become Opinionated

Teams may already have their own abstractions.

Mitigation:

* Allow using existing types.
* Allow namespace/type customization.
* Support generated and external abstraction modes.
* Support suppression and mapping configuration.

### 26.4 Risk: File System Shim Scope Explodes

`System.IO` is large.

Mitigation:

* Generate focused facades for common operations.
* Support integration with existing libraries later.
* Do not attempt full API parity in MVP.

### 26.5 Risk: Vendor SDK Shims Need Domain Intent

Automatically wrapping every SDK method can produce useless abstractions.

Mitigation:

* Detect and scaffold first.
* Let developers define domain gateway shape.
* Add provider-specific packages only after clear use cases.

### 26.6 Risk: Analyzer Noise Causes Rejection

Too many warnings can make developers ignore the tool.

Mitigation:

* Start with warnings and baselines.
* Support layer-aware severity.
* Provide clear messages.
* Make rules easy to configure.

---

## 27. Recommended Repository Structure

```text
/src
  /Inshiminator.Abstractions
  /Inshiminator.Analyzers
  /Inshiminator.Generators
  /Inshiminator.CodeFixes
  /Inshiminator.Testing
  /Inshiminator.DependencyInjection
  /Inshiminator.Cli
  /Inshiminator.MSBuild
  /Inshiminator.PolicyPacks.Default
  /Inshiminator.PolicyPacks.CleanArchitecture
/tests
  /Inshiminator.Analyzers.Tests
  /Inshiminator.Generators.Tests
  /Inshiminator.CodeFixes.Tests
  /Inshiminator.Cli.Tests
  /Inshiminator.IntegrationTests
/samples
  /MinimalApiWithClock
  /CleanArchitectureApp
  /LegacyConsoleApp
  /WorkerServiceWithDelays
  /HttpClientUsageApp
/docs
  /rules
  /getting-started
  /configuration
  /samples
```

---

## 28. Initial Rule Set

| ID        | Name                                    | Default Severity | Code Fix | Generator Support |
| --------- | --------------------------------------- | ---------------: | -------: | ----------------: |
| INSHIM001 | Direct system clock usage               |          Warning |      Yes |               Yes |
| INSHIM002 | Direct GUID generation                  |          Warning |      Yes |               Yes |
| INSHIM003 | Direct randomness usage                 |          Warning |  Partial |               Yes |
| INSHIM004 | Direct file system usage                |          Warning |   No MVP |               Yes |
| INSHIM005 | Direct environment usage                |          Warning |  Partial |               Yes |
| INSHIM006 | Direct `HttpClient` construction        |            Error |  Partial |           Partial |
| INSHIM007 | Direct process execution                |          Warning |   No MVP |               Yes |
| INSHIM008 | Blocking sleep detected                 |          Warning |  Partial |               Yes |
| INSHIM009 | Direct console I/O usage                |             Info |   No MVP |            Future |
| INSHIM010 | Direct database connection construction |             Info |   No MVP |            Future |
| INSHIM011 | Direct vendor SDK construction          |             Info |   No MVP |            Future |
| INSHIM012 | Generated shim not registered           |             Info |      Yes |               Yes |
| INSHIM013 | Generated shim unused                   |             Info |       No |               Yes |
| INSHIM014 | Layer policy violation                  |     Configurable |       No |               N/A |
| INSHIM015 | Generated shim type conflict            |            Error |       No |               Yes |

---

## 29. Naming and Branding

### 29.1 Product Name

```text
Inshiminator
```

### 29.2 CLI Name

```text
inshim
```

### 29.3 Taglines

Serious:

> Generated dependency boundaries for testable .NET systems.

Funny:

> Add seams to code that was born without them.

More direct:

> Find hard dependencies. Generate shims. Govern the boundary.

Darkly accurate:

> Your code has been touching infrastructure unsupervised.

### 29.4 README Opening

```markdown
# Inshiminator

Inshiminator is an analyzer-guided shim generation toolkit for .NET.
It finds direct dependency usage like `DateTime.UtcNow`, `Guid.NewGuid()`, `File.ReadAllText`, and `new HttpClient()`, then generates the shims, fakes, and diagnostics needed to make those dependencies testable and governable.
```

---

## 30. Final Recommendation

The best version of Inshiminator is not a one-shot CLI that rewrites code. It is a compiler-integrated boundary system.

Use:

* **Roslyn analyzers** to detect hard dependencies
* **Incremental source generators** to emit shims and fakes
* **Code fixes** to perform safe call-site migrations
* **MSBuild integration** to make it normal build behavior
* **CLI tooling** for reports, baselines, and CI governance

This gives the product a strong identity:

> Inshiminator is the tool that finds places where your app needs a shim, generates the shim, and keeps future code from bypassing it.

That is a real product. The name is still stupid. That is an asset.
