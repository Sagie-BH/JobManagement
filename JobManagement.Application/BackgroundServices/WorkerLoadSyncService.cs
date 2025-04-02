// Add this new background service class to JobManagement.Application/BackgroundServices
using JobManagement.Application.BackgroundServices;
using JobManagement.Application.Services.WorkerNodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JobManagement.Application.BackgroundServices
{
    public class WorkerLoadSyncService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<WorkerLoadSyncService> _logger;
        private readonly TimeSpan _syncInterval = TimeSpan.FromSeconds(15); // Run every 5 minutes

        public WorkerLoadSyncService(
            IServiceProvider serviceProvider,
            ILogger<WorkerLoadSyncService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker Load Synchronization Service is starting");

            // Wait a bit on startup to allow the system to initialize
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Running worker load synchronization");

                    using var scope = _serviceProvider.CreateScope();
                    var workerNodeService = scope.ServiceProvider.GetRequiredService<IWorkerNodeService>();

                    // Recalculate worker loads
                    await workerNodeService.RecalculateWorkerLoadsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in worker load synchronization service");
                }

                await Task.Delay(_syncInterval, stoppingToken);
            }

            _logger.LogInformation("Worker Load Synchronization Service is stopping");
        }
    }
}
