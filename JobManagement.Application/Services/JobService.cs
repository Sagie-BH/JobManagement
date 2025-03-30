using JobManagement.Domain.Entities;
using JobManagement.Domain.Enums;
using JobManagement.Domain.Interfaces;
using JobManagement.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace JobManagement.Application.Services
{
    public class JobService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IJobQueue _jobQueue;
        private readonly ILogger<JobService> _logger;

        public JobService(
            IUnitOfWork unitOfWork,
            IJobQueue jobQueue,
            ILogger<JobService> logger)
        {
            _unitOfWork = unitOfWork;
            _jobQueue = jobQueue;
            _logger = logger;
        }

        public async Task<Job> CreateJobAsync(string name, string description, JobPriority priority, DateTime? scheduledStartTime = null)
        {
            try
            {
                var job = new Job
                {
                    Name = name,
                    Description = description,
                    Status = JobStatus.Pending,
                    Priority = priority,
                    Progress = 0,
                    ScheduledStartTime = scheduledStartTime
                };

                // Save the job to the database
                await _unitOfWork.Repository<Job>().AddAsync(job);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation($"Created job {job.Id}: {job.Name} with {job.Priority} priority");

                // If not scheduled for the future, add to queue immediately
                if (scheduledStartTime == null || scheduledStartTime <= DateTime.UtcNow)
                {
                    await _jobQueue.EnqueueAsync(job);
                }

                return job;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating job {name}");
                throw;
            }
        }

        public async Task<Job> GetJobByIdAsync(int id)
        {
            return await _unitOfWork.Repository<Job>().GetByIdAsync(id);
        }

        public async Task<IReadOnlyList<Job>> GetAllJobsAsync()
        {
            return await _unitOfWork.Repository<Job>().GetAllAsync();
        }

        public async Task<IReadOnlyList<Job>> GetJobsByStatusAsync(JobStatus status)
        {
            return await _unitOfWork.Repository<Job>().GetAsync(j => j.Status == status);
        }

        public async Task<IReadOnlyList<Job>> GetJobsByPriorityAsync(JobPriority priority)
        {
            return await _unitOfWork.Repository<Job>().GetAsync(j => j.Priority == priority);
        }

        public async Task<bool> UpdateJobStatusAsync(int jobId, JobStatus newStatus)
        {
            var job = await _unitOfWork.Repository<Job>().GetByIdAsync(jobId);
            if (job == null)
            {
                _logger.LogWarning($"Attempted to update status for non-existent job ID {jobId}");
                return false;
            }

            job.Status = newStatus;

            // Set start/end times based on status
            if (newStatus == JobStatus.Running && job.StartTime == null)
            {
                job.StartTime = DateTime.UtcNow;
            }
            else if ((newStatus == JobStatus.Completed || newStatus == JobStatus.Failed || newStatus == JobStatus.Stopped) && job.EndTime == null)
            {
                job.EndTime = DateTime.UtcNow;
            }

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation($"Updated job {jobId} status to {newStatus}");
            return true;
        }

        public async Task<bool> UpdateJobProgressAsync(int jobId, int progress)
        {
            var job = await _unitOfWork.Repository<Job>().GetByIdAsync(jobId);
            if (job == null)
            {
                _logger.LogWarning($"Attempted to update progress for non-existent job ID {jobId}");
                return false;
            }

            job.UpdateProgress(progress);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogDebug($"Updated job {jobId} progress to {progress}%");
            return true;
        }

        public async Task<bool> DeleteJobAsync(int jobId)
        {
            var job = await _unitOfWork.Repository<Job>().GetByIdAsync(jobId);
            if (job == null)
            {
                _logger.LogWarning($"Attempted to delete non-existent job ID {jobId}");
                return false;
            }

            // Only allow deletion of completed, failed, or stopped jobs
            if (job.Status != JobStatus.Completed && job.Status != JobStatus.Failed && job.Status != JobStatus.Stopped)
            {
                _logger.LogWarning($"Cannot delete job {jobId} with status {job.Status}");
                return false;
            }

            await _unitOfWork.Repository<Job>().DeleteAsync(job);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation($"Deleted job {jobId}");
            return true;
        }

        public async Task<bool> StopJobAsync(int jobId)
        {
            var job = await _unitOfWork.Repository<Job>().GetByIdAsync(jobId);
            if (job == null || job.Status != JobStatus.Running)
            {
                return false;
            }

            job.Status = JobStatus.Stopped;
            job.EndTime = DateTime.UtcNow;

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation($"Stopped job {jobId}");
            return true;
        }

        public async Task<bool> RestartJobAsync(int jobId)
        {
            var job = await _unitOfWork.Repository<Job>().GetByIdAsync(jobId);
            if (job == null || (job.Status != JobStatus.Failed && job.Status != JobStatus.Stopped))
            {
                return false;
            }

            job.Status = JobStatus.Pending;
            job.StartTime = null;
            job.EndTime = null;
            job.Progress = 0;

            await _unitOfWork.SaveChangesAsync();
            await _jobQueue.EnqueueAsync(job);

            _logger.LogInformation($"Restarted job {jobId}");
            return true;
        }

        public async Task AddJobLogAsync(int jobId, string logType, string message, string details = null)
        {
            var job = await _unitOfWork.Repository<Job>().GetByIdAsync(jobId);
            if (job == null)
            {
                _logger.LogWarning($"Attempted to add log to non-existent job ID {jobId}");
                return;
            }

            var log = new JobLog
            {
                JobId = jobId,
                LogType = logType,
                Message = message,
                Details = details,
                Timestamp = DateTime.UtcNow
            };

            await _unitOfWork.Repository<JobLog>().AddAsync(log);
            await _unitOfWork.SaveChangesAsync();
        }
    }
}