using JobManagement.Application.Services.JobServices;
using JobManagement.Application.Services.WorkerNodes;
using JobManagement.Domain.Entities;
using JobManagement.Domain.Enums;
using JobManagement.Infrastructure.Interfaces;
using JobManagement.Infrastructure.Interfaces.SignalR;
using Microsoft.Extensions.Logging;

namespace JobManagement.Application.Services.JobExecutionService
{
    public class JobExecutionService : IJobExecutionService
    {
        private readonly IJobService _jobService;
        private readonly IWorkerNodeService _workerNodeService;
        private readonly IJobSignalRService _jobSignalRService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<JobExecutionService> _logger;
        private readonly IWorkerSignalRService _workerSignalRService;

        public JobExecutionService(
            IJobService jobService,
            IWorkerNodeService workerNodeService,
            IUnitOfWork unitOfWork,
            ILogger<JobExecutionService> logger,
            IJobSignalRService jobSignalRService,
            IWorkerSignalRService workerSignalRService)
        {
            _jobService = jobService;
            _workerNodeService = workerNodeService;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _jobSignalRService = jobSignalRService;
            _workerSignalRService = workerSignalRService;
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

                // Get the worker node to determine processing speed
                WorkerNode workerNode = null;
                if (job.WorkerNodeId.HasValue && job.WorkerNodeId != Guid.Empty)
                {
                    workerNode = await _workerNodeService.GetWorkerNodeByIdAsync(job.WorkerNodeId.Value);
                }

                // Default to medium power if worker not found
                int workerPower = workerNode?.Power ?? 5;

                // Calculate execution parameters based on worker power
                var random = new Random();
                int totalSteps = CalculateTotalSteps(workerPower);
                int progressPerStep = 100 / totalSteps;
                int currentProgress = 0;

                _logger.LogInformation($"Job {job.Id} will execute with {totalSteps} steps based on worker power {workerPower}");

                for (int step = 0; step < totalSteps && !cancellationToken.IsCancellationRequested; step++)
                {
                    // Calculate delay based on worker power and job priority
                    int delayMs = CalculateStepDelay(job.Priority, workerPower);

                    await Task.Delay(delayMs, cancellationToken);

                    // Update progress
                    currentProgress = Math.Min(100, (step + 1) * progressPerStep);
                    _logger.LogInformation($"Job {job.Id} progress: {currentProgress}% (worker power: {workerPower})");

                    await _jobService.UpdateJobProgressAsync(job.Id, currentProgress);

                    // Notify clients of progress update via SignalR
                    await _jobSignalRService.NotifyJobProgressUpdatedAsync(job.Id, currentProgress);

                    // Small chance of simulated failure (for testing)
                    // Higher chance for lower power workers
                    int failureChance = 5 - (workerPower / 2); // Power 1 = 4.5% chance, Power 10 = 0% chance
                    if (random.Next(100) < failureChance && currentProgress < 90)
                    {
                        throw new Exception($"Simulated job execution failure on worker with power {workerPower}");
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
                _logger.LogInformation($"Job {job.Id}: {job.Name} completed successfully on worker with power {workerPower}");

                // Explicitly notify about completed status
                await _jobSignalRService.NotifyJobStatusChangedAsync(job.Id, JobStatus.Completed.ToString());

                return true;
            }
            catch (Exception ex) when (!(ex is TaskCanceledException || ex is OperationCanceledException))
            {
                _logger.LogError(ex, $"Error executing job {job.Id}: {job.Name}");
                await _jobService.UpdateJobStatusAsync(job.Id, JobStatus.Failed);
                await _jobService.AddJobLogAsync(job.Id, LogType.Error, $"Job execution failed: {ex.Message}", ex.StackTrace ?? string.Empty);

                // Notify clients about job error
                await _jobSignalRService.NotifyJobErrorAsync(job.Id, ex.Message);

                // Retry logic if needed
                // ...

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
                if (job.WorkerNodeId.HasValue && job.WorkerNodeId != Guid.Empty)
                {
                    var worker = await _workerNodeService.GetWorkerNodeByIdAsync(job.WorkerNodeId.Value);
                    if (worker != null)
                    {
                        worker.DecreaseLoad();
                        await _workerNodeService.UpdateWorkerLoadAsync(job.WorkerNodeId.Value, worker.CurrentLoad);

                        // Notify about worker load changed via SignalR
                        await _workerSignalRService.NotifyWorkerLoadChangedAsync(
                            job.WorkerNodeId.Value,
                            worker.CurrentLoad,
                            worker.Capacity);
                    }
                }
            }
        }

        /// <summary>
        /// Calculate the number of progress updates based on worker power
        /// </summary>
        private int CalculateTotalSteps(int workerPower)
        {
            // Higher power workers will have fewer steps (because they complete faster)
            // Lower power workers will have more steps (because they take longer)
            // Range: Power 1 = 20 steps, Power 10 = 5 steps
            return Math.Max(5, 25 - (workerPower * 2));
        }

        /// <summary>
        /// Calculate delay between steps based on worker power and job priority
        /// </summary>
        private int CalculateStepDelay(JobPriority priority, int workerPower)
        {
            // Base delay - higher power means shorter delay
            // Power 1 = 5000ms base, Power 10 = 500ms base
            int baseDelay = 5500 - (workerPower * 500);

            // Apply randomness (±20%)
            var random = new Random();
            baseDelay = (int)(baseDelay * (0.8 + (random.NextDouble() * 0.4)));

            // Apply priority modifier
            double priorityModifier = GetPriorityModifier(priority);

            return (int)(baseDelay * priorityModifier);
        }

        /// <summary>
        /// Get a speed modifier based on job priority
        /// </summary>
        private double GetPriorityModifier(JobPriority priority)
        {
            return priority switch
            {
                JobPriority.Critical => 0.5,  // 2x faster
                JobPriority.Urgent => 0.6,    // 1.67x faster
                JobPriority.High => 0.8,      // 1.25x faster
                JobPriority.Regular => 1.0,   // normal speed
                JobPriority.Low => 1.5,       // 1.5x slower
                JobPriority.Deferred => 2.0,  // 2x slower
                _ => 1.0
            };
        }
    }
}