using JobManagement.Domain.Entities;
using JobManagement.Domain.Enums;
using JobManagement.Domain.Interfaces;
using JobManagement.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using static JobManagement.Domain.Constants.WorkerConstants;

namespace JobManagement.Application.Services
{
    public class WorkerAssignmentService : IWorkerAssignmentService
    {
        private readonly WorkerNodeService _workerNodeService;
        private readonly ILogger<WorkerAssignmentService> _logger;
        private readonly IUnitOfWork _unitOfWork;

        public WorkerAssignmentService(
            WorkerNodeService workerNodeService,
            IUnitOfWork unitOfWork,
            ILogger<WorkerAssignmentService> logger)
        {
            _workerNodeService = workerNodeService;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<WorkerNode> FindAvailableWorkerAsync()
        {
            var availableWorkers = await _workerNodeService.GetAvailableWorkerNodesAsync();

            if (!availableWorkers.Any())
            {
                _logger.LogWarning("No available workers found for job assignment");
                return null;
            }

            // Get worker with lowest current load (simple load balancing)
            return availableWorkers.OrderBy(w => w.CurrentLoad).First();
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
                if (!string.IsNullOrEmpty(job.WorkerNodeId))
                {
                    // Verify the worker is still available
                    if (int.TryParse(job.WorkerNodeId, out int existingWorkerId))
                    {
                        var existingWorker = await _workerNodeService.GetWorkerNodeByIdAsync(existingWorkerId);
                        if (existingWorker != null && existingWorker.IsAvailable())
                        {
                            _logger.LogInformation($"Job {job.Id} is already assigned to worker {existingWorkerId}");
                            return true;
                        }
                        else
                        {
                            // Clear assignment if worker is not available
                            job.WorkerNodeId = null;
                        }
                    }
                }

                // Find an available worker
                var worker = await FindAvailableWorkerAsync();
                if (worker == null)
                {
                    _logger.LogWarning($"No available workers found for job {job.Id}");
                    return false;
                }

                // Assign job to worker
                var success = await _workerNodeService.AssignJobToWorkerAsync(job.Id, worker.Id);
                if (success)
                {
                    _logger.LogInformation($"Assigned job {job.Id} to worker {worker.Id}: {worker.Name}");
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

        public async Task<bool> ReassignJobsFromOfflineWorkerAsync(int workerId)
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
                    j.WorkerNodeId == workerId.ToString() &&
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