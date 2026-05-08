using System;
using System.Collections.Generic;
using Inshiminator.Abstractions;

namespace Inshiminator.Testing;

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
            return Guid.NewGuid();

        return _values.Dequeue();
    }
}
