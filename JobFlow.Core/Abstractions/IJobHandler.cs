using System.Threading;
using System.Threading.Tasks;

namespace JobFlow.Core.Abstractions;

public interface IJobHandler<in T>
    where T : IJob
{
    Task ExecuteAsync(T message, CancellationToken cancellationToken);
}

public interface IJobHandler<in T, TR>
    where T : IJob<TR>
{
    Task<TR> ExecuteAsync(T message, CancellationToken cancellationToken);
}
