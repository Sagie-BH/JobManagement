using JobManagement.Domain.Entities;

namespace JobManagement.Infrastructure.Interfaces
{
    public interface IJobExecutionService
    {
        Task<bool> ExecuteJobAsync(Job job, CancellationToken cancellationToken = default);
    }
}
