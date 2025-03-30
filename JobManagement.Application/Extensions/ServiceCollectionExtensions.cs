using JobManagement.Application.BackgroundServices;
using JobManagement.Application.Services;
using JobManagement.Domain.Interfaces;
using JobManagement.Infrastructure.Interfaces;
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

            // Register background service
            services.AddHostedService<QueueProcessingService>();

            return services;
        }
    }
}