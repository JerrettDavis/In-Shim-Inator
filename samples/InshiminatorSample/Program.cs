using System;
// The generated shims are in Inshiminator.Generated namespace by default in my generator implementation
using Inshiminator.Generated;

namespace InshiminatorSample;

class Program
{
    static void Main(string[] args)
    {
        // This should trigger INSHIM001
        var now = DateTime.UtcNow;
        Console.WriteLine($"Current time: {now}");

        // This should trigger INSHIM002
        var guid = Guid.NewGuid();
        Console.WriteLine($"New GUID: {guid}");

        // Using shims (manually for now, code fix would do this in real usage)
        IClock clock = new SystemClock();
        Console.WriteLine($"Shim UtcNow: {clock.UtcNow}");

        IGuidGenerator guidGen = new SystemGuidGenerator();
        Console.WriteLine($"Shim GUID: {guidGen.NewGuid()}");
    }
}
