using JobManagement.Domain.Entities;
using JobManagement.Domain.Interfaces;
using JobManagement.Infrastructure.BackgroundServices;
using JobManagement.Infrastructure.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace JobManagement.Application.BackgroundServices
{
    public class QueueProcessingService : BaseQueueProcessingService
    {
        public QueueProcessingService(
            IServiceProvider serviceProvider,
            ILogger<BaseQueueProcessingService> logger)
            : base(serviceProvider, logger)
        {
        }

        protected override async Task ProcessQueueAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var jobQueue = scope.ServiceProvider.GetRequiredService<IJobQueue>();
            var workerAssignmentService = scope.ServiceProvider.GetRequiredService<IWorkerAssignmentService>();

            // Dequeue a job
            var job = await jobQueue.DequeueAsync();
            if (job == null)
            {
                return; // No jobs in queue
            }

            // Check if job is scheduled for the future
            if (job.ScheduledStartTime.HasValue && job.ScheduledStartTime > DateTime.UtcNow)
            {
                _logger.LogInformation($"Job {job.Id} is scheduled for future execution at {job.ScheduledStartTime}");
                // Re-queue for later processing
                await jobQueue.EnqueueAsync(job);
                return;
            }

            // Try to assign the job to a worker
            bool assigned = await workerAssignmentService.TryAssignJobToWorkerAsync(job);
            if (!assigned)
            {
                _logger.LogWarning($"Could not assign job {job.Id} to any worker. Requeuing for later processing.");
                // Re-queue for later assignment
                await jobQueue.EnqueueAsync(job);
                return;
            }

            // Execute the job
            bool executionStarted = await TryExecuteJobAsync(job, stoppingToken);
            if (!executionStarted)
            {
                _logger.LogError($"Failed to start execution for job {job.Id}. Requeuing.");
                await jobQueue.EnqueueAsync(job);
            }
        }
    }
}