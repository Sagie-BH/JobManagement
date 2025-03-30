using JobManagement.Domain.Entities;
using JobManagement.Domain.Enums;

namespace JobManagement.Infrastructure.Queue
{
    public class JobQueueState : BaseEntity
    {
        public DateTime LastUpdated { get; set; }
        public string QueueData { get; set; } // JSON representation of queue

        // Helper methods for serialization/deserialization
        public static JobQueueState FromQueue(IDictionary<JobPriority, IEnumerable<int>> queueState)
        {
            var data = System.Text.Json.JsonSerializer.Serialize(queueState);

            return new JobQueueState
            {
                LastUpdated = DateTime.UtcNow,
                QueueData = data
            };
        }

        public Dictionary<JobPriority, List<int>> ToQueueDictionary()
        {
            if (string.IsNullOrEmpty(QueueData))
            {
                return new Dictionary<JobPriority, List<int>>();
            }

            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<JobPriority, List<int>>>(QueueData);
        }
    }
}
