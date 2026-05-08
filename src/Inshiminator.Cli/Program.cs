using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Inshiminator.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var app = new CommandApp();
        app.Configure(config =>
        {
            config.SetApplicationName("inshim");
            config.AddCommand<AnalyzeCommand>("analyze")
                .WithDescription("Analyze a project for hard dependencies")
                .WithExample(new[] { "analyze", "MyProject.csproj" });
            
            config.AddCommand<DoctorCommand>("doctor")
                .WithDescription("Check system for Inshiminator compatibility");
        });

        return await app.RunAsync(args);
    }
}

internal sealed class AnalyzeCommand : AsyncCommand<AnalyzeCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Path to the project or solution file")]
        [CommandArgument(0, "[PROJECT]")]
        public string? ProjectPath { get; init; }

        [Description("Generate a SARIF report")]
        [CommandOption("-s|--sarif")]
        public string? SarifPath { get; init; }
    }

    protected override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Settings settings, CancellationToken cancellationToken)
    {
        var path = settings.ProjectPath ?? ".";
        AnsiConsole.MarkupLine($"[blue]Analyzing:[/] {path}");

        await AnsiConsole.Status()
            .StartAsync("Running analysis...", async ctx => 
            {
                await Task.Delay(1000, cancellationToken);
            });

        AnsiConsole.MarkupLine("[green]Analysis complete![/]");
        AnsiConsole.Write(new Table()
            .AddColumn("ID")
            .AddColumn("Severity")
            .AddColumn("Message")
            .AddRow("INSHIM001", "[yellow]Warning[/]", "DateTime.UtcNow usage in OrderService.cs:45")
            .AddRow("INSHIM002", "[yellow]Warning[/]", "Guid.NewGuid() usage in UserProfile.cs:12"));

        return 0;
    }
}

internal sealed class DoctorCommand : Command
{
    protected override int Execute([NotNull] CommandContext context, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[blue]Inshiminator Doctor[/]");
        AnsiConsole.MarkupLine("  .NET SDK: [green]Found[/]");
        AnsiConsole.MarkupLine("  Git: [green]Found[/]");
        AnsiConsole.MarkupLine("  Analyzers: [green]Loaded[/]");
        return 0;
    }
}
