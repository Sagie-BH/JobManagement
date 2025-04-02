using JobManagement.Application.Services.JobServices;
using JobManagement.Domain.Enums;
using JobManagement.Infrastructure.Interfaces.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JobManagement.Application.BackgroundServices
{
    public class JobCreationSimulatorService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<JobCreationSimulatorService> _logger;
        private readonly Random _random = new Random();

        // Create jobs more frequently for a more dynamic environment
        private readonly TimeSpan _minInterval = TimeSpan.FromSeconds(15);
        private readonly TimeSpan _maxInterval = TimeSpan.FromSeconds(45);

        // Sample job details
        private readonly string[] _jobNames = new[] {
            "Data Processing", "Daily Backup", "Report Generation", "Content Indexing",
            "Database Cleanup", "Log Analysis", "Email Campaign", "API Integration",
            "Security Scan", "Performance Test", "Batch Import", "Media Conversion",
            "Machine Learning", "Data Mining", "Real-time Analytics", "Video Transcoding",
            "Image Recognition", "Text Classification", "Sentiment Analysis", "Anomaly Detection"
        };

        private readonly string[] _jobDescriptions = new[] {
            "Processing daily data files from multiple sources",
            "Creating backup of critical systems and databases",
            "Generating monthly performance and analytics reports",
            "Indexing new content for search functionality",
            "Cleaning up old database records and optimizing tables",
            "Analyzing system logs for anomalies and issues",
            "Sending targeted email notifications to users",
            "Integrating with third-party APIs and services",
            "Scanning system for security vulnerabilities",
            "Testing system performance under simulated load",
            "Importing batch records from multiple file sources",
            "Converting media files to different formats and resolutions",
            "Running machine learning model training cycles",
            "Mining large datasets for patterns and insights",
            "Processing real-time analytics from user interactions",
            "Transcoding video files to multiple formats",
            "Processing images for object recognition",
            "Classifying text documents into categories",
            "Analyzing text for sentiment and emotional content",
            "Detecting anomalies in system performance metrics"
        };

        public JobCreationSimulatorService(
            IServiceProvider serviceProvider,
            ILogger<JobCreationSimulatorService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Job Creation Simulator Service is starting");

            // Initial delay before starting
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Create 1-3 new jobs in each cycle
                    int jobsToCreate = _random.Next(1, 4);
                    for (int i = 0; i < jobsToCreate; i++)
                    {
                        await CreateSimulatedJobAsync(stoppingToken);
                        
                        // Small delay between job creations
                        if (i < jobsToCreate - 1)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                        }
                    }

                    // Wait a random time before creating the next job(s)
                    int delaySeconds = _random.Next(
                        (int)_minInterval.TotalSeconds,
                        (int)_maxInterval.TotalSeconds);

                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in job creation simulator");

                    // Wait a bit before trying again
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
            }

            _logger.LogInformation("Job Creation Simulator Service is stopping");
        }

        private async Task CreateSimulatedJobAsync(CancellationToken stoppingToken)
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var jobService = scope.ServiceProvider.GetRequiredService<IJobService>();
            var jobSignalRService = scope.ServiceProvider.GetRequiredService<IJobSignalRService>();

            // Generate random job details
            string name = _jobNames[_random.Next(_jobNames.Length)];
            string description = _jobDescriptions[_random.Next(_jobDescriptions.Length)];

            // Add a number to make it unique
            name = $"{name} #{DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 10000}";

            // Random priority with weighted distribution
            JobPriority priority = GetRandomPriority();

            // Random job type
            var jobTypes = Enum.GetValues<JobType>();
            JobType jobType = jobTypes[_random.Next(jobTypes.Length)];

            // Small chance of future scheduling (15%)
            DateTime? scheduledTime = null;
            if (_random.Next(100) < 15) 
            {
                scheduledTime = DateTime.UtcNow.AddMinutes(_random.Next(2, 15));
            }

            _logger.LogInformation($"Creating simulated job: {name} with {priority} priority, type: {jobType}");

            // Create the job
            var job = await jobService.CreateJobAsync(
                name,
                description,
                priority,
                scheduledTime,
                null, // No preferred worker
                jobType);

            // Send an additional notification about job creation
            await jobSignalRService.NotifyGroupAsync("all-jobs", "JobNotification", job.Id, 
                $"New job created: {name} ({priority} priority)", "info");
        }

        private JobPriority GetRandomPriority()
        {
            // Weighted random priority:
            // Critical: 5%
            // Urgent: 10%
            // High: 20%
            // Regular: 40% (most common)
            // Low: 15%
            // Deferred: 10%
            
            int rand = _random.Next(100);
            
            if (rand < 5)
                return JobPriority.Critical;
            else if (rand < 15)
                return JobPriority.Urgent;
            else if (rand < 35)
                return JobPriority.High;
            else if (rand < 75) 
                return JobPriority.Regular;
            else if (rand < 90)
                return JobPriority.Low;
            else
                return JobPriority.Deferred;
        }
    }
}