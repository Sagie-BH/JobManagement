using JobManagement.Domain.Entities;
using JobManagement.Domain.Enums;
using JobManagement.Domain.Interfaces;
using JobManagement.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace JobManagement.Infrastructure.Queue
{
    public class PriorityJobQueue : IJobQueue
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<PriorityJobQueue> _logger;
        private readonly ConcurrentDictionary<JobPriority, ConcurrentQueue<Guid>> _jobQueues;

        public PriorityJobQueue(IUnitOfWork unitOfWork, ILogger<PriorityJobQueue> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _jobQueues = new ConcurrentDictionary<JobPriority, ConcurrentQueue<Guid>>();

            // Initialize queues for each priority level
            foreach (JobPriority priority in Enum.GetValues(typeof(JobPriority)))
            {
                _jobQueues[priority] = new ConcurrentQueue<Guid>();
            }
        }

        public async Task EnqueueAsync(Job job)
        {
            // Ensure job is saved to database first
            if (job.Id == Guid.Empty || job.Id == null)
            {
                await _unitOfWork.Repository<Job>().AddAsync(job);
                await _unitOfWork.SaveChangesAsync();
            }

            // Add job ID to the appropriate priority queue
            _jobQueues[job.Priority].Enqueue(job.Id);
            _logger.LogInformation($"Job {job.Id} ({job.Name}) added to {job.Priority} queue");
        }

        public async Task<Job> DequeueAsync()
        {
            // Try to dequeue from highest priority to lowest
            foreach (JobPriority priority in Enum.GetValues(typeof(JobPriority)).Cast<JobPriority>().OrderBy(p => p))
            {
                if (_jobQueues[priority].TryDequeue(out Guid jobId))
                {
                    var job = await _unitOfWork.Repository<Job>().GetByIdAsync(jobId);
                    if (job != null)
                    {
                        _logger.LogInformation($"Job {job.Id} ({job.Name}) dequeued from {priority} queue");
                        return job;
                    }
                }
            }

            return null; // No job available
        }

        public async Task<IReadOnlyList<Job>> GetPendingJobsAsync()
        {
            var jobIds = _jobQueues.SelectMany(q => q.Value).ToList();
            var result = new List<Job>();

            foreach (var id in jobIds)
            {
                var job = await _unitOfWork.Repository<Job>().GetByIdAsync(id);
                if (job != null)
                {
                    result.Add(job);
                }
            }

            return result;
        }

        public Task<int> GetQueueLengthAsync()
        {
            int totalLength = _jobQueues.Sum(q => q.Value.Count);
            return Task.FromResult(totalLength);
        }

        public async Task<IReadOnlyList<Job>> GetJobsByPriorityAsync(JobPriority priority)
        {
            var jobIds = _jobQueues[priority].ToList();
            var result = new List<Job>();

            foreach (var id in jobIds)
            {
                var job = await _unitOfWork.Repository<Job>().GetByIdAsync(id);
                if (job != null)
                {
                    result.Add(job);
                }
            }

            return result;
        }

        public async Task SaveQueueStateAsync()
        {
            // Implementation for persisting queue state to database
            _logger.LogInformation("Saving queue state");

            // Here you would implement the logic to save the current queue state
            // This could involve storing the queue contents in a dedicated table
        }

        public async Task RestoreQueueStateAsync()
        {
            _logger.LogInformation("Restoring queue state");

            // Clear existing queues
            foreach (var queue in _jobQueues.Values)
            {
                while (queue.TryDequeue(out _)) { }
            }

            // Get all pending jobs from the database
            var jobRepository = _unitOfWork.Repository<Job>();
            var pendingJobs = await jobRepository.GetAsync(j => j.Status == JobStatus.Pending);

            // Add them back to the appropriate queues
            foreach (var job in pendingJobs)
            {
                _jobQueues[job.Priority].Enqueue(job.Id);
            }

            _logger.LogInformation($"Restored {pendingJobs.Count} jobs to queue");
        }
    }
}