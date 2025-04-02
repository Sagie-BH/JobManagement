namespace JobManagement.Infrastructure.Interfaces.SignalR
{
    /// <summary>
    /// Base interface for SignalR services that defines common functionality
    /// </summary>
    public interface IBaseSignalRService
    {
        /// <summary>
        /// Notifies a specific group with a message and parameters
        /// </summary>
        Task NotifyGroupAsync(string groupName, string method, params object[] args);

        /// <summary>
        /// Notifies all connected clients with a message and parameters
        /// </summary>
        Task NotifyAllClientsAsync(string method, params object[] args);

        /// <summary>
        /// Sends a keep-alive signal to maintain active connections
        /// </summary>
        Task SendKeepAliveAsync();
    }
}
