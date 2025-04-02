using JobManagement.Domain.Enums;

namespace JobManagement.Domain.Entities
{
    public abstract class BaseJobEntity : AuditableEntity
    {
        public string Name { get; set; }
        public string? Description { get; set; }
        public JobStatus Status { get; set; }
        public JobPriority Priority { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int Progress { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
