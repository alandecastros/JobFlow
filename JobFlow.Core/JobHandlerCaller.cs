using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JobFlow.Core.Abstractions;
using JobFlow.Core.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace JobFlow.Core;

public class JobHandlerCaller(IServiceProvider serviceProvider) : IJobHandlerCaller
{
    public async Task<object?> CallHandler(
        Type messageType,
        object? payload,
        CancellationToken cancellationToken
    )
    {
        using var scope = serviceProvider.CreateScope();

        var jobRequestWithOutputInterface = messageType
            .GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IJob<>));

        if (jobRequestWithOutputInterface != null)
        {
            var returnType = jobRequestWithOutputInterface.GetGenericArguments()[0];
            var genericHandlerType = typeof(IJobHandler<,>);
            var specificHandlerType = genericHandlerType.MakeGenericType(messageType, returnType);
            var handler = scope.ServiceProvider.GetRequiredService(specificHandlerType);

            var executeMethod = handler.GetType().GetMethod("ExecuteAsync");

            // The result of Invoke will be Task<TR>
            var taskResult = executeMethod!.Invoke(handler, [payload, cancellationToken]);

            var result = await (dynamic)taskResult!;

            return result;
        }
        else
        {
            var genericHandlerType = typeof(IJobHandler<>);
            var specificHandlerType = genericHandlerType.MakeGenericType(messageType);
            var handler = scope.ServiceProvider.GetRequiredService(specificHandlerType);

            var executeMethod = handler.GetType().GetMethod("ExecuteAsync");

            var taskResult = (Task?)executeMethod!.Invoke(handler, [payload, cancellationToken]);

            await taskResult!;

            return null;
        }
    }
}
