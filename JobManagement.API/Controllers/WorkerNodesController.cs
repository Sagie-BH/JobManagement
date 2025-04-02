using JobManagement.Application.dtos;
using JobManagement.Application.Services.WorkerNodes;
using JobManagement.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JobManagement.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WorkerNodesController : ControllerBase
    {
        private readonly IWorkerNodeService _workerNodeService;
        private readonly ILogger<WorkerNodesController> _logger;

        public WorkerNodesController(IWorkerNodeService workerNodeService, ILogger<WorkerNodesController> logger)
        {
            _workerNodeService = workerNodeService;
            _logger = logger;
        }

        // Request models - Updated to include Power property
        public class RegisterWorkerRequest
        {
            public string Name { get; set; }
            public string Endpoint { get; set; }
            public int Capacity { get; set; } = 5;
            public int Power { get; set; } = 5; // Default to medium power (1-10 scale)
        }

        public class UpdateWorkerRequest
        {
            public string Endpoint { get; set; }
            public int Capacity { get; set; }
            public int Power { get; set; }
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<WorkerNodeDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllWorkerNodes([FromQuery] bool onlyAvailable = false)
        {
            try
            {
                IReadOnlyList<WorkerNode> workers;
                if (onlyAvailable)
                {
                    workers = await _workerNodeService.GetAvailableWorkerNodesAsync();
                }
                else
                {
                    workers = await _workerNodeService.GetAllWorkerNodesAsync();
                }

                var workerDtos = workers.Select(WorkerNodeDto.FromEntity).ToList();
                return Ok(workerDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving worker nodes");
                return StatusCode(500, "An error occurred while retrieving worker nodes.");
            }
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(WorkerNodeDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetWorkerNodeById(Guid id)
        {
            try
            {
                var worker = await _workerNodeService.GetWorkerNodeByIdAsync(id);
                if (worker == null)
                {
                    return NotFound($"Worker node with ID {id} not found.");
                }

                // Convert to DTO to avoid circular references
                var workerDto = WorkerNodeDto.FromEntity(worker);
                return Ok(workerDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving worker node with ID {id}");
                return StatusCode(500, "An error occurred while retrieving the worker node.");
            }
        }

        [HttpPost]
        [ProducesResponseType(typeof(WorkerNode), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RegisterWorkerNode([FromBody] RegisterWorkerRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return BadRequest("Worker node name is required.");
                }

                if (string.IsNullOrWhiteSpace(request.Endpoint))
                {
                    return BadRequest("Worker node endpoint is required.");
                }

                // Validate power range
                if (request.Power < 1 || request.Power > 10)
                {
                    return BadRequest("Worker node power must be between 1 and 10.");
                }

                var worker = await _workerNodeService.RegisterWorkerNodeAsync(
                    request.Name,
                    request.Endpoint,
                    request.Capacity,
                    request.Power);

                return CreatedAtAction(nameof(GetWorkerNodeById), new { id = worker.Id }, worker);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering worker node");
                return StatusCode(500, "An error occurred while registering the worker node.");
            }
        }

        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateWorkerNode(Guid id, [FromBody] UpdateWorkerRequest request)
        {
            try
            {
                // Validate power range
                if (request.Power < 1 || request.Power > 10)
                {
                    return BadRequest("Worker node power must be between 1 and 10.");
                }

                var worker = await _workerNodeService.UpdateWorkerNodeAsync(id, request.Endpoint, request.Capacity, request.Power);
                if (worker == null)
                {
                    return NotFound($"Worker node with ID {id} not found.");
                }
                return Ok(worker);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating worker node ID {id}");
                return StatusCode(500, "An error occurred while updating the worker node.");
            }
        }

        [HttpPost("{id}/heartbeat")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateHeartbeat(Guid id)
        {
            try
            {
                var result = await _workerNodeService.UpdateWorkerHeartbeatAsync(id);
                if (!result)
                {
                    return NotFound($"Worker node with ID {id} not found.");
                }
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating heartbeat for worker node ID {id}");
                return StatusCode(500, "An error occurred while updating the worker heartbeat.");
            }
        }

        [HttpPost("{id}/deactivate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeactivateWorkerNode(Guid id)
        {
            try
            {
                var result = await _workerNodeService.DeactivateWorkerNodeAsync(id);
                if (!result)
                {
                    return NotFound($"Worker node with ID {id} not found.");
                }
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deactivating worker node ID {id}");
                return StatusCode(500, "An error occurred while deactivating the worker node.");
            }
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DeleteWorkerNode(Guid id)
        {
            try
            {
                var result = await _workerNodeService.DeleteWorkerNodeAsync(id);
                if (!result)
                {
                    // Check if worker exists first
                    var worker = await _workerNodeService.GetWorkerNodeByIdAsync(id);
                    if (worker == null)
                    {
                        return NotFound($"Worker node with ID {id} not found.");
                    }
                    else
                    {
                        return BadRequest($"Worker node with ID {id} cannot be deleted as it has running jobs assigned.");
                    }
                }
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting worker node ID {id}");
                return StatusCode(500, "An error occurred while deleting the worker node.");
            }
        }

        [HttpPost("{workerId}/assign/{jobId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> AssignJobToWorker(Guid workerId, Guid jobId)
        {
            try
            {
                var result = await _workerNodeService.AssignJobToWorkerAsync(jobId, workerId);
                if (!result)
                {
                    // Could be either worker or job not found, or worker not available
                    return BadRequest($"Could not assign job {jobId} to worker {workerId}. Either the entities don't exist or the worker is not available.");
                }
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error assigning job {jobId} to worker {workerId}");
                return StatusCode(500, "An error occurred while assigning the job to the worker.");
            }
        }

        [HttpPost("{workerId}/load")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateWorkerLoad(Guid workerId, [FromBody] int currentLoad)
        {
            try
            {
                if (currentLoad < 0)
                {
                    return BadRequest("Current load cannot be negative.");
                }

                var result = await _workerNodeService.UpdateWorkerLoadAsync(workerId, currentLoad);
                if (!result)
                {
                    return NotFound($"Worker node with ID {workerId} not found.");
                }
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating load for worker node ID {workerId}");
                return StatusCode(500, "An error occurred while updating the worker load.");
            }
        }
    }
}