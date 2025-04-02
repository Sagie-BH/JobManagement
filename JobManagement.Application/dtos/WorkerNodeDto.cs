using JobManagement.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobManagement.Application.dtos
{
    public class WorkerNodeDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Endpoint { get; set; }
        public string Status { get; set; }
        public DateTime LastHeartbeat { get; set; }
        public int Capacity { get; set; }
        public int CurrentLoad { get; set; }
        public int Power { get; set; }

        // Only include job IDs, not the full job objects
        public ICollection<Guid> AssignedJobIds { get; set; } = new List<Guid>();

        // Mapper method
        public static WorkerNodeDto FromEntity(WorkerNode worker)
        {
            return new WorkerNodeDto
            {
                Id = worker.Id,
                Name = worker.Name,
                Endpoint = worker.Endpoint,
                Status = worker.Status,
                LastHeartbeat = worker.LastHeartbeat,
                Capacity = worker.Capacity,
                CurrentLoad = worker.CurrentLoad,
                Power = worker.Power,
                AssignedJobIds = worker.AssignedJobs?.Select(j => j.Id).ToList() ?? new List<Guid>()
            };
        }
    }
}
