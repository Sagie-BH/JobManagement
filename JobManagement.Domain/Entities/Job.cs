using JobManagement.Domain.Enums;
using System.Text.Json.Serialization;

namespace JobManagement.Domain.Entities
{
    public class Job : BaseJobEntity
    {
        // Job-specific properties
        public string ExecutionPayload { get; set; }
        public int MaxRetryAttempts { get; set; } = 3;
        public int CurrentRetryCount { get; set; } = 0;
        public Guid? WorkerNodeId { get; set; }
        [JsonIgnore]
        public virtual WorkerNode? AssignedWorker { get; set; }
        public DateTime? ScheduledStartTime { get; set; }
        public JobType Type { get; set; } = JobType.Generic;
        public virtual ICollection<JobLog> ExecutionLogs { get; set; } = new List<JobLog>();

        // Additional helper methods
        public bool CanRetry()
        {
            return Status == JobStatus.Failed && CurrentRetryCount < MaxRetryAttempts;
        }

        public void IncrementRetryCount()
        {
            CurrentRetryCount++;
        }

        public void UpdateProgress(int newProgress)
        {
            if (newProgress >= 0 && newProgress <= 100)
            {
                Progress = newProgress;
            }
        }
    }
}
