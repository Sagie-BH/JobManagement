namespace JobManagement.Domain.Entities
{
    public class JobMetric : BaseEntity
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public int TotalJobs { get; set; }
        public int PendingJobs { get; set; }
        public int RunningJobs { get; set; }
        public int CompletedJobs { get; set; }
        public int FailedJobs { get; set; }
        public int StoppedJobs { get; set; }
        public double AverageExecutionTimeMs { get; set; }
        public double SuccessRate { get; set; } // Stored as percentage (0-100)
        public int TotalRetries { get; set; }
    }
}
