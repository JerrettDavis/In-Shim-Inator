using System;

namespace Inshiminator.Abstractions;

public interface IGuidGenerator
{
    Guid NewGuid();
}
