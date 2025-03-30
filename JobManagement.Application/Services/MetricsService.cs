using JobManagement.Domain.Entities;
using JobManagement.Domain.Enums;
using JobManagement.Domain.Interfaces;
using JobManagement.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace JobManagement.Infrastructure.Services
{
    public class MetricsService : IMetricsService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<MetricsService> _logger;

        // In-memory counters for current metrics
        private int _jobsCreatedCounter = 0;
        private int _jobsCompletedCounter = 0;
        private int _jobsFailedCounter = 0;
        private int _totalRetriesCounter = 0;
        private List<double> _executionTimesMs = new List<double>();
        private readonly object _lockObject = new object();

        public MetricsService(IUnitOfWork unitOfWork, ILogger<MetricsService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task RecordJobCreatedAsync(Job job)
        {
            try
            {
                lock (_lockObject)
                {
                    _jobsCreatedCounter++;
                }

                _logger.LogDebug($"Recorded job created: {job.Id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording job created metric");
            }
        }

        public async Task RecordJobStatusChangedAsync(Job job, JobStatus previousStatus)
        {
            try
            {
                // This would typically update counters based on status changes
                _logger.LogDebug($"Recorded job status change: {job.Id} from {previousStatus} to {job.Status}");

                // If this is a terminal state, we may want to calculate some metrics
                if (job.Status == JobStatus.Completed || job.Status == JobStatus.Failed)
                {
                    if (job.StartTime.HasValue && job.EndTime.HasValue)
                    {
                        var executionTime = job.EndTime.Value - job.StartTime.Value;
                        lock (_lockObject)
                        {
                            _executionTimesMs.Add(executionTime.TotalMilliseconds);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording job status change metric");
            }
        }

        public async Task RecordJobCompletedAsync(Job job, TimeSpan executionTime)
        {
            try
            {
                lock (_lockObject)
                {
                    _jobsCompletedCounter++;
                    _executionTimesMs.Add(executionTime.TotalMilliseconds);
                }

                _logger.LogDebug($"Recorded job completed: {job.Id}, execution time: {executionTime.TotalMilliseconds}ms");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording job completed metric");
            }
        }

        public async Task RecordJobFailedAsync(Job job, string errorMessage)
        {
            try
            {
                lock (_lockObject)
                {
                    _jobsFailedCounter++;
                }

                _logger.LogDebug($"Recorded job failed: {job.Id}, error: {errorMessage}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording job failed metric");
            }
        }

        public async Task RecordJobRetriedAsync(Job job)
        {
            try
            {
                lock (_lockObject)
                {
                    _totalRetriesCounter++;
                }

                _logger.LogDebug($"Recorded job retry: {job.Id}, retry count: {job.CurrentRetryCount}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording job retry metric");
            }
        }

        public async Task RecordWorkerRegisteredAsync(WorkerNode worker)
        {
            try
            {
                _logger.LogDebug($"Recorded worker registered: {worker.Id}");

                // Update worker metrics
                await UpdateWorkerMetricsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording worker registered metric");
            }
        }

        public async Task RecordWorkerStatusChangedAsync(WorkerNode worker, string previousStatus)
        {
            try
            {
                _logger.LogDebug($"Recorded worker status change: {worker.Id} from {previousStatus} to {worker.Status}");

                // Update worker metrics
                await UpdateWorkerMetricsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording worker status change metric");
            }
        }

        public async Task RecordWorkerUtilizationChangedAsync(WorkerNode worker, int previousLoad)
        {
            try
            {
                _logger.LogDebug($"Recorded worker utilization change: {worker.Id} from {previousLoad} to {worker.CurrentLoad}");

                // Update worker metrics
                await UpdateWorkerMetricsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording worker utilization change metric");
            }
        }

        public async Task RecordQueueStateAsync(int totalLength, Dictionary<JobPriority, int> queueLengthsByPriority)
        {
            try
            {
                var queueMetric = new QueueMetric
                {
                    Timestamp = DateTime.UtcNow,
                    TotalQueueLength = totalLength,
                    HighPriorityJobs = queueLengthsByPriority.ContainsKey(JobPriority.High) ? queueLengthsByPriority[JobPriority.High] : 0,
                    RegularPriorityJobs = queueLengthsByPriority.ContainsKey(JobPriority.Regular) ? queueLengthsByPriority[JobPriority.Regular] : 0,
                    LowPriorityJobs = queueLengthsByPriority.ContainsKey(JobPriority.Low) ? queueLengthsByPriority[JobPriority.Low] : 0,
                    // Other properties would be calculated elsewhere
                };

                await _unitOfWork.Repository<QueueMetric>().AddAsync(queueMetric);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogDebug($"Recorded queue state: {totalLength} total jobs");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording queue state metric");
            }
        }

        public async Task RecordJobProcessedAsync(Job job, TimeSpan waitTime)
        {
            try
            {
                _logger.LogDebug($"Recorded job processed: {job.Id}, wait time: {waitTime.TotalMilliseconds}ms");

                // This would typically update a counter or rolling average
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording job processed metric");
            }
        }

        public async Task CreateMetricSnapshotAsync(string snapshotType)
        {
            try
            {
                // Get the most recent metrics
                var jobMetric = await GetLatestJobMetricsAsync();
                var workerMetric = await GetLatestWorkerMetricsAsync();
                var queueMetric = await GetLatestQueueMetricsAsync();

                // Create a snapshot object to hold all metrics
                var snapshot = new
                {
                    JobMetrics = jobMetric,
                    WorkerMetrics = workerMetric,
                    QueueMetrics = queueMetric,
                    GeneratedAt = DateTime.UtcNow
                };

                // Serialize to JSON
                var snapshotJson = JsonSerializer.Serialize(snapshot);

                // Create and save the metric snapshot
                var metricSnapshot = new MetricSnapshot
                {
                    Timestamp = DateTime.UtcNow,
                    SnapshotData = snapshotJson,
                    SnapshotType = snapshotType
                };

                await _unitOfWork.Repository<MetricSnapshot>().AddAsync(metricSnapshot);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation($"Created {snapshotType} metric snapshot");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating {snapshotType} metric snapshot");
            }
        }

        public async Task<JobMetric> GetLatestJobMetricsAsync()
        {
            try
            {
                // Get counts of jobs by status
                var jobRepo = _unitOfWork.Repository<Job>();
                var pendingCount = await jobRepo.CountAsync(j => j.Status == JobStatus.Pending);
                var runningCount = await jobRepo.CountAsync(j => j.Status == JobStatus.Running);
                var completedCount = await jobRepo.CountAsync(j => j.Status == JobStatus.Completed);
                var failedCount = await jobRepo.CountAsync(j => j.Status == JobStatus.Failed);
                var stoppedCount = await jobRepo.CountAsync(j => j.Status == JobStatus.Stopped);

                double avgExecutionTime = 0;
                lock (_lockObject)
                {
                    avgExecutionTime = _executionTimesMs.Count > 0 ? _executionTimesMs.Average() : 0;
                }

                // Calculate success rate
                double successRate = 0;
                int totalCompleted = _jobsCompletedCounter + _jobsFailedCounter;
                if (totalCompleted > 0)
                {
                    successRate = ((double)_jobsCompletedCounter / totalCompleted) * 100;
                }

                // Create and return the job metric
                var jobMetric = new JobMetric
                {
                    Timestamp = DateTime.UtcNow,
                    TotalJobs = pendingCount + runningCount + completedCount + failedCount + stoppedCount,
                    PendingJobs = pendingCount,
                    RunningJobs = runningCount,
                    CompletedJobs = completedCount,
                    FailedJobs = failedCount,
                    StoppedJobs = stoppedCount,
                    AverageExecutionTimeMs = avgExecutionTime,
                    SuccessRate = successRate,
                    TotalRetries = _totalRetriesCounter
                };

                return jobMetric;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting latest job metrics");
                return new JobMetric();
            }
        }

        public async Task<WorkerMetric> GetLatestWorkerMetricsAsync()
        {
            try
            {
                // Query the database for worker metrics
                var workerRepo = _unitOfWork.Repository<WorkerNode>();
                var allWorkers = await workerRepo.GetAllAsync();

                var activeWorkers = allWorkers.Count(w => w.Status == "Active");
                var idleWorkers = allWorkers.Count(w => w.Status == "Idle");
                var offlineWorkers = allWorkers.Count(w => w.Status == "Offline");

                int totalCapacity = allWorkers.Sum(w => w.Capacity);
                int currentLoad = allWorkers.Sum(w => w.CurrentLoad);

                double utilization = totalCapacity > 0 ? ((double)currentLoad / totalCapacity) * 100 : 0;

                // Create and return the worker metric
                var workerMetric = new WorkerMetric
                {
                    Timestamp = DateTime.UtcNow,
                    TotalWorkers = allWorkers.Count,
                    ActiveWorkers = activeWorkers,
                    IdleWorkers = idleWorkers,
                    OfflineWorkers = offlineWorkers,
                    AverageWorkerUtilization = utilization,
                    TotalCapacity = totalCapacity,
                    CurrentLoad = currentLoad
                };

                return workerMetric;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting latest worker metrics");
                return new WorkerMetric();
            }
        }

        public async Task<QueueMetric> GetLatestQueueMetricsAsync()
        {
            try
            {
                // Try to get the most recent queue metric from the database
                var queueMetricRepo = _unitOfWork.Repository<QueueMetric>();

                // Fix ambiguous method call by explicitly using the string overload
                var queueMetrics = await queueMetricRepo.GetAsync(
                    predicate: null,
                    orderBy: q => q.OrderByDescending(m => m.Timestamp),
                    includeString: null,
                    disableTracking: true
                );

                if (queueMetrics.Any())
                {
                    return queueMetrics.First();
                }

                // If no metrics exist yet, return a default one
                return new QueueMetric
                {
                    Timestamp = DateTime.UtcNow,
                    TotalQueueLength = 0,
                    HighPriorityJobs = 0,
                    RegularPriorityJobs = 0,
                    LowPriorityJobs = 0,
                    AverageWaitTimeMs = 0,
                    JobsProcessedLastMinute = 0
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting latest queue metrics");
                return new QueueMetric();
            }
        }

        public async Task<IEnumerable<MetricSnapshot>> GetMetricSnapshotsAsync(string snapshotType, DateTime from, DateTime to)
        {
            try
            {
                var snapshotRepo = _unitOfWork.Repository<MetricSnapshot>();

                // Fix ambiguous method call by explicitly using the string overload
                var snapshots = await snapshotRepo.GetAsync(
                    predicate: s => s.SnapshotType == snapshotType && s.Timestamp >= from && s.Timestamp <= to,
                    orderBy: s => s.OrderBy(m => m.Timestamp),
                    includeString: null,
                    disableTracking: true
                );

                return snapshots;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting {snapshotType} metric snapshots");
                return Enumerable.Empty<MetricSnapshot>();
            }
        }

        private async Task UpdateWorkerMetricsAsync()
        {
            try
            {
                var workerMetric = await GetLatestWorkerMetricsAsync();
                await _unitOfWork.Repository<WorkerMetric>().AddAsync(workerMetric);
                await _unitOfWork.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating worker metrics");
            }
        }
    }
}