namespace JobManagement.Domain.Entities
{
    public class JobLog : AuditableEntity
    {
        public int JobId { get; set; }
        public virtual Job Job { get; set; }
        public string LogType { get; set; } 
        public string Message { get; set; }
        public string Details { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
