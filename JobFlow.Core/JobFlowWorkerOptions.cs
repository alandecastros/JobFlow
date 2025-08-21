using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace JobFlow.Core;

public class JobFlowWorkerOptions
{
    public Assembly[] Assemblies { get; set; } =
        Assembly.GetEntryAssembly() is not null ? [Assembly.GetEntryAssembly()!] : [];
    public string? WorkerId { get; set; }
    public int? PollingIntervalInMilliseconds { get; set; }
    public Dictionary<string, JobFlowWorkerQueueOptions>? Queues { get; set; }
}
