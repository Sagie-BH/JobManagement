namespace JobManagement.Domain.Entities
{
    public class WorkerMetric : BaseEntity
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public int TotalWorkers { get; set; }
        public int ActiveWorkers { get; set; }
        public int IdleWorkers { get; set; }
        public int OfflineWorkers { get; set; }
        public double AverageWorkerUtilization { get; set; } // Stored as percentage (0-100)
        public int TotalCapacity { get; set; }
        public int CurrentLoad { get; set; }
    }
}
