using JobManagement.Application.Hubs;
using JobManagement.Domain.Entities;
using JobManagement.Infrastructure.Interfaces.SignalR;
using JobManagement.Infrastructure.Services.SignalR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace JobManagement.Application.Services.WorkerSignalR
{
    /// <summary>
    /// Enhanced implementation of worker-related SignalR notifications
    /// </summary>
    public class WorkerSignalRService : BaseSignalRService<WorkerHub>, IWorkerSignalRService
    {
        public WorkerSignalRService(
            IHubContext<WorkerHub> hubContext,
            ILogger<WorkerSignalRService> logger)
            : base(hubContext, logger)
        {
        }

        /// <inheritdoc/>
        public async Task NotifyWorkerStatusChangedAsync(Guid workerId, string status)
        {
            try
            {
                // Send to all clients monitoring all workers
                await NotifyGroupAsync("all-workers", "WorkerStatusChanged", workerId, status);

                // Send to clients specifically monitoring this worker
                await NotifyGroupAsync($"worker-{workerId}", "WorkerStatusChanged", workerId, status);

                _logger.LogDebug($"Notified clients of worker {workerId} status change to {status}");

                // Broadcast the updated count of active/inactive workers
                await BroadcastWorkerStatusCountsAsync();

                // If worker goes offline, send a notification
                if (status == "Offline")
                {
                    await NotifyGroupAsync("all-workers", "WorkerNotification", workerId, $"Worker {workerId} went offline", "warning");
                }
                else if (status == "Active")
                {
                    await NotifyGroupAsync("all-workers", "WorkerNotification", workerId, $"Worker {workerId} is now active", "success");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error notifying worker status change for worker {workerId}");
            }
        }

        /// <inheritdoc/>
        public async Task NotifyWorkerLoadChangedAsync(Guid workerId, int currentLoad, int capacity)
        {
            try
            {
                // Calculate utilization percentage
                int utilizationPercentage = capacity > 0 ? (currentLoad * 100 / capacity) : 0;

                // Send to all clients monitoring all workers
                await NotifyGroupAsync("all-workers", "WorkerLoadChanged", workerId, currentLoad, capacity, utilizationPercentage);

                // Send to clients specifically monitoring this worker
                await NotifyGroupAsync($"worker-{workerId}", "WorkerLoadChanged", workerId, currentLoad, capacity, utilizationPercentage);

                _logger.LogDebug($"Notified clients of worker {workerId} load change to {currentLoad}/{capacity} ({utilizationPercentage}%)");

                // Alert about high worker load if necessary
                if (utilizationPercentage > 90)
                {
                    await NotifyGroupAsync("all-workers", "WorkerNotification", workerId, $"Worker {workerId} is heavily loaded ({utilizationPercentage}%)", "warning");
                }
                else if (utilizationPercentage < 10 && currentLoad == 0)
                {
                    await NotifyGroupAsync("all-workers", "WorkerNotification", workerId, $"Worker {workerId} is idle", "info");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error notifying worker load change for worker {workerId}");
            }
        }

        /// <inheritdoc/>
        public async Task NotifyWorkerRegisteredAsync(WorkerNode worker)
        {
            try
            {
                var workerData = new
                {
                    worker.Id,
                    worker.Name,
                    worker.Endpoint,
                    worker.Status,
                    worker.Capacity,
                    worker.CurrentLoad,
                    worker.Power,
                    UtilizationPercentage = worker.Capacity > 0 ? (worker.CurrentLoad * 100 / worker.Capacity) : 0,
                    RegisteredAt = DateTime.UtcNow
                };

                await NotifyGroupAsync("all-workers", "WorkerRegistered", workerData);
                await NotifyGroupAsync("all-workers", "WorkerNotification", worker.Id, $"New worker registered: {worker.Name}", "info");

                _logger.LogDebug($"Notified clients of new worker registration: {worker.Id} ({worker.Name})");

                // Broadcast the updated count of workers
                await BroadcastWorkerStatusCountsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error notifying worker registration for worker {worker.Id}");
            }
        }

        /// <inheritdoc/>
        public async Task NotifyWorkerDeactivatedAsync(Guid workerId)
        {
            try
            {
                await NotifyGroupAsync("all-workers", "WorkerDeactivated", workerId);
                await NotifyGroupAsync($"worker-{workerId}", "WorkerDeactivated", workerId);
                await NotifyGroupAsync("all-workers", "WorkerNotification", workerId, $"Worker {workerId} has been deactivated", "info");

                _logger.LogDebug($"Notified clients of worker deactivation: {workerId}");

                // Broadcast the updated count of workers
                await BroadcastWorkerStatusCountsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error notifying worker deactivation for worker {workerId}");
            }
        }

        /// <inheritdoc/>
        public async Task NotifyJobAssignedToWorkerAsync(Guid jobId, Guid workerId)
        {
            try
            {
                // Notify worker groups
                await NotifyGroupAsync("all-workers", "JobAssignedToWorker", jobId, workerId);
                await NotifyGroupAsync($"worker-{workerId}", "JobAssignedToWorker", jobId, workerId);

                // Also notify job groups
                await NotifyGroupAsync("all-jobs", "JobAssignedToWorker", jobId, workerId);
                await NotifyGroupAsync($"job-{jobId}", "JobAssignedToWorker", jobId, workerId);

                _logger.LogDebug($"Notified clients of job {jobId} assignment to worker {workerId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error notifying job assignment for job {jobId} to worker {workerId}");
            }
        }

        // New method to broadcast current worker status counts
        private async Task BroadcastWorkerStatusCountsAsync()
        {
            try
            {
                // This would be implemented by querying the database for current counts
                // For simulation purposes, we'll just send approximate data
                var random = new Random();
                var statusData = new
                {
                    ActiveWorkers = random.Next(3, 8),
                    IdleWorkers = random.Next(1, 3),
                    OfflineWorkers = random.Next(0, 2),
                    TotalWorkers = random.Next(5, 10),
                    Timestamp = DateTime.UtcNow
                };

                await NotifyGroupAsync("all-workers", "WorkerStatusCounts", statusData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting worker status counts");
            }
        }
    }
}