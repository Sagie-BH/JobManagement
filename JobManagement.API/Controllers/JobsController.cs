using JobManagement.Application.Services;
using JobManagement.Domain.Entities;
using JobManagement.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace JobManagement.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class JobsController : ControllerBase
    {
        private readonly JobService _jobService;
        private readonly ILogger<JobsController> _logger;

        public JobsController(JobService jobService, ILogger<JobsController> logger)
        {
            _jobService = jobService;
            _logger = logger;
        }

        // Request models
        public class CreateJobRequest
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public JobPriority Priority { get; set; } = JobPriority.Regular;
            public DateTime? ScheduledStartTime { get; set; }
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<Job>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllJobs([FromQuery] JobStatus? status = null)
        {
            try
            {
                if (status.HasValue)
                {
                    var jobs = await _jobService.GetJobsByStatusAsync(status.Value);
                    return Ok(jobs);
                }
                else
                {
                    var jobs = await _jobService.GetAllJobsAsync();
                    return Ok(jobs);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving jobs");
                return StatusCode(500, "An error occurred while retrieving jobs.");
            }
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(Job), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetJobById(int id)
        {
            try
            {
                var job = await _jobService.GetJobByIdAsync(id);
                if (job == null)
                {
                    return NotFound($"Job with ID {id} not found.");
                }
                return Ok(job);
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

                var job = await _jobService.CreateJobAsync(
                    request.Name,
                    request.Description,
                    request.Priority,
                    request.ScheduledStartTime);

                return CreatedAtAction(nameof(GetJobById), new { id = job.Id }, job);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating job");
                return StatusCode(500, "An error occurred while creating the job.");
            }
        }

        [HttpPut("{id}/status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateJobStatus(int id, [FromBody] JobStatus status)
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
        public async Task<IActionResult> UpdateJobProgress(int id, [FromBody] int progress)
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
        public async Task<IActionResult> StopJob(int id)
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
        public async Task<IActionResult> RestartJob(int id)
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
        public async Task<IActionResult> DeleteJob(int id)
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