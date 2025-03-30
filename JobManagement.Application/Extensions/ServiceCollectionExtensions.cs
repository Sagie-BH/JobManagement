using JobManagement.Application.BackgroundServices;
using JobManagement.Application.Services;
using JobManagement.Infrastructure.Interfaces;
using JobManagement.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace JobManagement.Application.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            // Register basic job services
            services.AddScoped<JobService>();
            services.AddScoped<WorkerNodeService>();

            // Register job execution services (implementations of interfaces)
            services.AddScoped<IJobExecutionService, JobExecutionService>();
            services.AddScoped<IWorkerAssignmentService, WorkerAssignmentService>();

            // Register metrics services
            services.AddScoped<IMetricsService, MetricsService>();

            // Register background services
            services.AddHostedService<QueueProcessingService>();
            services.AddHostedService<MetricsCollectionService>();

            return services;
        }
    }
}