using JobManagement.Domain.Constants;
using JobManagement.Domain.Entities;
using JobManagement.Domain.Enums;
using JobManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace JobManagement.Infrastructure.Services
{
    /// <summary>
    /// Service that initializes the database with mock data for testing/development
    /// </summary>
    public class DbInitializerService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DbInitializerService> _logger;
        private readonly Random _random = new Random();

        public DbInitializerService(
            IServiceProvider serviceProvider,
            ILogger<DbInitializerService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // Create a scope to resolve scoped services
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            try
            {
                _logger.LogInformation("Initializing database with seed data");

                // Apply any pending migrations
                await dbContext.Database.MigrateAsync(cancellationToken);

                // Seed roles and permissions
                await SeedRolesAndPermissions(dbContext, cancellationToken);

                // Seed mock users
                await SeedMockUsers(dbContext, cancellationToken);

                //// Seed worker nodes
                //await SeedWorkerNodes(dbContext, cancellationToken);

                //// Seed jobs and job logs
                //await SeedJobsAndLogs(dbContext, cancellationToken);

                //// Seed metrics data
                //await SeedMetricsData(dbContext, cancellationToken);

                _logger.LogInformation("Database initialization completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while initializing the database");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private async Task SeedRolesAndPermissions(AppDbContext dbContext, CancellationToken cancellationToken)
        {
            // Check if roles already exist
            if (await dbContext.Roles.AnyAsync(cancellationToken))
            {
                _logger.LogInformation("Roles already exist, skipping role and permission seeding");
                return;
            }

            _logger.LogInformation("Creating roles and permissions");

            // Create default roles
            var adminRole = new Role { Name = AuthConstants.Roles.Admin, Description = "Administrator with full access" };
            var operatorRole = new Role { Name = AuthConstants.Roles.Operator, Description = "Operator with job and worker management permissions" };
            var userRole = new Role { Name = AuthConstants.Roles.User, Description = "Regular user with limited permissions" };

            dbContext.Roles.AddRange(adminRole, operatorRole, userRole);
            await dbContext.SaveChangesAsync(cancellationToken);

            // Create permissions
            var permissions = new List<Permission>
            {
                // Job permissions
                new Permission { Name = AuthConstants.Permissions.ViewJobs, Description = "Can view jobs" },
                new Permission { Name = AuthConstants.Permissions.CreateJobs, Description = "Can create jobs" },
                new Permission { Name = AuthConstants.Permissions.EditJobs, Description = "Can edit jobs" },
                new Permission { Name = AuthConstants.Permissions.DeleteJobs, Description = "Can delete jobs" },
                new Permission { Name = AuthConstants.Permissions.StopJobs, Description = "Can stop jobs" },
                new Permission { Name = AuthConstants.Permissions.RestartJobs, Description = "Can restart jobs" },

                // Worker permissions
                new Permission { Name = AuthConstants.Permissions.ViewWorkers, Description = "Can view workers" },
                new Permission { Name = AuthConstants.Permissions.ManageWorkers, Description = "Can manage workers" },
                new Permission { Name = AuthConstants.Permissions.DeleteWorkers, Description = "Can delete workers" },

                // Metrics permissions
                new Permission { Name = AuthConstants.Permissions.ViewMetrics, Description = "Can view metrics" },
                new Permission { Name = AuthConstants.Permissions.ExportMetrics, Description = "Can export metrics" },

                // User management permissions
                new Permission { Name = AuthConstants.Permissions.ViewUsers, Description = "Can view users" },
                new Permission { Name = AuthConstants.Permissions.CreateUsers, Description = "Can create users" },
                new Permission { Name = AuthConstants.Permissions.EditUsers, Description = "Can edit users" },
                new Permission { Name = AuthConstants.Permissions.DeleteUsers, Description = "Can delete users" },
                new Permission { Name = AuthConstants.Permissions.AssignRoles, Description = "Can assign roles to users" }
            };

            dbContext.Permissions.AddRange(permissions);
            await dbContext.SaveChangesAsync(cancellationToken);

            // Assign permissions to roles

            // Admin gets all permissions
            foreach (var permission in permissions)
            {
                dbContext.RolePermissions.Add(new RolePermission
                {
                    RoleId = adminRole.Id,
                    PermissionId = permission.Id
                });
            }

            // Operator gets job and worker management permissions
            var operatorPermissions = permissions.Where(p =>
                p.Name.StartsWith("Jobs.") ||
                p.Name.StartsWith("Workers.") ||
                p.Name == AuthConstants.Permissions.ViewMetrics).ToList();

            foreach (var permission in operatorPermissions)
            {
                dbContext.RolePermissions.Add(new RolePermission
                {
                    RoleId = operatorRole.Id,
                    PermissionId = permission.Id
                });
            }

            // Regular user gets limited permissions
            var userPermissions = permissions.Where(p =>
                p.Name == AuthConstants.Permissions.ViewJobs ||
                p.Name == AuthConstants.Permissions.CreateJobs ||
                p.Name == AuthConstants.Permissions.ViewWorkers).ToList();

            foreach (var permission in userPermissions)
            {
                dbContext.RolePermissions.Add(new RolePermission
                {
                    RoleId = userRole.Id,
                    PermissionId = permission.Id
                });
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Roles and permissions created successfully");
        }

        private async Task SeedMockUsers(AppDbContext dbContext, CancellationToken cancellationToken)
        {
            // Check if users already exist
            if (await dbContext.Users.AnyAsync(cancellationToken))
            {
                _logger.LogInformation("Mock users already exist, skipping user seeding");
                return;
            }

            _logger.LogInformation("Creating mock users");

            // Get roles
            var adminRole = await dbContext.Roles.FirstAsync(r => r.Name == AuthConstants.Roles.Admin, cancellationToken);
            var operatorRole = await dbContext.Roles.FirstAsync(r => r.Name == AuthConstants.Roles.Operator, cancellationToken);
            var userRole = await dbContext.Roles.FirstAsync(r => r.Name == AuthConstants.Roles.User, cancellationToken);

            // Create mock users
            var adminUser = new User
            {
                Username = "admin",
                Email = "admin@example.com",
                FirstName = "Admin",
                LastName = "User",
                PasswordHash = HashPassword("Admin123!"),
                Provider = AuthConstants.Providers.Local,
                ExternalId = "local_" + Guid.NewGuid().ToString(),
                IsActive = true,
                LastLogin = DateTime.UtcNow,
                CreatedOn = DateTime.UtcNow,
                CreatedBy = "System",
                LastModifiedOn = DateTime.UtcNow,
                LastModifiedBy = "System",
            };

            var operatorUser = new User
            {
                Username = "operator",
                Email = "operator@example.com",
                FirstName = "Operator",
                LastName = "User",
                PasswordHash = HashPassword("Operator123!"),
                Provider = AuthConstants.Providers.Local,
                ExternalId = "local_" + Guid.NewGuid().ToString(),
                IsActive = true,
                LastLogin = DateTime.UtcNow,
                CreatedOn = DateTime.UtcNow,
                CreatedBy = "System",
                LastModifiedOn = DateTime.UtcNow,
                LastModifiedBy = "System",
            };

            var regularUser = new User
            {
                Username = "user",
                Email = "user@example.com",
                FirstName = "Regular",
                LastName = "User",
                PasswordHash = HashPassword("User123!"),
                Provider = AuthConstants.Providers.Local,
                ExternalId = "local_" + Guid.NewGuid().ToString(),
                IsActive = true,
                LastLogin = DateTime.UtcNow,
                CreatedOn = DateTime.UtcNow,
                CreatedBy = "System",
                LastModifiedOn = DateTime.UtcNow,
                LastModifiedBy = "System",
            };

            var inactiveUser = new User
            {
                Username = "inactive",
                Email = "inactive@example.com",
                FirstName = "Inactive",
                LastName = "User",
                PasswordHash = HashPassword("Inactive123!"),
                Provider = AuthConstants.Providers.Local,
                ExternalId = "local_" + Guid.NewGuid().ToString(),
                IsActive = false,
                CreatedOn = DateTime.UtcNow,
                CreatedBy = "System",
                LastModifiedOn = DateTime.UtcNow,
                LastModifiedBy = "System",
            };

            // Add users to database
            dbContext.Users.AddRange(adminUser, operatorUser, regularUser, inactiveUser);
            await dbContext.SaveChangesAsync(cancellationToken);

            // Assign roles to users
            dbContext.UserRoles.Add(new UserRole { UserId = adminUser.Id, RoleId = adminRole.Id });
            dbContext.UserRoles.Add(new UserRole { UserId = operatorUser.Id, RoleId = operatorRole.Id });
            dbContext.UserRoles.Add(new UserRole { UserId = regularUser.Id, RoleId = userRole.Id });
            dbContext.UserRoles.Add(new UserRole { UserId = inactiveUser.Id, RoleId = userRole.Id });

            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Mock users created successfully");

            // Print out the credentials for easy access during development
            _logger.LogInformation("------------------------------------------------------");
            _logger.LogInformation("Created the following test accounts:");
            _logger.LogInformation("Admin: username='admin', password='Admin123!', email='admin@example.com'");
            _logger.LogInformation("Operator: username='operator', password='Operator123!', email='operator@example.com'");
            _logger.LogInformation("User: username='user', password='User123!', email='user@example.com'");
            _logger.LogInformation("Inactive: username='inactive', password='Inactive123!', email='inactive@example.com' (disabled)");
            _logger.LogInformation("------------------------------------------------------");
        }

        private async Task SeedWorkerNodes(AppDbContext dbContext, CancellationToken cancellationToken)
        {
            // Check if worker nodes already exist
            if (await dbContext.WorkerNodes.AnyAsync(cancellationToken))
            {
                _logger.LogInformation("Worker nodes already exist, skipping worker node seeding");
                return;
            }

            _logger.LogInformation("Creating mock worker nodes");

            // Create sample worker nodes with different statuses and capacities
            var workerNodes = new List<WorkerNode>
            {
                new WorkerNode
                {
                    Name = "Worker-1",
                    Endpoint = "http://worker1.example.com:5000",
                    Status = WorkerConstants.Status.Active,
                    LastHeartbeat = DateTime.UtcNow,
                    Capacity = 10,
                    CurrentLoad = 3,
                    CreatedOn = DateTime.UtcNow.AddDays(-30),
                    CreatedBy = "System",
                    LastModifiedOn = DateTime.UtcNow,
                    LastModifiedBy = "System",
                },
                new WorkerNode
                {
                    Name = "Worker-2",
                    Endpoint = "http://worker2.example.com:5000",
                    Status = WorkerConstants.Status.Active,
                    LastHeartbeat = DateTime.UtcNow,
                    Capacity = 8,
                    CurrentLoad = 8, // Fully loaded
                    CreatedOn = DateTime.UtcNow.AddDays(-25),
                    CreatedBy = "System",
                    LastModifiedOn = DateTime.UtcNow,
                    LastModifiedBy = "System",
                },
                new WorkerNode
                {
                    Name = "Worker-3",
                    Endpoint = "http://worker3.example.com:5000",
                    Status = WorkerConstants.Status.Idle,
                    LastHeartbeat = DateTime.UtcNow,
                    Capacity = 5,
                    CurrentLoad = 0, // Idle
                    CreatedOn = DateTime.UtcNow.AddDays(-20),
                    CreatedBy = "System",
                    LastModifiedOn = DateTime.UtcNow,
                    LastModifiedBy = "System",
                },
                new WorkerNode
                {
                    Name = "Worker-4",
                    Endpoint = "http://worker4.example.com:5000",
                    Status = WorkerConstants.Status.Offline,
                    LastHeartbeat = DateTime.UtcNow.AddDays(-2), // Older heartbeat to simulate offline
                    Capacity = 12,
                    CurrentLoad = 0,
                    CreatedOn = DateTime.UtcNow.AddDays(-15),
                    CreatedBy = "System",
                    LastModifiedOn = DateTime.UtcNow,
                    LastModifiedBy = "System",
                },
                new WorkerNode
                {
                    Name = "Worker-5",
                    Endpoint = "http://worker5.example.com:5000",
                    Status = WorkerConstants.Status.Active,
                    LastHeartbeat = DateTime.UtcNow,
                    Capacity = 15,
                    CurrentLoad = 7,
                    CreatedOn = DateTime.UtcNow.AddDays(-10),
                    CreatedBy = "System",
                    LastModifiedOn = DateTime.UtcNow,
                    LastModifiedBy = "System",
                }
            };

            dbContext.WorkerNodes.AddRange(workerNodes);
            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation($"Created {workerNodes.Count} mock worker nodes");
        }

        private async Task SeedJobsAndLogs(AppDbContext dbContext, CancellationToken cancellationToken)
        {
            // Check if jobs already exist
            if (await dbContext.Jobs.AnyAsync(cancellationToken))
            {
                _logger.LogInformation("Jobs already exist, skipping job seeding");
                return;
            }

            _logger.LogInformation("Creating mock jobs and job logs");

            // Get active worker nodes
            var workers = await dbContext.WorkerNodes
                .Where(w => w.Status != WorkerConstants.Status.Offline)
                .ToListAsync(cancellationToken);

            if (!workers.Any())
            {
                _logger.LogWarning("No active worker nodes found, skipping job creation");
                return;
            }

            // Create sample job data
            var jobNames = new[]
            {
                "Data Processing", "Daily Backup", "Weekly Report", "Monthly Analytics",
                "Data Migration", "System Maintenance", "Security Scan", "Database Cleanup",
                "Log Analysis", "Performance Testing", "Content Indexing", "Email Campaign",
                "Notification Delivery", "Image Processing", "Video Encoding", "PDF Generation"
            };

            var jobDescriptions = new[]
            {
                "Processes customer data for analytics",
                "Creates backup of all system data",
                "Generates weekly sales and performance report",
                "Analyzes monthly trends and creates dashboards",
                "Migrates data between systems",
                "Performs system maintenance tasks",
                "Scans system for security vulnerabilities",
                "Cleans up old database records",
                "Analyzes log files for patterns and anomalies",
                "Tests system performance under load",
                "Indexes content for search",
                "Sends marketing emails to customers",
                "Delivers system notifications to users",
                "Processes and optimizes images",
                "Encodes video content for multiple platforms",
                "Generates PDF reports"
            };

            // Create various jobs with different statuses
            var jobs = new List<Job>();
            var now = DateTime.UtcNow;

            // Create completed jobs (with full history)
            for (int i = 0; i < 25; i++)
            {
                var nameIndex = _random.Next(jobNames.Length);
                var descIndex = _random.Next(jobDescriptions.Length);
                var priority = GetRandomPriority();
                var worker = workers[_random.Next(workers.Count)];
                var startTime = now.AddDays(-_random.Next(1, 30)).AddHours(-_random.Next(1, 24));
                var duration = TimeSpan.FromMinutes(_random.Next(5, 180)); // 5 min to 3 hours
                var endTime = startTime.Add(duration);

                var job = new Job
                {
                    Name = $"{jobNames[nameIndex]} #{i + 1}",
                    Description = jobDescriptions[descIndex],
                    Status = JobStatus.Completed,
                    Priority = priority,
                    Progress = 100,
                    StartTime = startTime,
                    EndTime = endTime,
                    WorkerNodeId = worker.Id,
                    Type = GetRandomJobType(),
                    MaxRetryAttempts = 3,
                    CurrentRetryCount = 0,
                    CreatedOn = startTime.AddMinutes(-_random.Next(5, 60)),
                    CreatedBy = "System",
                    LastModifiedOn = DateTime.UtcNow,
                    LastModifiedBy = "System",
                };

                jobs.Add(job);

            }

            // Create failed jobs
            for (int i = 0; i < 10; i++)
            {
                var nameIndex = _random.Next(jobNames.Length);
                var descIndex = _random.Next(jobDescriptions.Length);
                var priority = GetRandomPriority();
                var worker = workers[_random.Next(workers.Count)];
                var startTime = now.AddDays(-_random.Next(1, 10)).AddHours(-_random.Next(1, 24));
                var duration = TimeSpan.FromMinutes(_random.Next(1, 30)); // Failed after 1-30 min
                var endTime = startTime.Add(duration);
                var retryCount = _random.Next(0, 3);

                var job = new Job
                {
                    Name = $"{jobNames[nameIndex]} #{i + 26}",
                    Description = jobDescriptions[descIndex],
                    Status = JobStatus.Failed,
                    Priority = priority,
                    Progress = _random.Next(10, 90), // Failed at some point
                    StartTime = startTime,
                    EndTime = endTime,
                    WorkerNodeId = worker.Id,
                    Type = GetRandomJobType(),
                    MaxRetryAttempts = 3,
                    CurrentRetryCount = retryCount,
                    ErrorMessage = GetRandomErrorMessage(),
                    CreatedOn = startTime.AddMinutes(-_random.Next(5, 60)),
                    CreatedBy = "System",
                    LastModifiedOn = DateTime.UtcNow,
                    LastModifiedBy = "System",
                };

                jobs.Add(job);

            }

            // Create running jobs
            for (int i = 0; i < 5; i++)
            {
                var nameIndex = _random.Next(jobNames.Length);
                var descIndex = _random.Next(jobDescriptions.Length);
                var priority = GetRandomPriority();
                var worker = workers.FirstOrDefault(w => w.CurrentLoad < w.Capacity && w.Status == WorkerConstants.Status.Active);
                if (worker == null) continue;

                var startTime = now.AddMinutes(-_random.Next(5, 120)); // Started 5 min to 2 hours ago
                var progress = _random.Next(10, 95); // Still in progress

                var job = new Job
                {
                    Name = $"{jobNames[nameIndex]} #{i + 36}",
                    Description = jobDescriptions[descIndex],
                    Status = JobStatus.Running,
                    Priority = priority,
                    Progress = progress,
                    StartTime = startTime,
                    EndTime = null, // Still running
                    WorkerNodeId = worker.Id,
                    Type = GetRandomJobType(),
                    MaxRetryAttempts = 3,
                    CurrentRetryCount = 0,
                    CreatedOn = startTime.AddMinutes(-_random.Next(1, 30)),
                    CreatedBy = "System",
                    LastModifiedOn = DateTime.UtcNow,
                    LastModifiedBy = "System",
                };

                jobs.Add(job);

            }

            // Create pending jobs
            for (int i = 0; i < 15; i++)
            {
                var nameIndex = _random.Next(jobNames.Length);
                var descIndex = _random.Next(jobDescriptions.Length);
                var priority = GetRandomPriority();
                var createdTime = now.AddMinutes(-_random.Next(1, 60));
                var scheduledTime = _random.Next(2) == 0 ? (DateTime?)now.AddMinutes(_random.Next(10, 120)) : null; // 50% chance of scheduled job

                var job = new Job
                {
                    Name = $"{jobNames[nameIndex]} #{i + 41}",
                    Description = jobDescriptions[descIndex],
                    Status = JobStatus.Pending,
                    Priority = priority,
                    Progress = 0,
                    StartTime = null,
                    EndTime = null,
                    ScheduledStartTime = scheduledTime,
                    WorkerNodeId = null, // Not assigned yet
                    Type = GetRandomJobType(),
                    MaxRetryAttempts = 3,
                    CurrentRetryCount = 0,
                    CreatedOn = createdTime,
                    CreatedBy = "System",
                    LastModifiedOn = DateTime.UtcNow,
                    LastModifiedBy = "System",
                };

                jobs.Add(job);
            }

            // Create stopped jobs
            for (int i = 0; i < 5; i++)
            {
                var nameIndex = _random.Next(jobNames.Length);
                var descIndex = _random.Next(jobDescriptions.Length);
                var priority = GetRandomPriority();
                var worker = workers[_random.Next(workers.Count)];
                var startTime = now.AddDays(-_random.Next(1, 10)).AddHours(-_random.Next(1, 24));
                var duration = TimeSpan.FromMinutes(_random.Next(5, 60)); // Ran for 5-60 min before stopping
                var endTime = startTime.Add(duration);

                var job = new Job
                {
                    Name = $"{jobNames[nameIndex]} #{i + 56}",
                    Description = jobDescriptions[descIndex],
                    Status = JobStatus.Stopped,
                    Priority = priority,
                    Progress = _random.Next(10, 95), // Stopped at some point
                    StartTime = startTime,
                    EndTime = endTime,
                    WorkerNodeId = worker.Id,
                    Type = GetRandomJobType(),
                    MaxRetryAttempts = 3,
                    CurrentRetryCount = 0,
                    CreatedOn = startTime.AddMinutes(-_random.Next(5, 60)),
                    CreatedBy = "System",
                    LastModifiedOn = DateTime.UtcNow,
                    LastModifiedBy = "System",
                };

                jobs.Add(job);

            }

            // Add jobs to database first
            await dbContext.Jobs.AddRangeAsync(jobs, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            // NOW generate and add logs after jobs have valid IDs
            var jobLogs = new List<JobLog>();

            // Create logs for completed jobs
            foreach (var job in jobs.Where(j => j.Status == JobStatus.Completed))
            {
                jobLogs.AddRange(GenerateJobLogs(job));
            }

            // Create logs for failed jobs
            foreach (var job in jobs.Where(j => j.Status == JobStatus.Failed))
            {
                jobLogs.AddRange(GenerateJobLogs(job, true));
            }

            // Create logs for running jobs
            foreach (var job in jobs.Where(j => j.Status == JobStatus.Running))
            {
                jobLogs.AddRange(GenerateJobLogs(job, false, true));
            }

            // Create logs for pending jobs
            foreach (var job in jobs.Where(j => j.Status == JobStatus.Pending))
            {
                jobLogs.Add(new JobLog
                {
                    JobId = job.Id,
                    LogType = LogType.Info,
                    Message = "Job created and queued",
                    Timestamp = job.CreatedOn,
                    CreatedOn = job.CreatedOn,
                    CreatedBy = "System",
                    LastModifiedOn = DateTime.UtcNow,
                    LastModifiedBy = "System",
                });
            }

            // Create logs for stopped jobs
            foreach (var job in jobs.Where(j => j.Status == JobStatus.Stopped))
            {
                jobLogs.AddRange(GenerateJobLogs(job, false, false, true));
            }

            // Add job logs to database
            await dbContext.JobLogs.AddRangeAsync(jobLogs, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation($"Created {jobs.Count} mock jobs with {jobLogs.Count} job logs");
        }

        private async Task SeedMetricsData(AppDbContext dbContext, CancellationToken cancellationToken)
        {
            // Check if metrics already exist
            if (await dbContext.JobMetrics.AnyAsync(cancellationToken))
            {
                _logger.LogInformation("Metrics already exist, skipping metrics seeding");
                return;
            }

            _logger.LogInformation("Creating mock metrics data");

            var now = DateTime.UtcNow;
            var jobMetrics = new List<JobMetric>();
            var workerMetrics = new List<WorkerMetric>();
            var queueMetrics = new List<QueueMetric>();
            var metricSnapshots = new List<MetricSnapshot>();

            // Create hourly metrics for the last 48 hours
            for (int i = 0; i < 48; i++)
            {
                var timestamp = now.AddHours(-i);

                // Create job metrics
                var jobMetric = new JobMetric
                {
                    Timestamp = timestamp,
                    TotalJobs = 50 + _random.Next(-5, 10),
                    PendingJobs = 10 + _random.Next(-5, 5),
                    RunningJobs = 5 + _random.Next(-2, 3),
                    CompletedJobs = 25 + _random.Next(-5, 5),
                    FailedJobs = 5 + _random.Next(-2, 2),
                    StoppedJobs = 5 + _random.Next(-2, 2),
                    AverageExecutionTimeMs = 1800000 + _random.Next(-300000, 300000), // Around 30 minutes
                    SuccessRate = 75 + _random.Next(-10, 10),
                    TotalRetries = _random.Next(0, 20)
                };
                jobMetrics.Add(jobMetric);

                // Create worker metrics
                var workerMetric = new WorkerMetric
                {
                    Timestamp = timestamp,
                    TotalWorkers = 5,
                    ActiveWorkers = 3 + _random.Next(-1, 2),
                    IdleWorkers = 1 + _random.Next(0, 2),
                    OfflineWorkers = _random.Next(0, 2),
                    AverageWorkerUtilization = 60 + _random.Next(-20, 20),
                    TotalCapacity = 50,
                    CurrentLoad = 30 + _random.Next(-10, 10)
                };
                workerMetrics.Add(workerMetric);

                // Create queue metrics
                var queueMetric = new QueueMetric
                {
                    Timestamp = timestamp,
                    TotalQueueLength = 10 + _random.Next(-5, 10),
                    HighPriorityJobs = 3 + _random.Next(-2, 2),
                    RegularPriorityJobs = 5 + _random.Next(-3, 3),
                    LowPriorityJobs = 2 + _random.Next(-1, 3),
                    AverageWaitTimeMs = 600000 + _random.Next(-300000, 300000), // Around 10 minutes
                    JobsProcessedLastMinute = _random.Next(0, 5)
                };
                queueMetrics.Add(queueMetric);

                // Create metric snapshots every 6 hours
                if (i % 6 == 0)
                {
                    var snapshotData = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        JobMetrics = jobMetric,
                        WorkerMetrics = workerMetric,
                        QueueMetrics = queueMetric,
                        GeneratedAt = timestamp
                    });

                    var snapshot = new MetricSnapshot
                    {
                        Timestamp = timestamp,
                        SnapshotData = snapshotData,
                        SnapshotType = i % 24 == 0 ? "Daily" : "Hourly"
                    };
                    metricSnapshots.Add(snapshot);
                }
            }

            // Add weekly snapshots for the last 4 weeks
            for (int i = 1; i <= 4; i++)
            {
                var timestamp = now.AddDays(-i * 7);

                var jobMetric = new JobMetric
                {
                    Timestamp = timestamp,
                    TotalJobs = 300 + _random.Next(-30, 30),
                    PendingJobs = 50 + _random.Next(-10, 10),
                    RunningJobs = 20 + _random.Next(-5, 5),
                    CompletedJobs = 200 + _random.Next(-20, 20),
                    FailedJobs = 20 + _random.Next(-5, 5),
                    StoppedJobs = 10 + _random.Next(-5, 5),
                    AverageExecutionTimeMs = 1800000 + _random.Next(-300000, 300000),
                    SuccessRate = 85 + _random.Next(-5, 5),
                    TotalRetries = _random.Next(10, 50)
                };

                var workerMetric = new WorkerMetric
                {
                    Timestamp = timestamp,
                    TotalWorkers = 5,
                    ActiveWorkers = 4,
                    IdleWorkers = 1,
                    OfflineWorkers = 0,
                    AverageWorkerUtilization = 70 + _random.Next(-10, 10),
                    TotalCapacity = 50,
                    CurrentLoad = 35 + _random.Next(-5, 5)
                };

                var queueMetric = new QueueMetric
                {
                    Timestamp = timestamp,
                    TotalQueueLength = 25 + _random.Next(-10, 10),
                    HighPriorityJobs = 8 + _random.Next(-3, 3),
                    RegularPriorityJobs = 12 + _random.Next(-5, 5),
                    LowPriorityJobs = 5 + _random.Next(-2, 2),
                    AverageWaitTimeMs = 900000 + _random.Next(-300000, 300000),
                    JobsProcessedLastMinute = _random.Next(1, 10)
                };

                var snapshotData = System.Text.Json.JsonSerializer.Serialize(new
                {
                    JobMetrics = jobMetric,
                    WorkerMetrics = workerMetric,
                    QueueMetrics = queueMetric,
                    GeneratedAt = timestamp
                });

                var snapshot = new MetricSnapshot
                {
                    Timestamp = timestamp,
                    SnapshotData = snapshotData,
                    SnapshotType = "Weekly"
                };
                metricSnapshots.Add(snapshot);
            }

            // Add metrics to database
            await dbContext.JobMetrics.AddRangeAsync(jobMetrics, cancellationToken);
            await dbContext.WorkerMetrics.AddRangeAsync(workerMetrics, cancellationToken);
            await dbContext.QueueMetrics.AddRangeAsync(queueMetrics, cancellationToken);
            await dbContext.MetricSnapshots.AddRangeAsync(metricSnapshots, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation($"Created {jobMetrics.Count} job metrics, {workerMetrics.Count} worker metrics, {queueMetrics.Count} queue metrics, and {metricSnapshots.Count} snapshots");
        }

        #region Helper Methods

        /// <summary>
        /// Hashes a password using PBKDF2 with HMAC-SHA256
        /// </summary>
        private string HashPassword(string password)
        {
            using var rng = RandomNumberGenerator.Create();
            var salt = new byte[16];
            rng.GetBytes(salt);

            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);
            var hash = pbkdf2.GetBytes(32);

            var hashBytes = new byte[48]; // 16 bytes salt + 32 bytes hash
            Array.Copy(salt, 0, hashBytes, 0, 16);
            Array.Copy(hash, 0, hashBytes, 16, 32);

            return Convert.ToBase64String(hashBytes);
        }

        private JobPriority GetRandomPriority()
        {
            var priorities = new[]
            {
                JobPriority.High,
                JobPriority.Regular,
                JobPriority.Low,
                JobPriority.Urgent,
                JobPriority.Critical,
                JobPriority.Deferred
            };

            // Weight probabilities - Regular and Low are more common
            var weights = new[] { 20, 40, 25, 5, 5, 5 }; // percentages

            var totalWeight = weights.Sum();
            var randomValue = _random.Next(totalWeight);
            var cumulativeWeight = 0;

            for (int i = 0; i < weights.Length; i++)
            {
                cumulativeWeight += weights[i];
                if (randomValue < cumulativeWeight)
                {
                    return priorities[i];
                }
            }

            // Fallback (should never happen)
            return JobPriority.Regular;
        }

        private JobType GetRandomJobType()
        {
            var types = Enum.GetValues<JobType>();
            return types[_random.Next(types.Length)];
        }

        private string GetRandomErrorMessage()
        {
            var errorMessages = new[]
            {
                "Connection timeout while processing data",
                "Out of memory exception during processing",
                "Database connection failed",
                "Remote service unavailable",
                "Invalid data format encountered",
                "Resource allocation failed",
                "Network connection lost during operation",
                "External API returned error status code",
                "File system permission denied",
                "Unexpected null reference encountered",
                "Processing timeout exceeded",
                "Invalid configuration detected"
            };

            return errorMessages[_random.Next(errorMessages.Length)];
        }

        private List<JobLog> GenerateJobLogs(Job job, bool includeError = false, bool isRunning = false, bool isStopped = false)
        {
            var logs = new List<JobLog>();
            var creationTime = job.CreatedOn;
            var startTime = job.StartTime ?? DateTime.UtcNow;
            var endTime = job.EndTime;

            // Add creation log
            logs.Add(new JobLog
            {
                JobId = job.Id,
                LogType = LogType.Info,
                Message = "Job created and queued",
                Timestamp = creationTime,
                CreatedOn = creationTime,
                CreatedBy = "System",
                LastModifiedOn = DateTime.UtcNow,
                LastModifiedBy = "System",
            });

            // Add scheduling log if applicable
            if (job.ScheduledStartTime.HasValue)
            {
                logs.Add(new JobLog
                {
                    JobId = job.Id,
                    LogType = LogType.Info,
                    Message = $"Job scheduled for {job.ScheduledStartTime.Value:yyyy-MM-dd HH:mm:ss}",
                    Timestamp = creationTime.AddSeconds(1),
                    CreatedOn = creationTime.AddSeconds(1),
                    CreatedBy = "System",
                    LastModifiedOn = DateTime.UtcNow,
                    LastModifiedBy = "System",
                });
            }

            // If job has started
            if (job.StartTime.HasValue)
            {
                // Add start log
                logs.Add(new JobLog
                {
                    JobId = job.Id,
                    LogType = LogType.Info,
                    Message = "Job execution started",
                    Timestamp = startTime,
                    CreatedOn = startTime,
                    CreatedBy = "System",
                    LastModifiedOn = DateTime.UtcNow,
                    LastModifiedBy = "System",
                });

                // Add worker assignment log
                if (job.WorkerNodeId != Guid.Empty)
                {
                    logs.Add(new JobLog
                    {
                        JobId = job.Id,
                        LogType = LogType.Info,
                        Message = $"Job assigned to worker {job.WorkerNodeId}",
                        Timestamp = startTime.AddSeconds(-1),
                        CreatedOn = startTime.AddSeconds(-1),
                        CreatedBy = "System",
                        LastModifiedOn = DateTime.UtcNow,
                        LastModifiedBy = "System",
                    });
                }

                // Add progress logs
                if (job.Progress > 0)
                {
                    // Generate 2-5 progress logs
                    int progressSteps = _random.Next(2, 6);
                    int progressPerStep = job.Progress / progressSteps;

                    DateTime progressTime;
                    if (endTime.HasValue)
                    {
                        progressTime = startTime;
                        var timeInterval = (endTime.Value - startTime) / (progressSteps + 1);

                        for (int i = 1; i <= progressSteps; i++)
                        {
                            progressTime = progressTime.Add(timeInterval);
                            int stepProgress = progressPerStep * i;

                            logs.Add(new JobLog
                            {
                                JobId = job.Id,
                                LogType = LogType.Info,
                                Message = $"Job progress: {stepProgress}%",
                                Timestamp = progressTime,
                                CreatedOn = progressTime,
                                CreatedBy = "System",
                                LastModifiedOn = DateTime.UtcNow,
                                LastModifiedBy = "System",
                            });
                        }
                    }
                    else if (isRunning) // Running but no end time
                    {
                        // For running jobs, space progress updates more evenly
                        progressTime = startTime;
                        TimeSpan runningTime = DateTime.UtcNow - startTime;
                        var timeInterval = runningTime / (progressSteps + 1);

                        for (int i = 1; i <= progressSteps; i++)
                        {
                            progressTime = progressTime.Add(timeInterval);
                            int stepProgress = progressPerStep * i;

                            logs.Add(new JobLog
                            {
                                JobId = job.Id,
                                LogType = LogType.Info,
                                Message = $"Job progress: {stepProgress}%",
                                Timestamp = progressTime,
                                CreatedOn = progressTime,
                                CreatedBy = "System",
                                LastModifiedOn = DateTime.UtcNow,
                                LastModifiedBy = "System",
                            });
                        }
                    }
                }

                // Add error log if job failed
                if (includeError && job.Status == JobStatus.Failed)
                {
                    logs.Add(new JobLog
                    {
                        JobId = job.Id,
                        LogType = LogType.Error,
                        Message = $"Job execution failed: {job.ErrorMessage}",
                        Details = "Stack trace would appear here in a real system",
                        Timestamp = endTime.Value,
                        CreatedOn = endTime.Value,
                        CreatedBy = "System",
                        LastModifiedOn = DateTime.UtcNow,
                        LastModifiedBy = "System",
                    });

                    // Add retry logs if applicable
                    if (job.CurrentRetryCount > 0)
                    {
                        for (int i = 0; i < job.CurrentRetryCount; i++)
                        {
                            var retryTime = endTime.Value.AddMinutes(-(job.CurrentRetryCount - i) * 5);

                            logs.Add(new JobLog
                            {
                                JobId = job.Id,
                                LogType = LogType.Warning,
                                Message = $"Retry attempt {i + 1} of {job.MaxRetryAttempts}",
                                Timestamp = retryTime,
                                CreatedOn = retryTime,
                                CreatedBy = "System",
                                LastModifiedOn = DateTime.UtcNow,
                                LastModifiedBy = "System",
                            });
                        }
                    }
                }

                // Add completion log for completed jobs
                if (job.Status == JobStatus.Completed && endTime.HasValue)
                {
                    logs.Add(new JobLog
                    {
                        JobId = job.Id,
                        LogType = LogType.Info,
                        Message = "Job completed successfully",
                        Timestamp = endTime.Value,
                        CreatedOn = endTime.Value,
                        CreatedBy = "System",
                        LastModifiedOn = DateTime.UtcNow,
                        LastModifiedBy = "System",
                    });
                }

                // Add stopped log for stopped jobs
                if (isStopped && job.Status == JobStatus.Stopped && endTime.HasValue)
                {
                    logs.Add(new JobLog
                    {
                        JobId = job.Id,
                        LogType = LogType.Warning,
                        Message = "Job execution was manually stopped",
                        Timestamp = endTime.Value,
                        CreatedOn = endTime.Value,
                        CreatedBy = "System",
                        LastModifiedOn = DateTime.UtcNow,
                        LastModifiedBy = "System",
                    });
                }
            }

            return logs;
        }

        #endregion
    }
}