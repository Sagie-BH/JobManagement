using JobManagement.Domain.Entities;
using JobManagement.Domain.Enums;

namespace JobManagement.Infrastructure.Interfaces.SignalR
{
    /// <summary>
    /// Interface for SignalR service specific to job updates
    /// </summary>
    public interface IJobSignalRService : IBaseSignalRService
    {
        Task NotifyJobCreatedAsync(Job job);
        Task NotifyJobDeletedAsync(Guid jobId);
        Task NotifyJobErrorAsync(Guid jobId, string errorMessage);
        Task NotifyJobProgressUpdatedAsync(Guid jobId, int progress);
        Task NotifyJobStatusChangedAsync(Guid jobId, string status);
    }
}
