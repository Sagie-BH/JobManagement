using JobManagement.Domain.Entities;
using JobManagement.Domain.Enums;

namespace JobManagement.Infrastructure.Interfaces
{
    public interface IJobQueue
    {
        Task EnqueueAsync(Job job);
        Task<Job> DequeueAsync();
        Task<IReadOnlyList<Job>> GetPendingJobsAsync();
        Task<int> GetQueueLengthAsync();
        Task<IReadOnlyList<Job>> GetJobsByPriorityAsync(JobPriority priority);
        Task SaveQueueStateAsync();
        Task RestoreQueueStateAsync();
    }
}
