using System;

namespace Inshiminator.Cli;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Inshiminator CLI - Analyzer-guided shim generation for .NET");
        Console.WriteLine("-----------------------------------------------------------");

        if (args.Length == 0)
        {
            PrintUsage();
            return;
        }

        var command = args[0].ToLowerInvariant();
        switch (command)
        {
            case "analyze":
                Console.WriteLine("Analyzing project...");
                // TODO: Implement Roslyn analysis
                break;
            case "baseline":
                Console.WriteLine("Creating baseline...");
                // TODO: Implement baselining
                break;
            case "report":
                Console.WriteLine("Generating report...");
                // TODO: Implement reporting
                break;
            case "doctor":
                Console.WriteLine("Checking system for Inshiminator compatibility...");
                break;
            default:
                Console.WriteLine($"Unknown command: {command}");
                PrintUsage();
                break;
        }
    }

    static void PrintUsage()
    {
        Console.WriteLine("Usage: inshim <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  analyze   Run project-wide analysis");
        Console.WriteLine("  baseline  Create or update a violation baseline");
        Console.WriteLine("  report    Generate findings reports (JSON, Markdown, SARIF)");
        Console.WriteLine("  doctor    Verify Inshiminator setup");
    }
}
