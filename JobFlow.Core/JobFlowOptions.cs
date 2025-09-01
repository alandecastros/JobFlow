using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace JobFlow.Core;

public class JobFlowOptions(IServiceCollection services)
{
    public IServiceCollection Services { get; } = services;
    public JobFlowWorkerOptions? Worker { get; set; }
}
