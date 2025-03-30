using JobManagement.Domain.Constants;

namespace JobManagement.Domain.Entities
{
    public class WorkerNode : AuditableEntity
    {
        public string Name { get; set; }
        public string Endpoint { get; set; }
        public string Status { get; set; } 
        public DateTime LastHeartbeat { get; set; }
        public int Capacity { get; set; } = 5;
        public int CurrentLoad { get; set; } = 0;
        public virtual ICollection<Job> AssignedJobs { get; set; } = [];

        // Helper methods
        public bool IsAvailable()
        {
            return Status == WorkerConstants.Status.Active && CurrentLoad < Capacity;
        }

        public void UpdateHeartbeat()
        {
            LastHeartbeat = DateTime.UtcNow;
            Status = WorkerConstants.Status.Active;
        }

        public void IncreaseLoad()
        {
            CurrentLoad++;
        }

        public void DecreaseLoad()
        {
            if (CurrentLoad > 0)
                CurrentLoad--;
        }
    }
}
