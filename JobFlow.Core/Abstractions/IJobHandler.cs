using System.Threading;
using System.Threading.Tasks;

namespace JobFlow.Core.Abstractions;

public interface IJobHandler<in T>
    where T : IJob
{
    Task HandleJobAsync(string jobId, T message, CancellationToken cancellationToken);
}

public interface IJobHandler<in T, TR>
    where T : IJob<TR>
{
    Task<TR> HandleJobAsync(string jobId, T message, CancellationToken cancellationToken);
}
