# Inshiminator

## Analyzer-guided shim generation for .NET applications

Inshiminator is a .NET-first developer toolkit that uses **Roslyn analyzers, incremental source generators, and code fixes** to insert clean shims around hard dependencies without forcing teams into invasive rewrites.

### 🚀 Key Features

- **Detect:** Automatically finds direct usage of `DateTime.UtcNow`, `Guid.NewGuid()`, `File.ReadAllText`, and more.
- **Generate:** Emits strongly typed abstractions (`IClock`, `IGuidGenerator`) and implementations (`SystemClock`, `SystemGuidGenerator`) at compile time, while also supporting framework abstractions like `TimeProvider`.
- **Guide:** Provides IDE code fixes to automatically inject shims into your classes.
- **Govern:** Enforce boundary rules through analyzer severity and baselines.

### 📦 Installation

```bash
dotnet add package Inshiminator.Analyzers
dotnet add package Inshiminator.Generators
```

### 🛠️ Example

**Before:**
```csharp
public class OrderService
{
    public void CreateOrder()
    {
        var createdAt = DateTime.UtcNow; // ❌ Hard to test
    }
}
```

**After (Automatic via Code Fix):**
```csharp
public class OrderService
{
    private readonly IClock _clock;

    public OrderService(IClock clock)
    {
        _clock = clock;
    }

    public void CreateOrder()
    {
        var createdAt = _clock.UtcNow; // ✅ Fully testable
    }
}
```

### 📜 License

MIT
