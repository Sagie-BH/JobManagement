namespace JobManagement.Domain.Entities
{
    /// <summary>
    /// Represents a complete snapshot of system metrics at a point in time
    /// </summary>
    public class MetricSnapshot : BaseEntity
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string SnapshotData { get; set; } // JSON serialized metrics
        public string SnapshotType { get; set; } // "Hourly", "Daily", "Weekly", etc.
    }
}
