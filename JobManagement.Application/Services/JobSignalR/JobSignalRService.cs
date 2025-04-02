using JobManagement.Application.Hubs;
using JobManagement.Domain.Entities;
using JobManagement.Domain.Enums;
using JobManagement.Infrastructure.Interfaces.SignalR;
using JobManagement.Infrastructure.Services.SignalR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace JobManagement.Application.Services.JobSignalR
{
    /// <summary>
    /// Implementation of job-related SignalR notifications
    /// </summary>
    public class JobSignalRService : BaseSignalRService<JobHub>, IJobSignalRService
    {
        public JobSignalRService(
            IHubContext<JobHub> hubContext,
            ILogger<JobSignalRService> logger)
            : base(hubContext, logger)
        {
        }

        /// <inheritdoc/>
        public async Task NotifyJobProgressUpdatedAsync(Guid jobId, int progress)
        {
            try
            {
                _logger.LogDebug($"Sending job progress update via SignalR: Job {jobId}, Progress {progress}%");

                // Send to all clients monitoring all jobs
                await NotifyGroupAsync("all-jobs", "JobProgressUpdated", jobId, progress);

                // Send to clients specifically monitoring this job
                await NotifyGroupAsync($"job-{jobId}", "JobProgressUpdated", jobId, progress);

                // Send to clients monitoring job status
                if (progress == 100)
                {
                    await NotifyGroupAsync("jobStatus-Completed", "JobStatusUpdate", jobId, "Completed", progress);
                }
                else
                {
                    await NotifyGroupAsync("jobStatus-Running", "JobStatusUpdate", jobId, "Running", progress);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending job progress notification for job {jobId}");
            }
        }

        /// <inheritdoc/>
        public async Task NotifyJobStatusChangedAsync(Guid jobId, string status)
        {
            try
            {
                _logger.LogDebug($"Sending job status change via SignalR: Job {jobId}, Status {status}");

                // Send to all clients monitoring all jobs
                await NotifyGroupAsync("all-jobs", "JobStatusChanged", jobId, status);

                // Send to clients specifically monitoring this job
                await NotifyGroupAsync($"job-{jobId}", "JobStatusChanged", jobId, status);

                // Send to clients monitoring this specific status
                await NotifyGroupAsync($"jobStatus-{status}", "JobStatusUpdate", jobId, status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending job status change notification for job {jobId}");
            }
        }

        /// <inheritdoc/>
        public async Task NotifyJobCreatedAsync(Job job)
        {
            var jobData = new
            {
                job.Id,
                job.Name,
                job.Description,
                Status = job.Status.ToString(),
                job.Priority,
                job.ScheduledStartTime,
                job.Progress,
                job.Type,
                CreatedAt = DateTime.UtcNow
            };

            await NotifyGroupAsync("all-jobs", "JobCreated", jobData);

            // Notify the specific job type group
            await NotifyGroupAsync($"jobType-{job.Type}", "JobCreated", jobData);

            // Notify the specific status group
            await NotifyGroupAsync($"jobStatus-{job.Status}", "JobCreated", jobData);

            await NotifyGroupAsync("all-jobs", "JobNotification", job.Id, $"New job created: {job.Name}", "info");
        }

        /// <inheritdoc/>
        public async Task NotifyJobDeletedAsync(Guid jobId)
        {
            await NotifyGroupAsync("all-jobs", "JobDeleted", jobId);
            await NotifyGroupAsync($"job-{jobId}", "JobDeleted", jobId);
        }

        /// <inheritdoc/>
        public async Task NotifyJobErrorAsync(Guid jobId, string errorMessage)
        {
            await NotifyGroupAsync("all-jobs", "JobError", jobId, errorMessage);
            await NotifyGroupAsync($"job-{jobId}", "JobError", jobId, errorMessage);
            await NotifyGroupAsync("all-jobs", "JobNotification", jobId, $"Error in job {jobId}: {errorMessage}", "error");
        }

        // New method to send system health updates
        public async Task NotifySystemHealthUpdateAsync(int queueLength, int runningJobs, int availableWorkers)
        {
            var healthData = new
            {
                QueueLength = queueLength,
                RunningJobs = runningJobs,
                AvailableWorkers = availableWorkers,
                Timestamp = DateTime.UtcNow
            };

            await NotifyAllClientsAsync("SystemHealthUpdate", healthData);
        }
    }
}