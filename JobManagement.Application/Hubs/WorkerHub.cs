using Microsoft.AspNetCore.SignalR;

namespace JobManagement.Application.Hubs
{
    /// <summary>
    /// SignalR hub for worker-related real-time updates
    /// </summary>
    public class WorkerHub : Hub
    {
        /// <summary>
        /// Adds the client to a specific worker group to receive targeted updates for that worker
        /// </summary>
        /// <param name="workerId">The ID of the worker to monitor</param>
        public async Task JoinWorkerGroup(string workerId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"worker-{workerId}");
        }

        /// <summary>
        /// Removes the client from a specific worker group
        /// </summary>
        /// <param name="workerId">The ID of the worker to stop monitoring</param>
        public async Task LeaveWorkerGroup(string workerId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"worker-{workerId}");
        }

        /// <summary>
        /// Adds the client to the worker status group to receive updates about all workers
        /// </summary>
        public async Task SubscribeToAllWorkerUpdates()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "all-workers");
        }

        /// <summary>
        /// Removes the client from the worker status group
        /// </summary>
        public async Task UnsubscribeFromAllWorkerUpdates()
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "all-workers");
        }

        /// <summary>
        /// Called when a client connects to the hub
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            // Automatically subscribe to all worker updates
            await SubscribeToAllWorkerUpdates();
            await base.OnConnectedAsync();
        }
    }
}
