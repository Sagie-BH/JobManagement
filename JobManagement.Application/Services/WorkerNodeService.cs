using JobManagement.Domain.Constants;
using JobManagement.Domain.Entities;
using JobManagement.Domain.Enums;
using JobManagement.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace JobManagement.Application.Services
{
    public class WorkerNodeService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<WorkerNodeService> _logger;
        private readonly TimeSpan _heartbeatTimeout = TimeSpan.FromMinutes(5);

        public WorkerNodeService(
            IUnitOfWork unitOfWork,
            ILogger<WorkerNodeService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<WorkerNode> RegisterWorkerNodeAsync(string name, string endpoint, int capacity = 5)
        {
            // Check if worker with this name already exists
            var existingNode = await GetWorkerNodeByNameAsync(name);
            if (existingNode != null)
            {
                _logger.LogWarning($"Worker node '{name}' already exists. Updating its details instead.");
                return await UpdateWorkerNodeAsync(existingNode.Id, endpoint, capacity);
            }

            var workerNode = new WorkerNode
            {
                Name = name,
                Endpoint = endpoint,
                Status = WorkerConstants.Status.Active,
                LastHeartbeat = DateTime.UtcNow,
                Capacity = capacity,
                CurrentLoad = 0
            };

            await _unitOfWork.Repository<WorkerNode>().AddAsync(workerNode);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation($"Registered new worker node: {name} at {endpoint} with capacity {capacity}");
            return workerNode;
        }

        public async Task<WorkerNode> GetWorkerNodeByIdAsync(int id)
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
            return allNodes.Where(w => w.IsAvailable() && IsHeartbeatValid(w.LastHeartbeat)).ToList();
        }

        public async Task<WorkerNode> UpdateWorkerNodeAsync(int id, string endpoint, int capacity)
        {
            var workerNode = await _unitOfWork.Repository<WorkerNode>().GetByIdAsync(id);
            if (workerNode == null)
            {
                _logger.LogWarning($"Attempted to update non-existent worker node ID {id}");
                return null;
            }

            workerNode.Endpoint = endpoint;
            workerNode.Capacity = capacity;

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation($"Updated worker node {id}: {workerNode.Name}");
            return workerNode;
        }

        public async Task<bool> UpdateWorkerHeartbeatAsync(int id)
        {
            var workerNode = await _unitOfWork.Repository<WorkerNode>().GetByIdAsync(id);
            if (workerNode == null)
            {
                _logger.LogWarning($"Attempted to update heartbeat for non-existent worker node ID {id}");
                return false;
            }

            workerNode.UpdateHeartbeat();
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeactivateWorkerNodeAsync(int id)
        {
            var workerNode = await _unitOfWork.Repository<WorkerNode>().GetByIdAsync(id);
            if (workerNode == null)
            {
                _logger.LogWarning($"Attempted to deactivate non-existent worker node ID {id}");
                return false;
            }

            workerNode.Status = WorkerConstants.Status.Offline;
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation($"Deactivated worker node {id}: {workerNode.Name}");
            return true;
        }

        public async Task<bool> DeleteWorkerNodeAsync(int id)
        {
            var workerNode = await _unitOfWork.Repository<WorkerNode>().GetByIdAsync(id);
            if (workerNode == null)
            {
                _logger.LogWarning($"Attempted to delete non-existent worker node ID {id}");
                return false;
            }

            // Check if the worker has any assigned jobs
            var assignedJobs = await _unitOfWork.Repository<Job>().GetAsync(j => j.WorkerNodeId == id.ToString() && j.Status == JobStatus.Running);
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

        public async Task<bool> UpdateWorkerLoadAsync(int id, int currentLoad)
        {
            var workerNode = await _unitOfWork.Repository<WorkerNode>().GetByIdAsync(id);
            if (workerNode == null)
            {
                _logger.LogWarning($"Attempted to update load for non-existent worker node ID {id}");
                return false;
            }

            workerNode.CurrentLoad = currentLoad;
            await _unitOfWork.SaveChangesAsync();
            _logger.LogDebug($"Updated worker node {id} load to {currentLoad}");
            return true;
        }

        public async Task<bool> AssignJobToWorkerAsync(int jobId, int workerId)
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

            job.WorkerNodeId = workerId.ToString();
            worker.IncreaseLoad();

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation($"Assigned job {jobId} to worker {workerId}: {worker.Name}");
            return true;
        }

        public async Task<bool> UnassignJobFromWorkerAsync(int jobId)
        {
            var job = await _unitOfWork.Repository<Job>().GetByIdAsync(jobId);
            if (job == null || string.IsNullOrEmpty(job.WorkerNodeId))
            {
                return false;
            }

            if (int.TryParse(job.WorkerNodeId, out int workerId))
            {
                var worker = await _unitOfWork.Repository<WorkerNode>().GetByIdAsync(workerId);
                if (worker != null)
                {
                    worker.DecreaseLoad();
                }
            }

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

        private bool IsHeartbeatValid(DateTime lastHeartbeat)
        {
            return (DateTime.UtcNow - lastHeartbeat) < _heartbeatTimeout;
        }
    }
}