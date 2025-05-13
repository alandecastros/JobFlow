using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace JobFlow.Core;

public class JobFlowWorkerOptions(IServiceCollection services)
{
    public IServiceCollection Services { get; } = services;
    public string? WorkerId { get; set; }
    public int? PollingIntervalInMilliseconds { get; set; }
    public Dictionary<string, JobFlowWorkerQueueOptions>? Queues { get; set; }
}
