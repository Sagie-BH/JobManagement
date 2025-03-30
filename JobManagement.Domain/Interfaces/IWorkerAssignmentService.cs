using JobManagement.Domain.Entities;

namespace JobManagement.Infrastructure.Interfaces
{
    public interface IWorkerAssignmentService
    {
        Task<WorkerNode> FindAvailableWorkerAsync();
        Task<bool> TryAssignJobToWorkerAsync(Job job);
        Task<bool> ReassignJobsFromOfflineWorkerAsync(int workerId);
    }
}
