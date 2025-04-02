using JobManagement.Application.Services.WorkerNodes;
using JobManagement.Domain.Entities;
using JobManagement.Domain.Enums;
using JobManagement.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;

namespace JobManagement.Application.Services.WorkerAssignment
{
    public class WorkerAssignmentService : IWorkerAssignmentService
    {
        private readonly IWorkerNodeService _workerNodeService;
        private readonly ILogger<WorkerAssignmentService> _logger;
        private readonly IUnitOfWork _unitOfWork;

        public WorkerAssignmentService(
            IWorkerNodeService workerNodeService,
            IUnitOfWork unitOfWork,
            ILogger<WorkerAssignmentService> logger)
        {
            _workerNodeService = workerNodeService;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<WorkerNode> FindAvailableWorkerAsync(Job job)
        {
            var availableWorkers = await _workerNodeService.GetAvailableWorkerNodesAsync();

            if (!availableWorkers.Any())
            {
                _logger.LogWarning("No available workers found for job assignment");
                return null;
            }

            // Select the best worker based on job priority and worker characteristics
            WorkerNode selectedWorker = null;

            switch (job.Priority)
            {
                // For highest priorities, select the most powerful available worker
                case JobPriority.Critical:
                case JobPriority.Urgent:
                    selectedWorker = availableWorkers
                        .OrderByDescending(w => w.Power)
                        .ThenBy(w => (double)w.CurrentLoad / w.Capacity)
                        .FirstOrDefault();
                    _logger.LogInformation($"Selected highest power worker for critical/urgent job: {selectedWorker?.Name} (Power: {selectedWorker?.Power})");
                    break;

                // For high priority, balance power and load but favor power
                case JobPriority.High:
                    selectedWorker = availableWorkers
                        .OrderByDescending(w => (w.Power * 0.7) + ((1 - (double)w.CurrentLoad / w.Capacity) * 0.3))
                        .FirstOrDefault();
                    _logger.LogInformation($"Selected balanced worker (power-focused) for high priority job: {selectedWorker?.Name} (Power: {selectedWorker?.Power})");
                    break;

                // For regular priority, balance power and load equally
                case JobPriority.Regular:
                    selectedWorker = availableWorkers
                        .OrderByDescending(w => (w.Power * 0.5) + ((1 - (double)w.CurrentLoad / w.Capacity) * 0.5))
                        .FirstOrDefault();
                    _logger.LogInformation($"Selected balanced worker for regular priority job: {selectedWorker?.Name} (Power: {selectedWorker?.Power})");
                    break;

                // For low priorities, prioritize load balancing over power
                case JobPriority.Low:
                case JobPriority.Deferred:
                    selectedWorker = availableWorkers
                        .OrderBy(w => (double)w.CurrentLoad / w.Capacity)
                        .ThenByDescending(w => w.Power)
                        .FirstOrDefault();
                    _logger.LogInformation($"Selected least loaded worker for low priority job: {selectedWorker?.Name} (Load: {selectedWorker?.CurrentLoad}/{selectedWorker?.Capacity})");
                    break;

                default:
                    // Fallback to simple load balancing
                    selectedWorker = availableWorkers.OrderBy(w => w.CurrentLoad).First();
                    break;
            }

            return selectedWorker;
        }

        public async Task<bool> TryAssignJobToWorkerAsync(Job job)
        {
            if (job == null || job.Status != JobStatus.Pending)
            {
                return false;
            }

            try
            {
                // Check if already assigned
                if (job.WorkerNodeId.HasValue && job.WorkerNodeId != Guid.Empty)
                {
                    var existingWorker = await _workerNodeService.GetWorkerNodeByIdAsync(job.WorkerNodeId.Value);
                    if (existingWorker != null && existingWorker.IsAvailable())
                    {
                        _logger.LogInformation($"Job {job.Id} is already assigned to worker {job.WorkerNodeId}");
                        return true;
                    }
                    else
                    {
                        // Clear assignment if worker is not available
                        job.WorkerNodeId = null;
                    }
                }

                // Find an available worker based on job characteristics
                var worker = await FindAvailableWorkerAsync(job);
                if (worker == null)
                {
                    _logger.LogWarning($"No available workers found for job {job.Id}");
                    return false;
                }

                // Assign job to worker
                var success = await _workerNodeService.AssignJobToWorkerAsync(job.Id, worker.Id);
                if (success)
                {
                    _logger.LogInformation($"Assigned job {job.Id} to worker {worker.Id}: {worker.Name} (Power: {worker.Power})");

                    // Log estimated processing time
                    var estimatedTime = worker.GetEstimatedProcessingTime(job);
                    _logger.LogInformation($"Estimated processing time for job {job.Id} on worker {worker.Name}: {estimatedTime.TotalMinutes:F1} minutes");

                    return true;
                }
                else
                {
                    _logger.LogWarning($"Failed to assign job {job.Id} to worker {worker.Id}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error assigning job {job.Id} to a worker");
                return false;
            }
        }

        public async Task<bool> ReassignJobsFromOfflineWorkerAsync(Guid workerId)
        {
            try
            {
                var workerNode = await _workerNodeService.GetWorkerNodeByIdAsync(workerId);
                if (workerNode == null)
                {
                    _logger.LogWarning($"Attempted to reassign jobs from non-existent worker {workerId}");
                    return false;
                }

                // Get all running jobs for this worker
                var jobRepository = _unitOfWork.Repository<Job>();
                var assignedJobs = await jobRepository.GetAsync(j =>
                    j.WorkerNodeId == workerId &&
                    (j.Status == JobStatus.Running || j.Status == JobStatus.Pending));

                if (!assignedJobs.Any())
                {
                    _logger.LogInformation($"No active jobs found for worker {workerId} to reassign");
                    return true;
                }

                _logger.LogInformation($"Reassigning {assignedJobs.Count} jobs from offline worker {workerId}");

                // Reset each job to pending state
                foreach (var job in assignedJobs)
                {
                    job.Status = JobStatus.Pending;
                    job.WorkerNodeId = null;
                    job.Progress = 0;

                    if (job.Status == JobStatus.Running)
                    {
                        await _unitOfWork.Repository<JobLog>().AddAsync(new JobLog
                        {
                            JobId = job.Id,
                            LogType = LogType.Warning,
                            Message = $"Job reassigned due to worker {workerId} going offline",
                            Timestamp = DateTime.UtcNow
                        });
                    }

                    // Try to assign the job to a new worker
                    await TryAssignJobToWorkerAsync(job);
                }

                await _unitOfWork.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error reassigning jobs from worker {workerId}");
                return false;
            }
        }
    }
}