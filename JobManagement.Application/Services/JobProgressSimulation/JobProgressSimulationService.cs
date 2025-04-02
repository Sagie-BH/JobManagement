using JobManagement.Application.Services.JobServices;
using JobManagement.Domain.Entities;
using JobManagement.Domain.Enums;
using JobManagement.Infrastructure.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JobManagement.Application.Services
{
    /// <summary>
    /// A service that simulates progress updates for existing jobs in the database
    /// </summary>
    public class JobProgressSimulationService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<JobProgressSimulationService> _logger;
        private readonly Random _random = new Random();
        private readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(5); // Update every 5 seconds

        public JobProgressSimulationService(
            IServiceProvider serviceProvider,
            ILogger<JobProgressSimulationService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Job Progress Simulation Service is starting");

            // Wait a bit on startup to allow the system to initialize
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await UpdateJobProgressAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Error in job progress simulation service");
                }

                await Task.Delay(_updateInterval, stoppingToken);
            }

            _logger.LogInformation("Job Progress Simulation Service is stopping");
        }

        private async Task UpdateJobProgressAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var jobService = scope.ServiceProvider.GetRequiredService<IJobService>();
            var jobSignalRService = scope.ServiceProvider.GetRequiredService<JobManagement.Infrastructure.Interfaces.SignalR.IJobSignalRService>();

            // Find jobs that can be updated (either in Pending or Running status)
            var pendingJobs = await unitOfWork.Repository<Job>().GetAsync(
                j => j.Status == JobStatus.Pending);

            var runningJobs = await unitOfWork.Repository<Job>().GetAsync(
                j => j.Status == JobStatus.Running);

            // For pending jobs, update them to running if they have a worker assigned
            foreach (var job in pendingJobs)
            {
                if (job.WorkerNodeId.HasValue && job.WorkerNodeId != Guid.Empty)
                {
                    _logger.LogInformation($"Moving job {job.Id} from Pending to Running status");
                    await jobService.UpdateJobStatusAsync(job.Id, JobStatus.Running);
                    await jobSignalRService.NotifyJobStatusChangedAsync(job.Id, JobStatus.Running.ToString());
                }
            }

            // For running jobs, update their progress based on worker power
            foreach (var job in runningJobs)
            {
                // Get the worker to determine power level
                WorkerNode worker = null;
                if (job.WorkerNodeId.HasValue && job.WorkerNodeId != Guid.Empty)
                {
                    worker = await unitOfWork.Repository<WorkerNode>().GetByIdAsync(job.WorkerNodeId.Value);
                }

                // Default to medium power (5) if worker not found
                int workerPower = worker?.Power ?? 5;

                // Calculate progress increment based on worker power and job priority
                int progressIncrement = CalculateProgressIncrement(job.Priority, workerPower);

                int newProgress = Math.Min(100, job.Progress + progressIncrement);

                _logger.LogInformation($"Updating job {job.Id} progress from {job.Progress}% to {newProgress}% (worker power: {workerPower})");

                // Update progress
                await jobService.UpdateJobProgressAsync(job.Id, newProgress);

                // Explicitly send SignalR notification
                await jobSignalRService.NotifyJobProgressUpdatedAsync(job.Id, newProgress);

                // If job has reached 100%, mark it as completed
                if (newProgress >= 100)
                {
                    _logger.LogInformation($"Job {job.Id} has completed (reached 100%)");
                    await jobService.UpdateJobStatusAsync(job.Id, JobStatus.Completed);
                    await jobSignalRService.NotifyJobStatusChangedAsync(job.Id, JobStatus.Completed.ToString());
                }
                // Small chance of failure for demonstration purposes
                // Higher chance for lower power workers
                else
                {
                    // Calculate failure chance: Power 1 = 10%, Power 10 = 1%
                    int failureChance = Math.Max(1, 11 - workerPower);

                    if (_random.Next(100) < failureChance)
                    {
                        _logger.LogInformation($"Simulating failure for job {job.Id} on worker with power {workerPower}");
                        await jobService.UpdateJobStatusAsync(job.Id, JobStatus.Failed);
                        await jobSignalRService.NotifyJobErrorAsync(job.Id, $"Simulated random failure during processing on worker with power {workerPower}");
                        await jobSignalRService.NotifyJobStatusChangedAsync(job.Id, JobStatus.Failed.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// Calculate progress increment based on worker power and job priority
        /// </summary>
        private int CalculateProgressIncrement(JobPriority priority, int workerPower)
        {
            // Base increment - higher power means faster progress
            // Power 1 = 5% base, Power 10 = 25% base
            int baseIncrement = 3 + (workerPower * 2);

            // Apply randomness (±20%)
            baseIncrement = (int)(baseIncrement * (0.8 + (_random.NextDouble() * 0.4)));

            // Apply priority modifier
            double priorityModifier = GetPriorityModifier(priority);

            return Math.Max(1, (int)(baseIncrement * priorityModifier));
        }

        /// <summary>
        /// Get a speed modifier based on job priority
        /// </summary>
        private double GetPriorityModifier(JobPriority priority)
        {
            return priority switch
            {
                JobPriority.Critical => 1.5,  // 50% faster
                JobPriority.Urgent => 1.3,    // 30% faster
                JobPriority.High => 1.2,      // 20% faster
                JobPriority.Regular => 1.0,   // normal speed
                JobPriority.Low => 0.8,       // 20% slower
                JobPriority.Deferred => 0.5,  // 50% slower
                _ => 1.0
            };
        }
    }
}