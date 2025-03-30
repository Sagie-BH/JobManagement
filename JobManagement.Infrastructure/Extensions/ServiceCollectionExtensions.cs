using JobManagement.Domain.Interfaces;
using JobManagement.Infrastructure.Data;
using JobManagement.Infrastructure.Interfaces;
using JobManagement.Infrastructure.Queue;
using JobManagement.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobManagement.Infrastructure.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Register DbContext
            services.AddDbContext<AppDbContext>(options =>
                options.UseMySql(
                    configuration.GetConnectionString("DefaultConnection"),
                    new MySqlServerVersion(new Version(8, 0, 21)),
                    b => b.MigrationsAssembly("JobManagement.Infrastructure")
                )
            );

            // Register repositories and unit of work
            services.AddRepositories();

            // Register queue services
            services.AddQueueServices();

            return services;
        }

        public static IServiceCollection AddRepositories(this IServiceCollection services)
        {
            services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            return services;
        }

        public static IServiceCollection AddQueueServices(this IServiceCollection services)
        {
            services.AddSingleton<IJobQueue, PriorityJobQueue>();

            return services;
        }
    }
}