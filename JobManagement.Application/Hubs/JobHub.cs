using Microsoft.AspNetCore.SignalR;

namespace JobManagement.Application.Hubs
{
    /// <summary>
    /// SignalR hub for job-related real-time updates
    /// </summary>
    public class JobHub : Hub
    {
        /// <summary>
        /// Adds the client to a specific job group to receive targeted updates for that job
        /// </summary>
        /// <param name="jobId">The ID of the job to monitor</param>
        public async Task JoinJobGroup(string jobId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"job-{jobId}");
        }

        /// <summary>
        /// Removes the client from a specific job group
        /// </summary>
        /// <param name="jobId">The ID of the job to stop monitoring</param>
        public async Task LeaveJobGroup(string jobId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"job-{jobId}");
        }

        /// <summary>
        /// Adds the client to the job status group to receive updates about all jobs
        /// </summary>
        public async Task SubscribeToAllJobUpdates()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "all-jobs");
        }
        public async Task SubscribeToJobType(string jobType)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"jobType-{jobType}");
        }

        public async Task SubscribeToJobStatus(string status)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"jobStatus-{status}");
        }

        /// <summary>
        /// Removes the client from the job status group
        /// </summary>
        public async Task UnsubscribeFromAllJobUpdates()
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "all-jobs");
        }

        /// <summary>
        /// Called when a client connects to the hub
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            // Automatically subscribe to all job updates
            await SubscribeToAllJobUpdates();
            await base.OnConnectedAsync();
        }
    }
}
