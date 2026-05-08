using System;
using System.Threading;
using System.Threading.Tasks;

namespace Inshiminator.Abstractions;

public interface IDelayProvider
{
    Task Delay(TimeSpan delay, CancellationToken cancellationToken = default);
}
