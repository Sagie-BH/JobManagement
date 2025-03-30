namespace JobManagement.Domain.Entities
{
    public class QueueMetric : BaseEntity
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public int TotalQueueLength { get; set; }
        public int HighPriorityJobs { get; set; }
        public int RegularPriorityJobs { get; set; }
        public int LowPriorityJobs { get; set; }
        public double AverageWaitTimeMs { get; set; }
        public int JobsProcessedLastMinute { get; set; }
    }
}
