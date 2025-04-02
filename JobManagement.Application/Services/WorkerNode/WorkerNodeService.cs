using JobManagement.Domain.Constants;
using JobManagement.Domain.Entities;
using JobManagement.Domain.Enums;
using JobManagement.Infrastructure.Interfaces;
using JobManagement.Infrastructure.Interfaces.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace JobManagement.Application.Services.WorkerNodes
{
    public class WorkerNodeService : IWorkerNodeService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWorkerSignalRService _workerSignalRService;
        private readonly ILogger<WorkerNodeService> _logger;
        private readonly TimeSpan _heartbeatTimeout = TimeSpan.FromMinutes(1);
        private readonly IServiceProvider _serviceProvider;

        public WorkerNodeService(
            IUnitOfWork unitOfWork,
            ILogger<WorkerNodeService> logger,
            IWorkerSignalRService workerSignalRService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _workerSignalRService = workerSignalRService;
        }

        public async Task<WorkerNode> RegisterWorkerNodeAsync(string name, string endpoint, int capacity = 5, int power = 5)
        {
            // Check if worker with this name already exists
            var existingNode = await GetWorkerNodeByNameAsync(name);
            if (existingNode != null)
            {
                _logger.LogWarning($"Worker node '{name}' already exists. Updating its details instead.");
                return await UpdateWorkerNodeAsync(existingNode.Id, endpoint, capacity, power);
            }

            // Validate power range
            power = Math.Clamp(power, 1, 10);

            var workerNode = new WorkerNode
            {
                Name = name,
                Endpoint = endpoint,
                Status = WorkerConstants.Status.Active,
                LastHeartbeat = DateTime.UtcNow,
                Capacity = capacity,
                CurrentLoad = 0,
                Power = power
            };

            await _unitOfWork.Repository<WorkerNode>().AddAsync(workerNode);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation($"Registered new worker node: {name} at {endpoint} with capacity {capacity} and power {power}");

            // Notify clients about new worker
            await _workerSignalRService.NotifyWorkerRegisteredAsync(workerNode);

            return workerNode;
        }

        public async Task<WorkerNode> GetWorkerNodeByIdAsync(Guid id)
        {
            return await _unitOfWork.Repository<WorkerNode>().GetByIdAsync(id);
        }

        public async Task<WorkerNode> GetWorkerNodeByNameAsync(string name)
        {
            var workers = await _unitOfWork.Repository<WorkerNode>().GetAsync(w => w.Name == name);
            return workers.FirstOrDefault();
        }

        public async Task<IReadOnlyList<WorkerNode>> GetAllWorkerNodesAsync()
        {
            return await _unitOfWork.Repository<WorkerNode>().GetAllAsync();
        }

        public async Task<IReadOnlyList<WorkerNode>> GetAvailableWorkerNodesAsync()
        {
            var allNodes = await _unitOfWork.Repository<WorkerNode>().GetAllAsync();

            foreach (var node in allNodes)
            {
                if (node.AssignedJobs != null && node.AssignedJobs.Any())
                {
                    // Count only running jobs
                    int actualRunningJobs = node.AssignedJobs.Count(j => j.Status == JobStatus.Running || j.Status == JobStatus.Pending);

                    if (node.CurrentLoad != actualRunningJobs)
                    {
                        _logger.LogWarning($"Worker {node.Id} ({node.Name}) load corrected from {node.CurrentLoad} to {actualRunningJobs} based on actual running jobs");
                        node.CurrentLoad = actualRunningJobs;
                        await _unitOfWork.SaveChangesAsync();
                    }
                }
            }

            return allNodes.Where(w => w.IsAvailable() && IsHeartbeatValid(w.LastHeartbeat)).ToList();
        }

        public async Task<WorkerNode> UpdateWorkerNodeAsync(Guid id, string endpoint, int capacity, int power)
        {
            var workerNode = await _unitOfWork.Repository<WorkerNode>().GetByIdAsync(id);
            if (workerNode == null)
            {
                _logger.LogWarning($"Attempted to update non-existent worker node ID {id}");
                return null;
            }

            // Validate power range
            power = Math.Clamp(power, 1, 10);

            workerNode.Endpoint = endpoint;
            workerNode.Capacity = capacity;
            workerNode.Power = power;

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation($"Updated worker node {id}: {workerNode.Name} with power {power}");

            // Notify clients about worker update
            await _workerSignalRService.NotifyWorkerLoadChangedAsync(id, workerNode.CurrentLoad, capacity);

            return workerNode;
        }

        public async Task<bool> UpdateWorkerHeartbeatAsync(Guid id)
        {
            var workerNode = await _unitOfWork.Repository<WorkerNode>().GetByIdAsync(id);
            if (workerNode == null)
            {
                _logger.LogWarning($"Attempted to update heartbeat for non-existent worker node ID {id}");
                return false;
            }

            bool wasOffline = workerNode.Status == WorkerConstants.Status.Offline ||
                             !IsHeartbeatValid(workerNode.LastHeartbeat);

            workerNode.LastHeartbeat = DateTime.UtcNow;

            // If worker was offline and is now sending heartbeats, activate it
            if (wasOffline)
            {
                _logger.LogInformation($"Worker {id} ({workerNode.Name}) was offline and is now sending heartbeats. Activating.");
                await HandleWorkerActivationAsync(id);
            }
            else
            {
                // Just update the heartbeat
                workerNode.Status = WorkerConstants.Status.Active;
                await _unitOfWork.SaveChangesAsync();
            }

            return true;
        }

        public async Task<bool> DeactivateWorkerNodeAsync(Guid id)
        {
            var workerNode = await _unitOfWork.Repository<WorkerNode>().GetByIdAsync(id);
            if (workerNode == null)
            {
                _logger.LogWarning($"Attempted to deactivate non-existent worker node ID {id}");
                return false;
            }

            var previousStatus = workerNode.Status;
            workerNode.Status = WorkerConstants.Status.Offline;

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation($"Deactivated worker node {id}: {workerNode.Name}");

            // Notify clients about worker status change
            await _workerSignalRService.NotifyWorkerStatusChangedAsync(id, WorkerConstants.Status.Offline);

            return true;
        }

        public async Task<bool> DeleteWorkerNodeAsync(Guid id)
        {
            var workerNode = await _unitOfWork.Repository<WorkerNode>().GetByIdAsync(id);
            if (workerNode == null)
            {
                _logger.LogWarning($"Attempted to delete non-existent worker node ID {id}");
                return false;
            }

            // Check if the worker has any assigned jobs
            var assignedJobs = await _unitOfWork.Repository<Job>().GetAsync(j => j.WorkerNodeId == id && j.Status == JobStatus.Running);
            if (assignedJobs.Any())
            {
                _logger.LogWarning($"Cannot delete worker node {id}: {workerNode.Name} as it has {assignedJobs.Count} running jobs");
                return false;
            }

            await _unitOfWork.Repository<WorkerNode>().DeleteAsync(workerNode);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation($"Deleted worker node {id}: {workerNode.Name}");
            return true;
        }

        public async Task<bool> UpdateWorkerLoadAsync(Guid id, int currentLoad)
        {
            var workerNode = await _unitOfWork.Repository<WorkerNode>().GetByIdAsync(id);
            if (workerNode == null)
            {
                _logger.LogWarning($"Attempted to update load for non-existent worker node ID {id}");
                return false;
            }

            int previousLoad = workerNode.CurrentLoad;
            workerNode.CurrentLoad = currentLoad;

            await _unitOfWork.SaveChangesAsync();
            _logger.LogDebug($"Updated worker node {id} load to {currentLoad}");

            // Notify clients about worker load change
            await _workerSignalRService.NotifyWorkerLoadChangedAsync(id, currentLoad, workerNode.Capacity);

            return true;
        }

        public async Task<bool> AssignJobToWorkerAsync(Guid jobId, Guid workerId)
        {
            var job = await _unitOfWork.Repository<Job>().GetByIdAsync(jobId);
            var worker = await _unitOfWork.Repository<WorkerNode>().GetByIdAsync(workerId);

            if (job == null || worker == null)
            {
                _logger.LogWarning($"Failed to assign job {jobId} to worker {workerId}: One or both entities don't exist");
                return false;
            }

            if (!worker.IsAvailable() || !IsHeartbeatValid(worker.LastHeartbeat))
            {
                _logger.LogWarning($"Cannot assign job {jobId} to worker {workerId}: Worker is not available");
                return false;
            }

            job.WorkerNodeId = workerId;
            worker.IncreaseLoad();

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation($"Assigned job {jobId} to worker {workerId}: {worker.Name} (power: {worker.Power})");

            // Notify clients about job assignment
            await _workerSignalRService.NotifyJobAssignedToWorkerAsync(jobId, workerId);

            return true;
        }

        public async Task<bool> UnassignJobFromWorkerAsync(Guid jobId)
        {
            var job = await _unitOfWork.Repository<Job>().GetByIdAsync(jobId);
            if (job == null || job.WorkerNodeId == Guid.Empty || !job.WorkerNodeId.HasValue)
            {
                return false;
            }

            var worker = await _unitOfWork.Repository<WorkerNode>().GetByIdAsync(job.WorkerNodeId.Value);
            if (worker != null)
            {
                worker.DecreaseLoad();
            }

            // Mark as unassigned
            job.WorkerNodeId = null;
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation($"Unassigned job {jobId} from worker");
            return true;
        }

        public async Task CheckInactiveWorkersAsync()
        {
            var allNodes = await _unitOfWork.Repository<WorkerNode>().GetAllAsync();
            var now = DateTime.UtcNow;

            foreach (var node in allNodes)
            {
                if (node.Status != WorkerConstants.Status.Offline && !IsHeartbeatValid(node.LastHeartbeat))
                {
                    node.Status = WorkerConstants.Status.Offline;
                    _logger.LogWarning($"Worker node {node.Id}: {node.Name} marked as offline due to heartbeat timeout");
                }
            }

            await _unitOfWork.SaveChangesAsync();
        }
        public async Task RecalculateWorkerLoadsAsync()
        {
            _logger.LogInformation("Starting worker load recalculation");

            try
            {
                // Get all workers
                var workers = await _unitOfWork.Repository<WorkerNode>().GetAllAsync();

                foreach (var worker in workers)
                {
                    // Get all running jobs for this worker
                    var runningJobs = await _unitOfWork.Repository<Job>().GetAsync(job =>
                        job.WorkerNodeId == worker.Id &&
                        (job.Status == JobStatus.Running || job.Status == JobStatus.Pending));

                    int actualRunningJobCount = runningJobs.Count;

                    // If current load doesn't match actual job count, update it
                    if (worker.CurrentLoad != actualRunningJobCount)
                    {
                        _logger.LogWarning($"Worker {worker.Id} ({worker.Name}) load corrected from {worker.CurrentLoad} to {actualRunningJobCount}");
                        worker.CurrentLoad = actualRunningJobCount;

                        // Notify about updated load
                        await _workerSignalRService.NotifyWorkerLoadChangedAsync(
                            worker.Id, worker.CurrentLoad, worker.Capacity);
                    }
                }

                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Worker load recalculation completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during worker load recalculation");
            }
        }
        // New method: Get the optimal worker for a given job
        public async Task<WorkerNode> GetOptimalWorkerForJobAsync(Job job)
        {
            var availableWorkers = await GetAvailableWorkerNodesAsync();
            if (!availableWorkers.Any())
            {
                return null;
            }

            // For high priority jobs, prioritize power over load
            if (job.Priority == JobPriority.Critical || job.Priority == JobPriority.Urgent || job.Priority == JobPriority.High)
            {
                // Get workers sorted by power (highest first), then by load (lowest first)
                return availableWorkers
                    .OrderByDescending(w => w.Power)
                    .ThenBy(w => (double)w.CurrentLoad / w.Capacity)
                    .FirstOrDefault();
            }
            // For regular and low priority jobs, balance load across workers
            else
            {
                // Calculate a score based on both load ratio and power
                // Higher power and lower load ratio is better
                return availableWorkers
                    .OrderByDescending(w => w.Power * (1 - ((double)w.CurrentLoad / w.Capacity)))
                    .FirstOrDefault();
            }
        }
        private async Task HandleWorkerActivationAsync(Guid workerId)
        {
            _logger.LogInformation($"Handling activation for worker {workerId}");

            try
            {
                var worker = await _unitOfWork.Repository<WorkerNode>().GetByIdAsync(workerId);
                if (worker == null)
                {
                    _logger.LogWarning($"Attempted to handle activation for non-existent worker ID {workerId}");
                    return;
                }

                // Update worker status to Active
                worker.Status = WorkerConstants.Status.Active;
                worker.LastHeartbeat = DateTime.UtcNow;

                // Make sure load is in sync
                var runningJobs = await _unitOfWork.Repository<Job>().GetAsync(job =>
                    job.WorkerNodeId == workerId &&
                    (job.Status == JobStatus.Running || job.Status == JobStatus.Pending));

                worker.CurrentLoad = runningJobs.Count;

                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation($"Worker {workerId} ({worker.Name}) activated with load {worker.CurrentLoad}/{worker.Capacity}");

                // Notify clients about worker status change
                await _workerSignalRService.NotifyWorkerStatusChangedAsync(workerId, WorkerConstants.Status.Active);

                // Notify about worker load
                await _workerSignalRService.NotifyWorkerLoadChangedAsync(
                    workerId, worker.CurrentLoad, worker.Capacity);

                // Check if there are jobs in the queue that can now be processed
                var jobQueue = _serviceProvider.GetRequiredService<IJobQueue>();
                var queueLength = await jobQueue.GetQueueLengthAsync();

                if (queueLength > 0)
                {
                    _logger.LogInformation($"Worker {workerId} activated with {queueLength} jobs in queue. Queue processing will handle job assignment.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error handling activation for worker {workerId}");
            }
        }
        private bool IsHeartbeatValid(DateTime lastHeartbeat)
        {
            // Convert Unspecified kind to UTC if needed
            DateTime normalizedHeartbeat = lastHeartbeat;
            if (lastHeartbeat.Kind == DateTimeKind.Unspecified)
            {
                normalizedHeartbeat = DateTime.SpecifyKind(lastHeartbeat, DateTimeKind.Utc);
            }

            TimeSpan timeSinceLastHeartbeat = DateTime.UtcNow - normalizedHeartbeat;
            bool isValid = timeSinceLastHeartbeat < _heartbeatTimeout;

            // Log detailed heartbeat information for debugging
            _logger.LogDebug($"Heartbeat validation: Last heartbeat at {normalizedHeartbeat:yyyy-MM-dd HH:mm:ss.fff} UTC, " +
                             $"Time since last heartbeat: {timeSinceLastHeartbeat.TotalMinutes:F1} minutes, " +
                             $"Timeout: {_heartbeatTimeout.TotalMinutes:F1} minutes, Valid: {isValid}");

            return isValid;
        }
    }
}