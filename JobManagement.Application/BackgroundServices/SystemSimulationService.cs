using JobManagement.Application.Services.JobServices;
using JobManagement.Application.Services.WorkerNodes;
using JobManagement.Domain.Constants;
using JobManagement.Domain.Entities;
using JobManagement.Domain.Enums;
using JobManagement.Infrastructure.Interfaces;
using JobManagement.Infrastructure.Interfaces.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JobManagement.Application.BackgroundServices
{
    public class SystemSimulationService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SystemSimulationService> _logger;
        private readonly Random _random = new Random();
        // Shorter update interval for more frequent updates
        private readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(5);
        // Different update intervals for different types of updates
        private readonly TimeSpan _jobProgressInterval = TimeSpan.FromSeconds(3);
        private readonly TimeSpan _workerStatusInterval = TimeSpan.FromSeconds(15);
        private readonly TimeSpan _systemNotificationInterval = TimeSpan.FromSeconds(30);

        // Tracking when each type of update was last performed
        private DateTime _lastJobProgressUpdate = DateTime.MinValue;
        private DateTime _lastWorkerStatusUpdate = DateTime.MinValue;
        private DateTime _lastSystemNotification = DateTime.MinValue;

        // System messages for random notifications
        private readonly string[] _systemMessages = new[] {
            "System performing routine maintenance...",
            "Network performance optimized",
            "Memory usage optimized",
            "Database connection pool refreshed",
            "Worker heartbeat monitoring active",
            "System health check completed successfully",
            "Queue optimization routine executed",
            "Job priority rebalancing performed",
            "Worker load balancing completed",
            "System metrics updated"
        };

        public SystemSimulationService(
            IServiceProvider serviceProvider,
            ILogger<SystemSimulationService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Enhanced System Simulation Service is starting");

            // Wait a bit for system initialization
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.UtcNow;
                    await using var scope = _serviceProvider.CreateAsyncScope();

                    // Update running jobs progress at frequent intervals
                    if (now - _lastJobProgressUpdate >= _jobProgressInterval)
                    {
                        await UpdateJobProgressAsync(scope, stoppingToken);
                        _lastJobProgressUpdate = now;
                    }

                    // Simulate worker state changes less frequently
                    if (now - _lastWorkerStatusUpdate >= _workerStatusInterval)
                    {
                        await SimulateWorkerStatusChangesAsync(scope, stoppingToken);
                        _lastWorkerStatusUpdate = now;
                    }

                    // Send random system notifications periodically
                    if (now - _lastSystemNotification >= _systemNotificationInterval)
                    {
                        await SendSystemNotificationAsync(scope, stoppingToken);
                        _lastSystemNotification = now;
                    }

                    // Process some pending jobs if needed
                    await ProcessSomePendingJobsAsync(scope, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in enhanced system simulation service");
                }

                await Task.Delay(_updateInterval, stoppingToken);
            }

            _logger.LogInformation("Enhanced System Simulation Service is stopping");
        }

        private async Task UpdateJobProgressAsync(AsyncServiceScope scope, CancellationToken stoppingToken)
        {
            var jobService = scope.ServiceProvider.GetRequiredService<IJobService>();
            var jobSignalRService = scope.ServiceProvider.GetRequiredService<IJobSignalRService>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            // Get running jobs
            var runningJobs = await unitOfWork.Repository<Job>().GetAsync(
                j => j.Status == JobStatus.Running);

            if (!runningJobs.Any())
            {
                // If no running jobs, occasionally broadcast an empty update to keep connection alive
                if (_random.Next(5) == 0)
                {
                    await jobSignalRService.NotifyAllClientsAsync("KeepAlive", DateTime.UtcNow);
                }
                return;
            }

            foreach (var job in runningJobs)
            {
                // Update progress by some random amount (only 1-5% increments for smoother updates)
                int progressIncrement = _random.Next(1, 6);
                int newProgress = Math.Min(100, job.Progress + progressIncrement);

                _logger.LogInformation($"Simulating progress update for job {job.Id}: {job.Progress}% -> {newProgress}%");
                await jobService.UpdateJobProgressAsync(job.Id, newProgress);

                // Explicitly send SignalR notification
                await jobSignalRService.NotifyJobProgressUpdatedAsync(job.Id, newProgress);

                // If job reached 100%, mark as completed
                if (newProgress >= 100)
                {
                    _logger.LogInformation($"Job {job.Id} completed (reached 100%)");
                    await jobService.UpdateJobStatusAsync(job.Id, JobStatus.Completed);
                    await jobSignalRService.NotifyJobStatusChangedAsync(job.Id, JobStatus.Completed.ToString());

                    // Send a completion notification
                    await jobSignalRService.NotifyGroupAsync("all-jobs", "JobNotification", job.Id, $"Job \"{job.Name}\" completed successfully", "success");
                }
                // Small chance of failure for demonstration (reduced to 2%)
                else if (_random.Next(100) < 2)
                {
                    _logger.LogInformation($"Simulating random failure for job {job.Id}");
                    await jobService.UpdateJobStatusAsync(job.Id, JobStatus.Failed);
                    string errorMessage = "Simulated random failure during processing";
                    await jobSignalRService.NotifyJobErrorAsync(job.Id, errorMessage);
                    await jobSignalRService.NotifyJobStatusChangedAsync(job.Id, JobStatus.Failed.ToString());
                    await jobSignalRService.NotifyGroupAsync("all-jobs", "JobNotification", job.Id, $"Job \"{job.Name}\" failed: {errorMessage}", "error");
                }
            }
        }

        private async Task SimulateWorkerStatusChangesAsync(AsyncServiceScope scope, CancellationToken stoppingToken)
        {
            var workerService = scope.ServiceProvider.GetRequiredService<IWorkerNodeService>();
            var workerSignalRService = scope.ServiceProvider.GetRequiredService<IWorkerSignalRService>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            // Get all workers
            var workers = await unitOfWork.Repository<WorkerNode>().GetAllAsync();

            // Select a random worker to potentially toggle status (15% chance any worker changes)
            if (workers.Any() && _random.Next(100) < 15)
            {
                var worker = workers[_random.Next(workers.Count)];

                if (worker.Status == WorkerConstants.Status.Active)
                {
                    // Only 25% chance an active worker goes offline
                    if (_random.Next(100) < 25)
                    {
                        _logger.LogInformation($"Simulating worker {worker.Id} ({worker.Name}) going offline");
                        await workerService.DeactivateWorkerNodeAsync(worker.Id);
                        await workerSignalRService.NotifyGroupAsync("all-workers", "WorkerNotification", worker.Id, $"Worker \"{worker.Name}\" went offline", "warning");
                    }
                }
                else if (worker.Status == WorkerConstants.Status.Offline)
                {
                    // 75% chance offline worker comes back online
                    if (_random.Next(100) < 75)
                    {
                        _logger.LogInformation($"Simulating worker {worker.Id} ({worker.Name}) coming back online");
                        worker.Status = WorkerConstants.Status.Active;
                        worker.LastHeartbeat = DateTime.UtcNow;
                        await unitOfWork.SaveChangesAsync();
                        await workerSignalRService.NotifyWorkerStatusChangedAsync(worker.Id, WorkerConstants.Status.Active);
                        await workerSignalRService.NotifyGroupAsync("all-workers", "WorkerNotification", worker.Id, $"Worker \"{worker.Name}\" is now online", "success");
                    }
                }
            }

            // Update worker loads with small random fluctuations for more dynamic display
            foreach (var worker in workers.Where(w => w.Status == WorkerConstants.Status.Active))
            {
                // Update heartbeat for active workers
                await workerService.UpdateWorkerHeartbeatAsync(worker.Id);

                // 30% chance to adjust load slightly for active workers
                if (_random.Next(100) < 30)
                {
                    int loadChange = _random.Next(-1, 2); // -1, 0, or 1
                    int newLoad = Math.Max(0, Math.Min(worker.Capacity, worker.CurrentLoad + loadChange));

                    if (newLoad != worker.CurrentLoad)
                    {
                        await workerService.UpdateWorkerLoadAsync(worker.Id, newLoad);
                        _logger.LogInformation($"Simulated small load change for worker {worker.Name}: {worker.CurrentLoad} -> {newLoad}");
                    }
                }
            }
        }

        private async Task ProcessSomePendingJobsAsync(AsyncServiceScope scope, CancellationToken stoppingToken)
        {
            var jobService = scope.ServiceProvider.GetRequiredService<IJobService>();
            var workerService = scope.ServiceProvider.GetRequiredService<IWorkerNodeService>();
            var jobSignalRService = scope.ServiceProvider.GetRequiredService<IJobSignalRService>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            // Get pending jobs
            var pendingJobs = await unitOfWork.Repository<Job>().GetAsync(
                j => j.Status == JobStatus.Pending);

            if (pendingJobs.Any())
            {
                // Get available workers
                var availableWorkers = await workerService.GetAvailableWorkerNodesAsync();

                if (availableWorkers.Any())
                {
                    // Process a subset of pending jobs (1-3 jobs) with higher chance
                    if (_random.Next(100) < 70)
                    {
                        int jobsToProcess = Math.Min(_random.Next(1, 4), Math.Min(pendingJobs.Count, availableWorkers.Count));

                        for (int i = 0; i < jobsToProcess; i++)
                        {
                            var job = pendingJobs[i];
                            var worker = availableWorkers[_random.Next(availableWorkers.Count)];

                            _logger.LogInformation($"Simulation: Starting job {job.Id} ({job.Name}) on worker {worker.Id} ({worker.Name})");

                            // Assign to worker
                            await workerService.AssignJobToWorkerAsync(job.Id, worker.Id);

                            // Update status to running
                            await jobService.UpdateJobStatusAsync(job.Id, JobStatus.Running);
                            await jobSignalRService.NotifyJobStatusChangedAsync(job.Id, JobStatus.Running.ToString());
                            await jobSignalRService.NotifyGroupAsync("all-jobs", "JobNotification", job.Id, $"Job \"{job.Name}\" started on worker \"{worker.Name}\"", "info");
                        }
                    }
                }
            }
        }

        private async Task SendSystemNotificationAsync(AsyncServiceScope scope, CancellationToken stoppingToken)
        {
            try
            {
                var jobSignalRService = scope.ServiceProvider.GetRequiredService<IJobSignalRService>();
                var workerSignalRService = scope.ServiceProvider.GetRequiredService<IWorkerSignalRService>();

                string message = _systemMessages[_random.Next(_systemMessages.Length)];

                // 50% chance to send to jobs hub, 50% to workers hub
                if (_random.Next(2) == 0)
                {
                    await jobSignalRService.NotifyGroupAsync("all-jobs", "SystemNotification", Guid.NewGuid(), message, "info");
                    _logger.LogInformation($"Sent system notification to jobs hub: {message}");
                }
                else
                {
                    await workerSignalRService.NotifyGroupAsync("all-workers", "SystemNotification", Guid.NewGuid(), message, "info");
                    _logger.LogInformation($"Sent system notification to workers hub: {message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending system notification");
            }
        }
    }
}