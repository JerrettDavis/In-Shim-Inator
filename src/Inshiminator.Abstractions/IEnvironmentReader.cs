namespace Inshiminator.Abstractions;

public interface IEnvironmentReader
{
    string? GetEnvironmentVariable(string variable);
    string MachineName { get; }
    string CurrentDirectory { get; }
    string UserName { get; }
}
