using JobManagement.Application.Models.Requests;
using JobManagement.Domain.Entities;
using JobManagement.Domain.Enums;

namespace JobManagement.Application.Services.JobServices
{
    public interface IJobService
    {
        Task AddJobLogAsync(Guid jobId, LogType logType, string message, string details = null);
        Task<Job> CreateJobAsync(string name, string description, JobPriority priority, DateTime? scheduledStartTime = null, Guid? preferredWorkerId = null, JobType type = JobType.Generic);
        Task<bool> DeleteJobAsync(Guid jobId);
        Task<IReadOnlyList<Job>> GetAllJobsAsync();
        Task<Job> GetJobByIdAsync(Guid id);
        Task<IReadOnlyList<Job>> GetJobsAsync(JobFilterRequest jobFilterRequest);
        Task<IReadOnlyList<Job>> GetJobsByPriorityAsync(JobPriority priority);
        Task<IReadOnlyList<Job>> GetJobsByStatusAsync(JobStatus status);
        Task<bool> RestartJobAsync(Guid jobId);
        Task<bool> RetryJobAsync(Guid jobId);
        Task<bool> StopJobAsync(Guid jobId);
        Task<bool> UpdateJobProgressAsync(Guid jobId, int progress);
        Task<bool> UpdateJobStatusAsync(Guid jobId, JobStatus newStatus);
    }
}