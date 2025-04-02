using JobManagement.Application.Services.Metrics;
using JobManagement.Domain.Entities;
using JobManagement.Domain.Enums;
using JobManagement.Infrastructure.Interfaces;
using JobManagement.Infrastructure.Interfaces.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JobManagement.Application.BackgroundServices
{
    /// <summary>
    /// Service that monitors system health and broadcasts statistics via SignalR
    /// to keep clients updated with real-time data
    /// </summary>
    public class SystemHealthMonitoringService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SystemHealthMonitoringService> _logger;

        // Different update intervals for different metrics
        private readonly TimeSpan _quickStatsInterval = TimeSpan.FromSeconds(3);
        private readonly TimeSpan _detailedStatsInterval = TimeSpan.FromSeconds(10);
        private readonly TimeSpan _systemHealthInterval = TimeSpan.FromSeconds(15);
        private readonly TimeSpan _keepAliveInterval = TimeSpan.FromSeconds(30);

        // Timestamps for last updates
        private DateTime _lastQuickStatsUpdate = DateTime.MinValue;
        private DateTime _lastDetailedStatsUpdate = DateTime.MinValue;
        private DateTime _lastHealthUpdate = DateTime.MinValue;
        private DateTime _lastKeepAlive = DateTime.MinValue;

        public SystemHealthMonitoringService(
            IServiceProvider serviceProvider,
            ILogger<SystemHealthMonitoringService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("System Health Monitoring Service is starting");

            // Wait a bit for system initialization
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.UtcNow;

                    // Send quick stats updates very frequently (jobs, queue counts)
                    if (now - _lastQuickStatsUpdate >= _quickStatsInterval)
                    {
                        await SendQuickStatsUpdateAsync();
                        _lastQuickStatsUpdate = now;
                    }

                    // Send more detailed stats less frequently
                    if (now - _lastDetailedStatsUpdate >= _detailedStatsInterval)
                    {
                        await SendDetailedStatsUpdateAsync();
                        _lastDetailedStatsUpdate = now;
                    }

                    // Send full system health update periodically
                    if (now - _lastHealthUpdate >= _systemHealthInterval)
                    {
                        await SendSystemHealthUpdateAsync();
                        _lastHealthUpdate = now;
                    }

                    // Send keep-alive signals to maintain connection
                    if (now - _lastKeepAlive >= _keepAliveInterval)
                    {
                        await SendKeepAliveAsync();
                        _lastKeepAlive = now;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in system health monitoring service");
                }

                // Short delay between iterations
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }

            _logger.LogInformation("System Health Monitoring Service is stopping");
        }

        private async Task SendQuickStatsUpdateAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var jobSignalRService = scope.ServiceProvider.GetRequiredService<IJobSignalRService>();
            var workerSignalRService = scope.ServiceProvider.GetRequiredService<IWorkerSignalRService>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            try
            {
                // Get current job status counts
                var pendingCount = await unitOfWork.Repository<Job>().CountAsync(j => j.Status == JobStatus.Pending);
                var runningCount = await unitOfWork.Repository<Job>().CountAsync(j => j.Status == JobStatus.Running);
                var completedCount = await unitOfWork.Repository<Job>().CountAsync(j => j.Status == JobStatus.Completed);
                var failedCount = await unitOfWork.Repository<Job>().CountAsync(j => j.Status == JobStatus.Failed);

                // Send quick job stats update
                var jobStats = new
                {
                    Pending = pendingCount,
                    Running = runningCount,
                    Completed = completedCount,
                    Failed = failedCount,
                    Total = pendingCount + runningCount + completedCount + failedCount,
                    Timestamp = DateTime.UtcNow
                };

                await jobSignalRService.NotifyAllClientsAsync("QuickStatsUpdate", jobStats);

                // Get worker status counts
                var workers = await unitOfWork.Repository<WorkerNode>().GetAllAsync();
                var workerStats = new
                {
                    Total = workers.Count,
                    Active = workers.Count(w => w.Status == "Active"),
                    Idle = workers.Count(w => w.Status == "Idle"),
                    Offline = workers.Count(w => w.Status == "Offline"),
                    AverageLoad = workers.Any() ? workers.Average(w => (double)w.CurrentLoad / w.Capacity) * 100 : 0,
                    Timestamp = DateTime.UtcNow
                };

                await workerSignalRService.NotifyAllClientsAsync("QuickStatsUpdate", workerStats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending quick stats update");
            }
        }

        private async Task SendDetailedStatsUpdateAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var jobSignalRService = scope.ServiceProvider.GetRequiredService<IJobSignalRService>();
            var workerSignalRService = scope.ServiceProvider.GetRequiredService<IWorkerSignalRService>();
            var metricsService = scope.ServiceProvider.GetRequiredService<IMetricsService>();

            try
            {
                // Get job metrics
                var jobMetrics = await metricsService.GetLatestJobMetricsAsync();
                var workerMetrics = await metricsService.GetLatestWorkerMetricsAsync();
                var queueMetrics = await metricsService.GetLatestQueueMetricsAsync();

                // Send detailed metrics to job hub
                var jobDetailedStats = new
                {
                    jobMetrics.TotalJobs,
                    jobMetrics.PendingJobs,
                    jobMetrics.RunningJobs,
                    jobMetrics.CompletedJobs,
                    jobMetrics.FailedJobs,
                    jobMetrics.StoppedJobs,
                    jobMetrics.AverageExecutionTimeMs,
                    jobMetrics.SuccessRate,
                    jobMetrics.TotalRetries,
                    QueueLength = queueMetrics.TotalQueueLength,
                    HighPriorityInQueue = queueMetrics.HighPriorityJobs,
                    RegularPriorityInQueue = queueMetrics.RegularPriorityJobs,
                    LowPriorityInQueue = queueMetrics.LowPriorityJobs,
                    AverageWaitTimeMs = queueMetrics.AverageWaitTimeMs,
                    Timestamp = DateTime.UtcNow
                };

                await jobSignalRService.NotifyAllClientsAsync("DetailedStatsUpdate", jobDetailedStats);

                // Send detailed worker metrics
                var workerDetailedStats = new
                {
                    workerMetrics.TotalWorkers,
                    workerMetrics.ActiveWorkers,
                    workerMetrics.IdleWorkers,
                    workerMetrics.OfflineWorkers,
                    UtilizationPercentage = workerMetrics.AverageWorkerUtilization,
                    workerMetrics.TotalCapacity,
                    workerMetrics.CurrentLoad,
                    Timestamp = DateTime.UtcNow
                };

                await workerSignalRService.NotifyAllClientsAsync("DetailedStatsUpdate", workerDetailedStats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending detailed stats update");
            }
        }

        private async Task SendSystemHealthUpdateAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var jobSignalRService = scope.ServiceProvider.GetRequiredService<IJobSignalRService>();
            var workerSignalRService = scope.ServiceProvider.GetRequiredService<IWorkerSignalRService>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var metricsService = scope.ServiceProvider.GetRequiredService<IMetricsService>();

            try
            {
                // Get comprehensive metrics
                var jobMetrics = await metricsService.GetLatestJobMetricsAsync();
                var workerMetrics = await metricsService.GetLatestWorkerMetricsAsync();
                var queueMetrics = await metricsService.GetLatestQueueMetricsAsync();

                // System health data to send to both hubs
                var systemHealth = new
                {
                    Status = DetermineSystemStatus(jobMetrics, workerMetrics, queueMetrics),
                    OverallPerformance = CalculateOverallPerformance(jobMetrics, workerMetrics, queueMetrics),
                    CPUUtilization = GenerateSimulatedCPUUtilization(),
                    MemoryUtilization = GenerateSimulatedMemoryUtilization(),
                    NetworkUtilization = GenerateSimulatedNetworkUtilization(),
                    Uptime = GenerateSimulatedUptime(),
                    LatestActivity = DateTime.UtcNow,
                    SystemLogs = GenerateSimulatedLogs(),
                    Timestamp = DateTime.UtcNow
                };

                // Send to both job and worker hubs
                await jobSignalRService.NotifyAllClientsAsync("SystemHealthUpdate", systemHealth);
                await workerSignalRService.NotifyAllClientsAsync("SystemHealthUpdate", systemHealth);

                _logger.LogInformation("Sent system health update to all clients");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending system health update");
            }
        }

        private async Task SendKeepAliveAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var jobSignalRService = scope.ServiceProvider.GetRequiredService<IJobSignalRService>();
            var workerSignalRService = scope.ServiceProvider.GetRequiredService<IWorkerSignalRService>();

            try
            {
                // Send keep-alive signals to both hubs
                await jobSignalRService.SendKeepAliveAsync();
                await workerSignalRService.SendKeepAliveAsync();

                _logger.LogDebug("Sent keep-alive signals to all clients");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending keep-alive signals");
            }
        }

        #region Simulation Helper Methods

        // Determine overall system status based on metrics
        private string DetermineSystemStatus(JobMetric jobMetrics, WorkerMetric workerMetrics, QueueMetric queueMetrics)
        {
            if (workerMetrics.ActiveWorkers == 0)
                return "Critical"; // No active workers

            if (workerMetrics.AverageWorkerUtilization > 90)
                return "Warning"; // System under heavy load

            if (queueMetrics.TotalQueueLength > 10 && workerMetrics.ActiveWorkers < 3)
                return "Warning"; // Queue building up with few workers

            return "Healthy"; // Default status
        }

        // Calculate overall performance score (0-100)
        private int CalculateOverallPerformance(JobMetric jobMetrics, WorkerMetric workerMetrics, QueueMetric queueMetrics)
        {
            // This would be a complex calculation in a real system
            // For simulation, we'll generate a value that trends around 70-90 with some variance
            var random = new Random();
            return random.Next(70, 91);
        }

        // Generate simulated CPU utilization (percentage)
        private int GenerateSimulatedCPUUtilization()
        {
            var random = new Random();
            return random.Next(20, 81); // 20% to 80%
        }

        // Generate simulated memory utilization (percentage)
        private int GenerateSimulatedMemoryUtilization()
        {
            var random = new Random();
            return random.Next(30, 76); // 30% to 75% 
        }

        // Generate simulated network utilization (percentage)
        private int GenerateSimulatedNetworkUtilization()
        {
            var random = new Random();
            return random.Next(10, 61); // 10% to 60%
        }

        // Generate simulated uptime
        private string GenerateSimulatedUptime()
        {
            var random = new Random();
            int days = random.Next(1, 31);
            int hours = random.Next(0, 24);
            int minutes = random.Next(0, 60);

            return $"{days}d {hours}h {minutes}m";
        }

        // Generate simulated system logs
        private object[] GenerateSimulatedLogs()
        {
            var logMessages = new[]
            {
                "System startup completed",
                "Worker node registered",
                "Job queue optimization completed",
                "Database connection pool refreshed",
                "Memory usage optimized",
                "Worker load balancing performed",
                "Job processing metrics calculated",
                "Worker heartbeat monitoring active",
                "System health check completed",
                "Network performance optimized"
            };

            var random = new Random();
            var logs = new List<object>();

            for (int i = 0; i < 5; i++)
            {
                logs.Add(new
                {
                    Message = logMessages[random.Next(logMessages.Length)],
                    Timestamp = DateTime.UtcNow.AddMinutes(-random.Next(1, 120)),
                    Level = random.Next(5) == 0 ? "Warning" : "Info" // Occasional warnings
                });
            }

            return logs.OrderByDescending(l => ((dynamic)l).Timestamp).ToArray();
        }

        #endregion
    }
}