using JobManagement.Application.BackgroundServices;
using JobManagement.Application.Services;
using JobManagement.Application.Services.JobExecutionService;
using JobManagement.Application.Services.JobServices;
using JobManagement.Application.Services.Metrics;
using JobManagement.Application.Services.WorkerAssignment;
using JobManagement.Application.Services.WorkerNodes;
using JobManagement.Infrastructure.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace JobManagement.Application.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<IJobService, JobService>();
            services.AddScoped<IWorkerNodeService, WorkerNodeService>();

            // Register job execution services (implementations of interfaces)
            services.AddScoped<IJobExecutionService, JobExecutionService>();
            services.AddScoped<IWorkerAssignmentService, WorkerAssignmentService>();

            // Register metrics services
            services.AddScoped<IMetricsService, MetricsService>();

            // Register background services
            services.AddHostedService<QueueProcessingService>();
            services.AddHostedService<MetricsCollectionService>();

            // Register enhanced simulation services
            services.AddHostedService<SystemSimulationService>();
            services.AddHostedService<JobCreationSimulatorService>();
            services.AddHostedService<WorkerLoadSyncService>();
            services.AddHostedService<SystemHealthMonitoringService>();

            // Uncomment if you want to use the original progress simulator in addition
            // services.AddHostedService<JobProgressSimulationService>();

            return services;
        }
    }
}