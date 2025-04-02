using JobManagement.Infrastructure.Interfaces.SignalR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace JobManagement.Infrastructure.Services.SignalR
{
    /// <summary>
    /// Base implementation of SignalR service with common functionality for all hub communications
    /// </summary>
    public abstract class BaseSignalRService<THub> : IBaseSignalRService where THub : Hub
    {
        protected readonly IHubContext<THub> _hubContext;
        protected readonly ILogger<BaseSignalRService<THub>> _logger;

        public BaseSignalRService(
            IHubContext<THub> hubContext,
            ILogger<BaseSignalRService<THub>> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task NotifyGroupAsync(string groupName, string method, params object[] args)
        {
            try
            {
                await _hubContext.Clients.Group(groupName).SendAsync(method, args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending notification to group {groupName} with method {method}");
            }
        }

        /// <inheritdoc/>
        public async Task NotifyAllClientsAsync(string method, params object[] args)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync(method, args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending notification to all clients with method {method}");
            }
        }

        /// <inheritdoc/>
        public async Task SendKeepAliveAsync()
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("KeepAlive", DateTime.UtcNow);
                _logger.LogDebug("Sent keep-alive signal to all clients");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending keep-alive signal");
            }
        }
    }
}