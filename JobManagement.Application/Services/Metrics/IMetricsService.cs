using JobManagement.Domain.Entities;
using JobManagement.Domain.Enums;

namespace JobManagement.Application.Services.Metrics
{
    public interface IMetricsService
    {
        // Job metrics
        Task RecordJobCreatedAsync(Job job);
        Task RecordJobStatusChangedAsync(Job job, JobStatus previousStatus);
        Task RecordJobCompletedAsync(Job job, TimeSpan executionTime);
        Task RecordJobFailedAsync(Job job, string errorMessage);
        Task RecordJobRetriedAsync(Job job);

        // Worker metrics
        Task RecordWorkerRegisteredAsync(WorkerNode worker);
        Task RecordWorkerStatusChangedAsync(WorkerNode worker, string previousStatus);
        Task RecordWorkerUtilizationChangedAsync(WorkerNode worker, int previousLoad);

        // Queue metrics
        Task RecordQueueStateAsync(int totalLength, Dictionary<JobPriority, int> queueLengthsByPriority);
        Task RecordJobProcessedAsync(Job job, TimeSpan waitTime);

        // Snapshots
        Task CreateMetricSnapshotAsync(string snapshotType);

        // Retrieval
        Task<JobMetric> GetLatestJobMetricsAsync();
        Task<WorkerMetric> GetLatestWorkerMetricsAsync();
        Task<QueueMetric> GetLatestQueueMetricsAsync();
        Task<IEnumerable<MetricSnapshot>> GetMetricSnapshotsAsync(string snapshotType, DateTime from, DateTime to);
    }
}
