using System;
using Inshiminator.Abstractions;

namespace Inshiminator.Testing;

public sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; private set; }
    public DateTimeOffset Now => UtcNow.ToLocalTime();

    public FakeClock(DateTimeOffset? utcNow = null)
    {
        UtcNow = utcNow ?? new DateTimeOffset(2026, 5, 7, 0, 0, 0, TimeSpan.Zero);
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
