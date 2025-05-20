using System;
using System.Threading;
using System.Threading.Tasks;

namespace JobFlow.Core.Abstractions;

public interface IJobHandlerCaller
{
    Task<object?> CallHandler(
        Type messageType,
        object? payload,
        CancellationToken cancellationToken
    );
}
