using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Inshiminator.Abstractions;

public interface IFileSystem
{
    bool FileExists(string path);
    string ReadAllText(string path);
    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);
    void WriteAllText(string path, string contents);
    Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default);
    IEnumerable<string> EnumerateFiles(string path, string searchPattern = "*");
}
