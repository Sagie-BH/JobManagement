using JobManagement.Domain.Entities;
using JobManagement.Domain.Enums;

namespace JobManagement.Application.dtos
{
    public class JobDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public JobStatus Status { get; set; }
        public JobPriority Priority { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int Progress { get; set; }
        public string ErrorMessage { get; set; }
        public JobType Type { get; set; }

        // Include worker ID but not the full worker object
        public Guid? WorkerNodeId { get; set; }
        public string WorkerNodeName { get; set; }

        // Mapper method
        public static JobDto FromEntity(Job job)
        {
            return new JobDto
            {
                Id = job.Id,
                Name = job.Name,
                Description = job.Description,
                Status = job.Status,
                Priority = job.Priority,
                StartTime = job.StartTime,
                EndTime = job.EndTime,
                Progress = job.Progress,
                ErrorMessage = job.ErrorMessage,
                Type = job.Type,
                WorkerNodeId = job.WorkerNodeId,
                WorkerNodeName = job.AssignedWorker?.Name
            };
        }
    }
}
