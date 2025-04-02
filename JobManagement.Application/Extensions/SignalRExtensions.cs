using JobManagement.Application.Services.JobSignalR;
using JobManagement.Application.Services.WorkerSignalR;
using JobManagement.Infrastructure.Interfaces.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace JobManagement.Application.Extensions
{
    /// <summary>
    /// Extension methods for configuring SignalR services
    /// </summary>
    public static class SignalRExtensions
    {
        /// <summary>
        /// Adds SignalR services to the service collection
        /// </summary>
        public static IServiceCollection AddSignalRServices(this IServiceCollection services)
        {
            // Register SignalR services
            services.AddScoped<IJobSignalRService, JobSignalRService>();
            services.AddScoped<IWorkerSignalRService, WorkerSignalRService>();

            return services;
        }
    }
}