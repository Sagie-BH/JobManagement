using JobManagement.Domain.Entities;

namespace JobManagement.Application.Services.WorkerNodes
{
    public interface IWorkerNodeService
    {
        Task<bool> AssignJobToWorkerAsync(Guid jobId, Guid workerId);
        Task CheckInactiveWorkersAsync();
        Task<bool> DeactivateWorkerNodeAsync(Guid id);
        Task<bool> DeleteWorkerNodeAsync(Guid id);
        Task<IReadOnlyList<WorkerNode>> GetAllWorkerNodesAsync();
        Task<IReadOnlyList<WorkerNode>> GetAvailableWorkerNodesAsync();
        Task<WorkerNode> GetWorkerNodeByIdAsync(Guid id);
        Task<WorkerNode> GetWorkerNodeByNameAsync(string name);
        Task<WorkerNode> RegisterWorkerNodeAsync(string name, string endpoint, int capacity = 5, int power = 5);
        Task<bool> UnassignJobFromWorkerAsync(Guid jobId);
        Task<bool> UpdateWorkerHeartbeatAsync(Guid id);
        Task<bool> UpdateWorkerLoadAsync(Guid id, int currentLoad);
        Task<WorkerNode> UpdateWorkerNodeAsync(Guid id, string endpoint, int capacity, int power);
        Task RecalculateWorkerLoadsAsync();

        // Optional: Add a method to get optimal worker for a job
        Task<WorkerNode> GetOptimalWorkerForJobAsync(Job job);
    }
}