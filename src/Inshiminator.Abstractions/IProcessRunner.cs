using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Inshiminator.Abstractions;

public record ProcessRequest(
    string FileName,
    string Arguments = "",
    string? WorkingDirectory = null,
    IDictionary<string, string>? EnvironmentVariables = null);

public record ProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default);
}
