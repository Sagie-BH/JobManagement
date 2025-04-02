using JobManagement.Domain.Enums;

namespace JobManagement.Domain.Entities
{
    public class JobLog : AuditableEntity
    {
        public Guid JobId { get; set; }
        public virtual Job Job { get; set; }
        public LogType LogType { get; set; } 
        public string? Message { get; set; }
        public string? Details { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
