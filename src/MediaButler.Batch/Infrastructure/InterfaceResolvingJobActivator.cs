using Hangfire;
using MediaButler.Batch.Jobs.Batch;
using MediaButler.Core.Services;

namespace MediaButler.Batch.Infrastructure;

/// <summary>
/// Custom Hangfire job activator that resolves interface types to concrete implementations.
/// This allows the API to enqueue jobs by interface without circular dependency.
/// </summary>
public class InterfaceResolvingJobActivator : JobActivator
{
    private readonly IServiceProvider _serviceProvider;

    public InterfaceResolvingJobActivator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override object ActivateJob(Type jobType)
    {
        // Map interface to concrete implementation
        if (jobType == typeof(IBatchFileProcessor))
        {
            jobType = typeof(BatchFileProcessingJob);
        }

        // Use DI container to create the instance
        return _serviceProvider.GetService(jobType)
            ?? throw new InvalidOperationException($"Failed to resolve job type: {jobType.Name}");
    }

    public override JobActivatorScope BeginScope(JobActivatorContext context)
    {
        return new InterfaceResolvingJobActivatorScope(_serviceProvider);
    }

    private class InterfaceResolvingJobActivatorScope : JobActivatorScope
    {
        private readonly IServiceScope _scope;

        public InterfaceResolvingJobActivatorScope(IServiceProvider serviceProvider)
        {
            _scope = serviceProvider.CreateScope();
        }

        public override object Resolve(Type type)
        {
            // Map interface to concrete implementation
            if (type == typeof(IBatchFileProcessor))
            {
                type = typeof(BatchFileProcessingJob);
            }

            return _scope.ServiceProvider.GetService(type)
                ?? throw new InvalidOperationException($"Failed to resolve job type: {type.Name}");
        }

        public override void DisposeScope()
        {
            _scope.Dispose();
        }
    }
}
