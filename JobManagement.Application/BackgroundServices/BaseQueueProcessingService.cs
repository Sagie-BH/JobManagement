using JobManagement.Domain.Entities;
using JobManagement.Infrastructure.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace JobManagement.Infrastructure.BackgroundServices
{
    public abstract class BaseQueueProcessingService : BackgroundService
    {
        protected readonly ILogger<BaseQueueProcessingService> _logger;
        protected readonly IServiceProvider _serviceProvider;
        protected readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(5);
        protected readonly ConcurrentDictionary<Guid, CancellationTokenSource> _runningJobs = new();

        protected BaseQueueProcessingService(
            IServiceProvider serviceProvider,
            ILogger<BaseQueueProcessingService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Queue Processing Service is starting");

            // Restore queue state on startup
            using (var scope = _serviceProvider.CreateScope())
            {
                var jobQueue = scope.ServiceProvider.GetRequiredService<IJobQueue>();
                await jobQueue.RestoreQueueStateAsync();
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessQueueAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while processing the job queue");
                }

                await Task.Delay(_pollingInterval, stoppingToken);
            }

            // Cancel all running jobs when the service is stopping
            foreach (var jobCts in _runningJobs.Values)
            {
                jobCts.Cancel();
            }

            _logger.LogInformation("Queue Processing Service is stopping");
        }

        // Abstract method to be implemented by derived classes
        protected abstract Task ProcessQueueAsync(CancellationToken stoppingToken);

        public async Task StopJobAsync(Guid jobId)
        {
            if (_runningJobs.TryGetValue(jobId, out var cts))
            {
                _logger.LogInformation($"Cancelling job {jobId}");
                cts.Cancel();
            }
        }

        protected async Task<bool> TryExecuteJobAsync(Job job, CancellationToken stoppingToken)
        {
            if (job == null)
            {
                return false;
            }

            using var scope = _serviceProvider.CreateScope();
            var jobExecutionService = scope.ServiceProvider.GetRequiredService<IJobExecutionService>();

            // Create a linked token source for this job
            var jobCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            _runningJobs[job.Id] = jobCts;

            try
            {
                // Execute the job asynchronously
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await jobExecutionService.ExecuteJobAsync(job, jobCts.Token);
                    }
                    finally
                    {
                        // Clean up after execution
                        _runningJobs.TryRemove(job.Id, out _);
                        jobCts.Dispose();
                    }
                }, stoppingToken);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error starting job {job.Id} execution");
                _runningJobs.TryRemove(job.Id, out _);
                jobCts.Dispose();

                // Error handling should be done by the caller
                return false;
            }
        }
    }
}