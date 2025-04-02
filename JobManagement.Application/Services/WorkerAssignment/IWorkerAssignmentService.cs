using JobManagement.Domain.Entities;

namespace JobManagement.Infrastructure.Interfaces
{
    public interface IWorkerAssignmentService
    {
        /// <summary>
        /// Find the most appropriate worker for a given job based on job priority, worker power, and load
        /// </summary>
        Task<WorkerNode> FindAvailableWorkerAsync(Job job);

        /// <summary>
        /// Attempts to assign a job to the most appropriate worker
        /// </summary>
        Task<bool> TryAssignJobToWorkerAsync(Job job);

        /// <summary>
        /// Reassigns all jobs from an offline worker to other available workers
        /// </summary>
        Task<bool> ReassignJobsFromOfflineWorkerAsync(Guid workerId);
    }
}
