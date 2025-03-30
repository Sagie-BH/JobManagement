using JobManagement.Domain.Entities;
using JobManagement.Domain.Enums;
using JobManagement.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using static JobManagement.Domain.Constants.WorkerConstants;

namespace JobManagement.Application.Services
{
    public class JobExecutionService : IJobExecutionService
    {
        private readonly JobService _jobService;
        private readonly WorkerNodeService _workerNodeService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<JobExecutionService> _logger;

        public JobExecutionService(
            JobService jobService,
            WorkerNodeService workerNodeService,
            IUnitOfWork unitOfWork,
            ILogger<JobExecutionService> logger)
        {
            _jobService = jobService;
            _workerNodeService = workerNodeService;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<bool> ExecuteJobAsync(Job job, CancellationToken cancellationToken = default)
        {
            if (job == null)
            {
                _logger.LogError("Attempted to execute null job");
                return false;
            }

            try
            {
                // Update job status to Running
                await _jobService.UpdateJobStatusAsync(job.Id, JobStatus.Running);
                _logger.LogInformation($"Started execution of job {job.Id}: {job.Name}");

                // Simulating job execution with progress reports
                var random = new Random();
                int progressStep = random.Next(5, 15); // Random progress steps between 5-15%
                int currentProgress = 0;

                while (currentProgress < 100 && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(random.Next(500, 2000), cancellationToken); // Simulate work

                    // Update progress
                    currentProgress = Math.Min(100, currentProgress + progressStep);
                    await _jobService.UpdateJobProgressAsync(job.Id, currentProgress);

                    // 10% chance of simulated failure (for testing)
                    if (random.Next(100) < 10 && currentProgress < 90)
                    {
                        throw new Exception("Simulated job execution failure");
                    }
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning($"Job {job.Id} execution was cancelled");
                    await _jobService.UpdateJobStatusAsync(job.Id, JobStatus.Stopped);
                    await _jobService.AddJobLogAsync(job.Id, LogType.Warning, "Job execution was cancelled");
                    return false;
                }

                // Job completed successfully
                await _jobService.UpdateJobStatusAsync(job.Id, JobStatus.Completed);
                await _jobService.AddJobLogAsync(job.Id, LogType.Info, "Job completed successfully");
                _logger.LogInformation($"Job {job.Id}: {job.Name} completed successfully");

                return true;
            }
            catch (Exception ex) when (!(ex is TaskCanceledException || ex is OperationCanceledException))
            {
                _logger.LogError(ex, $"Error executing job {job.Id}: {job.Name}");
                await _jobService.UpdateJobStatusAsync(job.Id, JobStatus.Failed);
                await _jobService.AddJobLogAsync(job.Id, LogType.Error, $"Job execution failed: {ex.Message}", ex.StackTrace);

                // Check if we should retry
                if (job.CanRetry())
                {
                    _logger.LogInformation($"Job {job.Id} will be retried. Retry count: {job.CurrentRetryCount}/{job.MaxRetryAttempts}");
                    job.IncrementRetryCount();
                    job.Status = JobStatus.Pending;
                    job.Progress = 0;
                    job.StartTime = null;
                    job.EndTime = null;

                    // Todo: Implement exponential backoff for retries

                    // Re-queue the job
                    // Note: This would be handled by the queue processing service in a real implementation
                }

                return false;
            }
            catch (Exception ex) // Handle cancellation
            {
                _logger.LogInformation($"Job {job.Id} execution was cancelled");
                await _jobService.UpdateJobStatusAsync(job.Id, JobStatus.Stopped);
                return false;
            }
            finally
            {
                // Ensure the worker's load is decreased regardless of outcome
                if (!string.IsNullOrEmpty(job.WorkerNodeId) && int.TryParse(job.WorkerNodeId, out int workerId))
                {
                    var worker = await _workerNodeService.GetWorkerNodeByIdAsync(workerId);
                    if (worker != null)
                    {
                        worker.DecreaseLoad();
                        await _workerNodeService.UpdateWorkerLoadAsync(workerId, worker.CurrentLoad);
                    }
                }
            }
        }
    }
}