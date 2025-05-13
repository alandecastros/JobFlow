using System.Threading;
using System.Threading.Tasks;

namespace JobFlow.Core.Abstractions;

public interface IJobQueue
{
    Task<string> SubmitJobAsync<T>(T request, string? queue = null, CancellationToken ct = default)
        where T : notnull;
}
