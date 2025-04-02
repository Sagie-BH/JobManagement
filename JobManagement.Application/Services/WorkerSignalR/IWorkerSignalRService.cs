using JobManagement.Domain.Entities;

namespace JobManagement.Infrastructure.Interfaces.SignalR
{
    /// <summary>
    /// Interface for SignalR service specific to worker node updates
    /// </summary>
    public interface IWorkerSignalRService : IBaseSignalRService
    {
        /// <summary>
        /// Notifies clients that a worker status has changed
        /// </summary>
        Task NotifyWorkerStatusChangedAsync(Guid workerId, string status);

        /// <summary>
        /// Notifies clients that a worker's load has changed
        /// </summary>
        Task NotifyWorkerLoadChangedAsync(Guid workerId, int currentLoad, int capacity);

        /// <summary>
        /// Notifies clients that a new worker has been registered
        /// </summary>
        Task NotifyWorkerRegisteredAsync(WorkerNode worker);

        /// <summary>
        /// Notifies clients that a worker has been deactivated
        /// </summary>
        Task NotifyWorkerDeactivatedAsync(Guid workerId);

        /// <summary>
        /// Notifies clients that a job has been assigned to a worker
        /// </summary>
        Task NotifyJobAssignedToWorkerAsync(Guid jobId, Guid workerId);
    }
}
