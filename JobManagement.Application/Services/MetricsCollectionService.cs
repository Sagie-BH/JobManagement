using JobManagement.Infrastructure.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JobManagement.Application.BackgroundServices
{
    public class MetricsCollectionService : BackgroundService
    {
        private readonly ILogger<MetricsCollectionService> _logger;
        private readonly IServiceProvider _serviceProvider;

        // Collection intervals
        private readonly TimeSpan _hourlyInterval = TimeSpan.FromHours(1);
        private readonly TimeSpan _dailyInterval = TimeSpan.FromDays(1);
        private readonly TimeSpan _weeklyInterval = TimeSpan.FromDays(7);

        // Next collection times
        private DateTime _nextHourlyCollection = DateTime.UtcNow;
        private DateTime _nextDailyCollection = DateTime.UtcNow.Date.AddDays(1);
        private DateTime _nextWeeklyCollection = DateTime.UtcNow.Date.AddDays(7 - (int)DateTime.UtcNow.DayOfWeek);

        public MetricsCollectionService(
            IServiceProvider serviceProvider,
            ILogger<MetricsCollectionService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Metrics Collection Service is starting");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CollectMetricsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while collecting metrics");
                }

                // Check again in 1 minute
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }

            _logger.LogInformation("Metrics Collection Service is stopping");
        }

        private async Task CollectMetricsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var metricsService = scope.ServiceProvider.GetRequiredService<IMetricsService>();
            var jobQueue = scope.ServiceProvider.GetRequiredService<IJobQueue>();

            var now = DateTime.UtcNow;

            // Update current queue metrics
            await UpdateQueueMetricsAsync(metricsService, jobQueue);

            // Check if it's time for hourly collection
            if (now >= _nextHourlyCollection)
            {
                await metricsService.CreateMetricSnapshotAsync("Hourly");
                _nextHourlyCollection = now.AddHours(1);
                _logger.LogInformation("Created hourly metrics snapshot");
            }

            // Check if it's time for daily collection
            if (now >= _nextDailyCollection)
            {
                await metricsService.CreateMetricSnapshotAsync("Daily");
                _nextDailyCollection = now.Date.AddDays(1);
                _logger.LogInformation("Created daily metrics snapshot");
            }

            // Check if it's time for weekly collection
            if (now >= _nextWeeklyCollection)
            {
                await metricsService.CreateMetricSnapshotAsync("Weekly");
                _nextWeeklyCollection = now.Date.AddDays(7);
                _logger.LogInformation("Created weekly metrics snapshot");
            }
        }

        private async Task UpdateQueueMetricsAsync(IMetricsService metricsService, IJobQueue jobQueue)
        {
            // Get current queue lengths by priority
            var queueLength = await jobQueue.GetQueueLengthAsync();

            var queueLengthsByPriority = new Dictionary<JobManagement.Domain.Enums.JobPriority, int>();

            // Get queue lengths by priority
            foreach (var priority in Enum.GetValues(typeof(JobManagement.Domain.Enums.JobPriority)).Cast<JobManagement.Domain.Enums.JobPriority>())
            {
                var jobs = await jobQueue.GetJobsByPriorityAsync(priority);
                queueLengthsByPriority[priority] = jobs.Count;
            }

            // Record queue state
            await metricsService.RecordQueueStateAsync(queueLength, queueLengthsByPriority);
        }
    }
}