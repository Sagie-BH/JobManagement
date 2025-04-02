using JobManagement.Application.Models.Requests;
using JobManagement.Application.Services.WorkerNodes;
using JobManagement.Application.Services.WorkerSignalR;
using JobManagement.Domain.Entities;
using JobManagement.Domain.Enums;
using JobManagement.Infrastructure.Interfaces;
using JobManagement.Infrastructure.Interfaces.SignalR;
using JobManagement.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace JobManagement.Application.Services.JobServices
{
    public class JobService : IJobService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IJobQueue _jobQueue;
        private readonly ILogger<JobService> _logger;
        private readonly IJobSignalRService _jobSignalRService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IWorkerNodeService _workerNodeService;
        private readonly IWorkerAssignmentService _workerAssignmentService;
        private readonly IWorkerSignalRService _workerSignalRService;
        public JobService(
            IUnitOfWork unitOfWork,
            IJobQueue jobQueue,
            ILogger<JobService> logger,
            IJobSignalRService jobSignalRService,
            ICurrentUserService currentUserService,
            IWorkerNodeService workerNodeService,
            IWorkerAssignmentService workerAssignmentService,
            IWorkerSignalRService signalRService)
        {
            _unitOfWork = unitOfWork;
            _jobQueue = jobQueue;
            _logger = logger;
            _jobSignalRService = jobSignalRService;
            _currentUserService = currentUserService;
            _workerNodeService = workerNodeService;
            _workerAssignmentService = workerAssignmentService;
            _workerSignalRService = signalRService;
        }

        public async Task<Job> CreateJobAsync(string name, string description, JobPriority priority, DateTime? scheduledStartTime = null, Guid? preferredWorkerId = null, JobType type = JobType.Generic)
        {
            try
            {
                // Check if there are any worker nodes registered in the system
                var allWorkers = await _workerNodeService.GetAllWorkerNodesAsync();
                if (!allWorkers.Any())
                {
                    throw new InvalidOperationException("Cannot create job: No worker nodes exist in the system.");
                }

                var currentUser = await _currentUserService.GetCurrentUserAsync();

                var job = new Job
                {
                    Name = name,
                    Description = description,
                    Status = JobStatus.Pending,
                    Priority = priority,
                    Progress = 0,
                    ScheduledStartTime = scheduledStartTime,
                    Type = type,
                    CreatedBy = currentUser?.Username ?? "System",
                    CreatedOn = DateTime.UtcNow,
                    LastModifiedBy = currentUser?.Username ?? "System",
                    LastModifiedOn = DateTime.UtcNow
                };

                await _unitOfWork.Repository<Job>().AddAsync(job);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation($"Created job {job.Id}: {job.Name} with {job.Priority} priority");

                // Notify clients about new job
                await _jobSignalRService.NotifyJobCreatedAsync(job);

                // Check for available workers
                var availableWorkers = await _workerNodeService.GetAvailableWorkerNodesAsync();
                bool workerAvailable = availableWorkers.Any();

                // Try to assign the job to a worker
                if (preferredWorkerId.HasValue)
                {
                    // Get the preferred worker
                    var worker = await _workerNodeService.GetWorkerNodeByIdAsync(preferredWorkerId.Value);

                    // Check if the worker exists and is available
                    if (worker != null && worker.IsAvailable())
                    {
                        // Try to assign to preferred worker
                        var assigned = await _workerNodeService.AssignJobToWorkerAsync(job.Id, worker.Id);

                        if (!assigned)
                        {
                            _logger.LogWarning($"Could not assign job {job.Id} to preferred worker {worker.Id}. Using automatic assignment.");
                            await _workerAssignmentService.TryAssignJobToWorkerAsync(job);
                        }
                    }
                    else if (worker != null)
                    {
                        _logger.LogWarning($"Preferred worker {preferredWorkerId} is not available. Job will be queued.");
                        // Always add to queue if scheduled for the future or no worker is available
                        await _jobQueue.EnqueueAsync(job);
                        _logger.LogInformation($"Job {job.Id} added to queue for future processing");
                    }
                    else
                    {
                        _logger.LogWarning($"Preferred worker {preferredWorkerId} not found. Using automatic assignment.");
                        await _workerAssignmentService.TryAssignJobToWorkerAsync(job);
                    }
                }
                else if (workerAvailable)
                {
                    // Use automatic worker assignment
                    bool assigned = await _workerAssignmentService.TryAssignJobToWorkerAsync(job);

                    if (!assigned)
                    {
                        // If assignment fails for some reason, add to queue
                        await _jobQueue.EnqueueAsync(job);
                        _logger.LogInformation($"Job {job.Id} added to queue after assignment failure");
                    }
                }
                else
                {
                    // No workers available - add to queue for future processing
                    await _jobQueue.EnqueueAsync(job);
                    _logger.LogInformation($"Job {job.Id} added to queue - no workers available");
                }

                // If job is scheduled for the future and not yet added to queue, add it
                if (scheduledStartTime != null && scheduledStartTime > DateTime.UtcNow &&
                    (job.WorkerNodeId == null || job.WorkerNodeId == Guid.Empty))
                {
                    // Check if job is already in queue to avoid duplicate entries
                    var queuedJobs = await _jobQueue.GetPendingJobsAsync();
                    if (!queuedJobs.Any(j => j.Id == job.Id))
                    {
                        await _jobQueue.EnqueueAsync(job);
                        _logger.LogInformation($"Job {job.Id} added to queue for scheduled execution at {scheduledStartTime}");
                    }
                }

                return job;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating job {name}");
                throw;
            }
        }

        public async Task<Job> GetJobByIdAsync(Guid id)
        {
            return await _unitOfWork.Repository<Job>().GetByIdAsync(id);
        }

        public async Task<IReadOnlyList<Job>> GetAllJobsAsync()
        {
            return await _unitOfWork.Repository<Job>().GetAllAsync();
        }

        public async Task<IReadOnlyList<Job>> GetJobsByStatusAsync(JobStatus status)
        {
            return await _unitOfWork.Repository<Job>().GetAsync(j => j.Status == status);
        }

        public async Task<IReadOnlyList<Job>> GetJobsByPriorityAsync(JobPriority priority)
        {
            return await _unitOfWork.Repository<Job>().GetAsync(j => j.Priority == priority);
        }

        public async Task<bool> UpdateJobStatusAsync(Guid jobId, JobStatus newStatus)
        {
            var job = await _unitOfWork.Repository<Job>().GetByIdAsync(jobId);
            if (job == null)
            {
                _logger.LogWarning($"Attempted to update status for non-existent job ID {jobId}");
                return false;
            }

            var previousStatus = job.Status;
            job.Status = newStatus;

            // Set start/end times based on status
            if (newStatus == JobStatus.Running && job.StartTime == null)
            {
                job.StartTime = DateTime.UtcNow;
            }
            else if ((newStatus == JobStatus.Completed || newStatus == JobStatus.Failed || newStatus == JobStatus.Stopped) && job.EndTime == null)
            {
                job.EndTime = DateTime.UtcNow;

                // For completed/failed/stopped jobs, update the worker load
                if (job.WorkerNodeId.HasValue && job.WorkerNodeId != Guid.Empty)
                {
                    var worker = await _workerNodeService.GetWorkerNodeByIdAsync(job.WorkerNodeId.Value);
                    if (worker != null)
                    {
                        // Decrease the worker load
                        worker.DecreaseLoad();

                        // Ensure load doesn't go below 0
                        if (worker.CurrentLoad < 0)
                        {
                            worker.CurrentLoad = 0;
                            _logger.LogWarning($"Worker {worker.Id} ({worker.Name}) load was corrected to 0 (went negative)");
                        }

                        await _unitOfWork.SaveChangesAsync();

                        // Log the worker load change
                        _logger.LogInformation($"Decreased load for worker {worker.Id} ({worker.Name}) to {worker.CurrentLoad}");

                        // Notify clients about worker load change
                        await _workerSignalRService.NotifyWorkerLoadChangedAsync(
                            worker.Id, worker.CurrentLoad, worker.Capacity);
                    }
                }
            }

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation($"Updated job {jobId} status from {previousStatus} to {newStatus}");

            // Notify clients about status change
            await _jobSignalRService.NotifyJobStatusChangedAsync(jobId, newStatus.ToString());

            return true;
        }

        public async Task<IReadOnlyList<Job>> GetJobsAsync(JobFilterRequest jobFilterRequest)
        {
            // Build the filter expression
            Expression<Func<Job, bool>> predicate = job => true; // Start with all jobs

            if (!string.IsNullOrWhiteSpace(jobFilterRequest.NameFilter))
            {
                predicate = job => job.Name.Contains(jobFilterRequest.NameFilter, StringComparison.OrdinalIgnoreCase);
            }

            if (jobFilterRequest.StatusFilter.HasValue)
            {
                var statusValue = jobFilterRequest.StatusFilter.Value;
                predicate = job => job.Status == statusValue;
            }

            if (jobFilterRequest.PriorityFilter.HasValue)
            {
                var priorityValue = jobFilterRequest.PriorityFilter.Value;
                predicate = job => job.Priority == priorityValue;
            }

            // Get the filtered jobs
            var jobs = await _unitOfWork.Repository<Job>().GetAsync(predicate);

            // Apply sorting if specified
            if (!string.IsNullOrWhiteSpace(jobFilterRequest.SortBy))
            {
                jobs = jobFilterRequest.SortBy.ToLowerInvariant() switch
                {
                    "name" => jobFilterRequest.SortAscending
                        ? jobs.OrderBy(j => j.Name).ToList()
                        : jobs.OrderByDescending(j => j.Name).ToList(),

                    "status" => jobFilterRequest.SortAscending
                        ? jobs.OrderBy(j => j.Status).ToList()
                        : jobs.OrderByDescending(j => j.Status).ToList(),

                    "priority" => jobFilterRequest.SortAscending
                        ? jobs.OrderBy(j => j.Priority).ToList()
                        : jobs.OrderByDescending(j => j.Priority).ToList(),

                    "progress" => jobFilterRequest.SortAscending
                        ? jobs.OrderBy(j => j.Progress).ToList()
                        : jobs.OrderByDescending(j => j.Progress).ToList(),

                    "scheduled" => jobFilterRequest.SortAscending
                        ? jobs.OrderBy(j => j.ScheduledStartTime).ToList()
                        : jobs.OrderByDescending(j => j.ScheduledStartTime).ToList(),

                    "created" => jobFilterRequest.SortAscending
                        ? jobs.OrderBy(j => j.CreatedOn).ToList()
                        : jobs.OrderByDescending(j => j.CreatedOn).ToList(),

                    "type" => jobFilterRequest.SortAscending
                        ? jobs.OrderBy(j => j.Type).ToList()
                        : jobs.OrderByDescending(j => j.Type).ToList(),

                    // Default sort by creation date, newest first
                    _ => jobs.OrderByDescending(j => j.CreatedOn).ToList()
                };
            }

            return jobs;
        }

        public async Task<bool> UpdateJobProgressAsync(Guid jobId, int progress)
        {
            var job = await _unitOfWork.Repository<Job>().GetByIdAsync(jobId);
            if (job == null)
            {
                _logger.LogWarning($"Attempted to update progress for non-existent job ID {jobId}");
                return false;
            }

            job.UpdateProgress(progress);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogDebug($"Updated job {jobId} progress to {progress}%");

            // Explicitly notify clients about progress update
            await _jobSignalRService.NotifyJobProgressUpdatedAsync(jobId, progress);

            return true;
        }

        public async Task<bool> DeleteJobAsync(Guid jobId)
        {
            var job = await _unitOfWork.Repository<Job>().GetByIdAsync(jobId);
            if (job == null)
            {
                _logger.LogWarning($"Attempted to delete non-existent job ID {jobId}");
                return false;
            }

            // Only allow deletion of completed, failed, or stopped jobs
            if (job.Status != JobStatus.Completed && job.Status != JobStatus.Failed && job.Status != JobStatus.Stopped)
            {
                _logger.LogWarning($"Cannot delete job {jobId} with status {job.Status}");
                return false;
            }

            await _unitOfWork.Repository<Job>().DeleteAsync(job);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation($"Deleted job {jobId}");

            // Notify clients about job deletion
            await _jobSignalRService.NotifyJobDeletedAsync(jobId);

            return true;
        }

        public async Task<bool> StopJobAsync(Guid jobId)
        {
            var job = await _unitOfWork.Repository<Job>().GetByIdAsync(jobId);
            if (job == null || job.Status != JobStatus.Running)
            {
                return false;
            }

            job.Status = JobStatus.Stopped;
            job.EndTime = DateTime.UtcNow;

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation($"Stopped job {jobId}");

            // Notify worker node to decrease load
            if (job.WorkerNodeId.HasValue && job.WorkerNodeId != Guid.Empty)
            {
                await _workerNodeService.UpdateWorkerLoadAsync(job.WorkerNodeId.Value,
                    Math.Max(0, (await _workerNodeService.GetWorkerNodeByIdAsync(job.WorkerNodeId.Value))?.CurrentLoad - 1 ?? 0));
            }

            return true;
        }

        public async Task<bool> RestartJobAsync(Guid jobId)
        {
            var job = await _unitOfWork.Repository<Job>().GetByIdAsync(jobId);
            if (job == null || (job.Status != JobStatus.Failed && job.Status != JobStatus.Stopped))
            {
                return false;
            }

            job.Status = JobStatus.Pending;
            job.StartTime = null;
            job.EndTime = null;
            job.Progress = 0;
            job.WorkerNodeId = null; // Clear worker assignment for reassignment

            await _unitOfWork.SaveChangesAsync();

            // Try to assign to an available worker
            await _workerAssignmentService.TryAssignJobToWorkerAsync(job);

            // Add to queue if not assigned to a worker
            if (job.WorkerNodeId == null || job.WorkerNodeId == Guid.Empty)
            {
                await _jobQueue.EnqueueAsync(job);
            }

            _logger.LogInformation($"Restarted job {jobId}");
            return true;
        }

        public async Task<bool> RetryJobAsync(Guid jobId)
        {
            var job = await _unitOfWork.Repository<Job>().GetByIdAsync(jobId);
            if (job == null)
            {
                _logger.LogWarning($"Attempted to retry non-existent job ID {jobId}");
                return false;
            }

            // We can retry failed, stopped, or completed jobs
            if (job.Status != JobStatus.Failed && job.Status != JobStatus.Stopped && job.Status != JobStatus.Completed)
            {
                _logger.LogWarning($"Cannot retry job {jobId} with status {job.Status}. Only Failed, Stopped, or Completed jobs can be retried.");
                return false;
            }

            // Reset job for retry
            job.Status = JobStatus.Pending;
            job.Progress = 0;
            job.StartTime = null;
            job.EndTime = null;
            job.ErrorMessage = null;
            job.WorkerNodeId = null; // Clear worker assignment for reassignment
            job.CurrentRetryCount++;

            await _unitOfWork.SaveChangesAsync();

            // Try to assign to an available worker
            await _workerAssignmentService.TryAssignJobToWorkerAsync(job);

            // Add to queue if not assigned to a worker
            if (job.WorkerNodeId == null || job.WorkerNodeId == Guid.Empty)
            {
                await _jobQueue.EnqueueAsync(job);
            }

            _logger.LogInformation($"Job {jobId} has been reset and queued for retry (retry count: {job.CurrentRetryCount})");

            // Notify clients about status change
            await _jobSignalRService.NotifyJobStatusChangedAsync(jobId, JobStatus.Pending.ToString());

            return true;
        }

        public async Task AddJobLogAsync(Guid jobId, LogType logType, string message, string details = null)
        {
            var job = await _unitOfWork.Repository<Job>().GetByIdAsync(jobId);
            if (job == null)
            {
                _logger.LogWarning($"Attempted to add log to non-existent job ID {jobId}");
                return;
            }

            var log = new JobLog
            {
                JobId = jobId,
                LogType = logType,
                Message = message,
                Details = details,
                Timestamp = DateTime.UtcNow
            };

            await _unitOfWork.Repository<JobLog>().AddAsync(log);
            await _unitOfWork.SaveChangesAsync();
        }
    }
}