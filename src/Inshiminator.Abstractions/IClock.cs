using System;

namespace Inshiminator.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
    DateTimeOffset Now { get; }
}
