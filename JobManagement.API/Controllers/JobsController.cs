using JobManagement.Application.dtos;
using JobManagement.Application.Models.Requests;
using JobManagement.Application.Services.JobServices;
using JobManagement.Application.Services.WorkerNodes;
using JobManagement.Domain.Constants;
using JobManagement.Domain.Entities;
using JobManagement.Domain.Enums;
using JobManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace JobManagement.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class JobsController : ControllerBase
    {
        private readonly IJobService _jobService;
        private readonly ILogger<JobsController> _logger;
        private readonly IWorkerNodeService _workerNodeService;

        public JobsController(
            IJobService jobService,
            ILogger<JobsController> logger,
            IWorkerNodeService workerNodeService)
        {
            _jobService = jobService;
            _logger = logger;
            _workerNodeService = workerNodeService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<JobDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetJobs([FromQuery] JobFilterRequest jobFilterRequest)
        {
            try
            {
                var jobs = await _jobService.GetJobsAsync(jobFilterRequest);

                var jobDtos = jobs.Select(job => JobDto.FromEntity(job)).ToList();

                return Ok(jobDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving jobs with filters");
                return StatusCode(500, "An error occurred while retrieving jobs.");
            }
        }

        

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(JobDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetJobById(Guid id)
        {
            try
            {
                var job = await _jobService.GetJobByIdAsync(id);
                if (job == null)
                {
                    return NotFound($"Job with ID {id} not found.");
                }

                // Convert to DTO
                var jobDto = JobDto.FromEntity(job);
                return Ok(jobDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving job with ID {id}");
                return StatusCode(500, "An error occurred while retrieving the job.");
            }
        }

        [HttpPost]
        [ProducesResponseType(typeof(Job), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateJob([FromBody] CreateJobRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return BadRequest("Job name is required.");
                }

                // Force a worker load recalculation to ensure accurate availability
                await _workerNodeService.RecalculateWorkerLoadsAsync();

                // Check if we have any registered worker nodes at all (regardless of availability)
                var allWorkers = await _workerNodeService.GetAllWorkerNodesAsync();
                if (!allWorkers.Any())
                {
                    _logger.LogWarning("Job creation failed: No worker nodes exist in the system");
                    return BadRequest("Cannot create a job: No worker nodes exist in the system. Please create at least one worker node first.");
                }

                // Validate preferred worker exists if specified
                if (request.PreferredWorkerId.HasValue)
                {
                    var worker = await _workerNodeService.GetWorkerNodeByIdAsync(request.PreferredWorkerId.Value);
                    if (worker == null)
                    {
                        return BadRequest($"The specified preferred worker node (ID: {request.PreferredWorkerId}) does not exist.");
                    }
                }

                // Create the job regardless of worker availability
                var job = await _jobService.CreateJobAsync(
                    request.Name,
                    request.Description,
                    request.Priority,
                    request.ScheduledStartTime,
                    request.PreferredWorkerId,
                    request.Type);

                // Check if there are available workers
                var availableWorkers = await _workerNodeService.GetAvailableWorkerNodesAsync();
                if (!availableWorkers.Any())
                {
                    _logger.LogInformation($"No available workers for job {job.Id}. Job will be queued until a worker becomes available.");

                    // Return success but with a note about queuing
                    return CreatedAtAction(
                        nameof(GetJobById),
                        new { id = job.Id },
                        new
                        {
                            job,
                            message = "Job created successfully and added to the queue. It will start when a worker becomes available."
                        });
                }

                return CreatedAtAction(nameof(GetJobById), new { id = job.Id }, job);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating job");
                return StatusCode(500, "An error occurred while creating the job: " + ex.Message);
            }
        }

        // Post method for filtering (alternative to using query parameters)
        [HttpPost("filter")]
        [ProducesResponseType(typeof(IEnumerable<JobDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> FilterJobs([FromBody] JobFilterRequest request)
        {
            try
            {
                var jobs = await _jobService.GetJobsAsync(request);

                var jobDtos = jobs.Select(job => JobDto.FromEntity(job)).ToList();

                return Ok(jobDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filtering jobs");
                return StatusCode(500, "An error occurred while filtering jobs.");
            }
        }

        // Retry endpoint
        [HttpPost("{id}/retry")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RetryJob(Guid id)
        {
            try
            {
                var result = await _jobService.RetryJobAsync(id);
                if (!result)
                {
                    // The job might exist but not be in a state where it can be retried
                    var job = await _jobService.GetJobByIdAsync(id);
                    if (job == null)
                    {
                        return NotFound($"Job with ID {id} not found.");
                    }
                    else
                    {
                        return BadRequest($"Job with ID {id} cannot be retried in its current state ({job.Status}).");
                    }
                }
                return Ok("Job scheduled for retry.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrying job ID {id}");
                return StatusCode(500, "An error occurred while retrying the job.");
            }
        }

        [HttpPut("{id}/status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateJobStatus(Guid id, [FromBody] JobStatus status)
        {
            try
            {
                var result = await _jobService.UpdateJobStatusAsync(id, status);
                if (!result)
                {
                    return NotFound($"Job with ID {id} not found.");
                }
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating job status for job ID {id}");
                return StatusCode(500, "An error occurred while updating the job status.");
            }
        }

        [HttpPut("{id}/progress")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateJobProgress(Guid id, [FromBody] int progress)
        {
            try
            {
                if (progress < 0 || progress > 100)
                {
                    return BadRequest("Progress must be between 0 and 100.");
                }

                var result = await _jobService.UpdateJobProgressAsync(id, progress);
                if (!result)
                {
                    return NotFound($"Job with ID {id} not found.");
                }
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating progress for job ID {id}");
                return StatusCode(500, "An error occurred while updating the job progress.");
            }
        }

        [HttpPost("{id}/stop")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> StopJob(Guid id)
        {
            try
            {
                var result = await _jobService.StopJobAsync(id);
                if (!result)
                {
                    return NotFound($"Job with ID {id} not found or is not in a running state.");
                }
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error stopping job ID {id}");
                return StatusCode(500, "An error occurred while stopping the job.");
            }
        }

        [HttpPost("{id}/restart")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RestartJob(Guid id)
        {
            try
            {
                var result = await _jobService.RestartJobAsync(id);
                if (!result)
                {
                    return NotFound($"Job with ID {id} not found or is not in a failed or stopped state.");
                }
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error restarting job ID {id}");
                return StatusCode(500, "An error occurred while restarting the job.");
            }
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DeleteJob(Guid id)
        {
            try
            {
                var result = await _jobService.DeleteJobAsync(id);
                if (!result)
                {
                    // The job might exist but not be in a state where it can be deleted
                    var job = await _jobService.GetJobByIdAsync(id);
                    if (job == null)
                    {
                        return NotFound($"Job with ID {id} not found.");
                    }
                    else
                    {
                        return BadRequest($"Job with ID {id} cannot be deleted in its current state ({job.Status}).");
                    }
                }
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting job ID {id}");
                return StatusCode(500, "An error occurred while deleting the job.");
            }
        }
    }
}