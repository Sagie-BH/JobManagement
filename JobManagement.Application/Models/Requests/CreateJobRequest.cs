using JobManagement.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace JobManagement.Application.Models.Requests
{
    public class CreateJobRequest
    {
        [Required(ErrorMessage = "Job name is required")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Job name must be between 3 and 100 characters")]
        public string Name { get; set; }

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string Description { get; set; }

        [EnumDataType(typeof(JobPriority), ErrorMessage = "Invalid job priority")]
        public JobPriority Priority { get; set; } = JobPriority.Regular;

        public DateTime? ScheduledStartTime { get; set; }

        [EnumDataType(typeof(JobType), ErrorMessage = "Invalid job type")]
        public JobType Type { get; set; } = JobType.Generic;

        // Optional maximum runtime in minutes
        [Range(1, 1440, ErrorMessage = "Maximum runtime must be between 1 minute and 24 hours")]
        public int? MaximumRuntimeMinutes { get; set; }

        // Optional target worker ID - if null, system will assign the best worker
        public Guid? PreferredWorkerId { get; set; }
    }
}
