using JobManagement.Domain.Enums;

namespace JobManagement.Application.Models.Requests
{

    public class JobFilterRequest
    {
        public string? NameFilter { get; set; }
        public JobStatus? StatusFilter { get; set; }
        public JobPriority? PriorityFilter { get; set; }
        public string? SortBy { get; set; }
        public bool SortAscending { get; set; } = true;
    }
}
