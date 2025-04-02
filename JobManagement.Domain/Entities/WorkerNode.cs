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

        // Added property to represent worker processing power
        // Higher values mean faster job processing (1-10 scale)
        public int Power { get; set; } = 5;

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

        // Get estimated processing time for a job based on this worker's power
        public TimeSpan GetEstimatedProcessingTime(Job job)
        {
            // Base processing time - adjust these values as needed
            double baseMinutes = 10.0;

            // Apply power factor - higher power means faster processing
            double powerFactor = 10.0 / Power; // Power 10 = 1x, Power 5 = 2x, Power 1 = 10x

            // Apply priority multiplier - higher priority jobs run faster
            double priorityMultiplier = 1.0;
            switch (job.Priority)
            {
                case Domain.Enums.JobPriority.Critical:
                case Domain.Enums.JobPriority.Urgent:
                    priorityMultiplier = 0.5; // 2x faster
                    break;
                case Domain.Enums.JobPriority.High:
                    priorityMultiplier = 0.75; // 1.33x faster
                    break;
                case Domain.Enums.JobPriority.Low:
                    priorityMultiplier = 1.5; // 1.5x slower
                    break;
                case Domain.Enums.JobPriority.Deferred:
                    priorityMultiplier = 2.0; // 2x slower
                    break;
            }

            // Calculate final processing time in minutes
            double processingMinutes = baseMinutes * powerFactor * priorityMultiplier;

            return TimeSpan.FromMinutes(processingMinutes);
        }
    }
}