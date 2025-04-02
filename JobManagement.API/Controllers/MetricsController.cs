using JobManagement.Application.Services.Metrics;
using JobManagement.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace JobManagement.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MetricsController : ControllerBase
    {
        private readonly IMetricsService _metricsService;
        private readonly ILogger<MetricsController> _logger;

        public MetricsController(
            IMetricsService metricsService,
            ILogger<MetricsController> logger)
        {
            _metricsService = metricsService;
            _logger = logger;
        }

        [HttpGet]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCurrentMetrics()
        {
            try
            {
                var jobMetrics = await _metricsService.GetLatestJobMetricsAsync();
                var workerMetrics = await _metricsService.GetLatestWorkerMetricsAsync();
                var queueMetrics = await _metricsService.GetLatestQueueMetricsAsync();

                var metrics = new
                {
                    Timestamp = DateTime.UtcNow,
                    Jobs = new
                    {
                        Total = jobMetrics.TotalJobs,
                        ByStatus = new
                        {
                            Pending = jobMetrics.PendingJobs,
                            Running = jobMetrics.RunningJobs,
                            Completed = jobMetrics.CompletedJobs,
                            Failed = jobMetrics.FailedJobs,
                            Stopped = jobMetrics.StoppedJobs
                        },
                        AverageExecutionTimeMs = jobMetrics.AverageExecutionTimeMs,
                        SuccessRate = jobMetrics.SuccessRate,
                        TotalRetries = jobMetrics.TotalRetries
                    },
                    Workers = new
                    {
                        Total = workerMetrics.TotalWorkers,
                        ByStatus = new
                        {
                            Active = workerMetrics.ActiveWorkers,
                            Idle = workerMetrics.IdleWorkers,
                            Offline = workerMetrics.OfflineWorkers
                        },
                        Utilization = new
                        {
                            PercentUtilized = workerMetrics.AverageWorkerUtilization,
                            TotalCapacity = workerMetrics.TotalCapacity,
                            CurrentLoad = workerMetrics.CurrentLoad
                        }
                    },
                    Queue = new
                    {
                        TotalLength = queueMetrics.TotalQueueLength,
                        ByPriority = new
                        {
                            High = queueMetrics.HighPriorityJobs,
                            Regular = queueMetrics.RegularPriorityJobs,
                            Low = queueMetrics.LowPriorityJobs
                        },
                        AverageWaitTimeMs = queueMetrics.AverageWaitTimeMs,
                        JobsProcessedLastMinute = queueMetrics.JobsProcessedLastMinute
                    }
                };

                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving current metrics");
                return StatusCode(500, "An error occurred while retrieving metrics.");
            }
        }

        [HttpGet("jobs")]
        [ProducesResponseType(typeof(JobMetric), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetJobMetrics()
        {
            try
            {
                var jobMetrics = await _metricsService.GetLatestJobMetricsAsync();
                return Ok(jobMetrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving job metrics");
                return StatusCode(500, "An error occurred while retrieving job metrics.");
            }
        }

        [HttpGet("workers")]
        [ProducesResponseType(typeof(WorkerMetric), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetWorkerMetrics()
        {
            try
            {
                var workerMetrics = await _metricsService.GetLatestWorkerMetricsAsync();
                return Ok(workerMetrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving worker metrics");
                return StatusCode(500, "An error occurred while retrieving worker metrics.");
            }
        }

        [HttpGet("queue")]
        [ProducesResponseType(typeof(QueueMetric), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetQueueMetrics()
        {
            try
            {
                var queueMetrics = await _metricsService.GetLatestQueueMetricsAsync();
                return Ok(queueMetrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving queue metrics");
                return StatusCode(500, "An error occurred while retrieving queue metrics.");
            }
        }

        [HttpGet("snapshots/{type}")]
        [ProducesResponseType(typeof(IEnumerable<MetricSnapshot>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMetricSnapshots(
            string type,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            try
            {
                // Default to last 24 hours if no range specified
                from ??= DateTime.UtcNow.AddDays(-1);
                to ??= DateTime.UtcNow;

                var snapshots = await _metricsService.GetMetricSnapshotsAsync(
                    type,
                    from.Value,
                    to.Value);

                return Ok(snapshots);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving {type} metric snapshots");
                return StatusCode(500, "An error occurred while retrieving metric snapshots.");
            }
        }

        [HttpGet("export")]
        [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
        public async Task<IActionResult> ExportMetrics([FromQuery] string format = "json")
        {
            try
            {
                var jobMetrics = await _metricsService.GetLatestJobMetricsAsync();
                var workerMetrics = await _metricsService.GetLatestWorkerMetricsAsync();
                var queueMetrics = await _metricsService.GetLatestQueueMetricsAsync();

                var metrics = new
                {
                    Generated = DateTime.UtcNow,
                    Jobs = jobMetrics,
                    Workers = workerMetrics,
                    Queue = queueMetrics
                };

                if (format.ToLower() == "csv")
                {
                    // Create CSV content
                    var csvBuilder = new StringBuilder();

                    // Header
                    csvBuilder.AppendLine("Category,Metric,Value");

                    // Job metrics
                    csvBuilder.AppendLine($"Jobs,Total,{jobMetrics.TotalJobs}");
                    csvBuilder.AppendLine($"Jobs,Pending,{jobMetrics.PendingJobs}");
                    csvBuilder.AppendLine($"Jobs,Running,{jobMetrics.RunningJobs}");
                    csvBuilder.AppendLine($"Jobs,Completed,{jobMetrics.CompletedJobs}");
                    csvBuilder.AppendLine($"Jobs,Failed,{jobMetrics.FailedJobs}");
                    csvBuilder.AppendLine($"Jobs,Stopped,{jobMetrics.StoppedJobs}");
                    csvBuilder.AppendLine($"Jobs,AverageExecutionTimeMs,{jobMetrics.AverageExecutionTimeMs}");
                    csvBuilder.AppendLine($"Jobs,SuccessRate,{jobMetrics.SuccessRate}");
                    csvBuilder.AppendLine($"Jobs,TotalRetries,{jobMetrics.TotalRetries}");

                    // Worker metrics
                    csvBuilder.AppendLine($"Workers,Total,{workerMetrics.TotalWorkers}");
                    csvBuilder.AppendLine($"Workers,Active,{workerMetrics.ActiveWorkers}");
                    csvBuilder.AppendLine($"Workers,Idle,{workerMetrics.IdleWorkers}");
                    csvBuilder.AppendLine($"Workers,Offline,{workerMetrics.OfflineWorkers}");
                    csvBuilder.AppendLine($"Workers,UtilizationPercentage,{workerMetrics.AverageWorkerUtilization}");
                    csvBuilder.AppendLine($"Workers,TotalCapacity,{workerMetrics.TotalCapacity}");
                    csvBuilder.AppendLine($"Workers,CurrentLoad,{workerMetrics.CurrentLoad}");

                    // Queue metrics
                    csvBuilder.AppendLine($"Queue,TotalLength,{queueMetrics.TotalQueueLength}");
                    csvBuilder.AppendLine($"Queue,HighPriorityJobs,{queueMetrics.HighPriorityJobs}");
                    csvBuilder.AppendLine($"Queue,RegularPriorityJobs,{queueMetrics.RegularPriorityJobs}");
                    csvBuilder.AppendLine($"Queue,LowPriorityJobs,{queueMetrics.LowPriorityJobs}");
                    csvBuilder.AppendLine($"Queue,AverageWaitTimeMs,{queueMetrics.AverageWaitTimeMs}");
                    csvBuilder.AppendLine($"Queue,JobsProcessedLastMinute,{queueMetrics.JobsProcessedLastMinute}");

                    var csvBytes = Encoding.UTF8.GetBytes(csvBuilder.ToString());
                    var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                    return File(csvBytes, "text/csv", $"metrics_export_{timestamp}.csv");
                }
                else // Default to JSON
                {
                    var jsonContent = System.Text.Json.JsonSerializer.Serialize(metrics, new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                    var jsonBytes = Encoding.UTF8.GetBytes(jsonContent);
                    var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                    return File(jsonBytes, "application/json", $"metrics_export_{timestamp}.json");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting metrics");
                return StatusCode(500, "An error occurred while exporting metrics.");
            }
        }

        [HttpPost("trigger-snapshot/{type}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> TriggerMetricSnapshot(string type)
        {
            try
            {
                if (string.IsNullOrEmpty(type))
                {
                    return BadRequest("Snapshot type is required");
                }

                var validTypes = new[] { "Hourly", "Daily", "Weekly", "Custom" };
                if (!validTypes.Contains(type, StringComparer.OrdinalIgnoreCase))
                {
                    return BadRequest($"Invalid snapshot type. Valid types are: {string.Join(", ", validTypes)}");
                }

                await _metricsService.CreateMetricSnapshotAsync(type);
                return Ok($"{type} snapshot created successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating {type} snapshot");
                return StatusCode(500, $"An error occurred while creating the {type} snapshot.");
            }
        }
    }
}