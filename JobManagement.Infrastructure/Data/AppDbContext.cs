using JobManagement.Domain.Entities;
using JobManagement.Infrastructure.Data.Extensions;
using JobManagement.Infrastructure.Queue;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace JobManagement.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AppDbContext(
            DbContextOptions<AppDbContext> options,
            IHttpContextAccessor httpContextAccessor = null)
            : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        #region Job-related DbSets
        public DbSet<Job> Jobs { get; set; }
        public DbSet<JobLog> JobLogs { get; set; }
        #endregion

        #region Worker-related DbSets
        public DbSet<WorkerNode> WorkerNodes { get; set; }
        #endregion

        #region Metrics-related DbSets
        public DbSet<JobMetric> JobMetrics { get; set; }
        public DbSet<WorkerMetric> WorkerMetrics { get; set; }
        public DbSet<QueueMetric> QueueMetrics { get; set; }
        public DbSet<MetricSnapshot> MetricSnapshots { get; set; }
        #endregion

        #region Queue-related DbSets
        public DbSet<JobQueueState> JobQueueStates { get; set; }
        #endregion

        #region Authentication-related DbSets
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        #endregion

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Apply configurations from current assembly
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

            // Apply domain-specific configurations from extension methods
            modelBuilder.ApplyAuthConfiguration();
            modelBuilder.ApplyJobConfiguration();
            modelBuilder.ApplyWorkerConfiguration();
            modelBuilder.ApplyMetricsConfiguration();
            modelBuilder.ApplyQueueConfiguration();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateAuditableEntities();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateAuditableEntities()
        {
            var currentUser = _httpContextAccessor?.HttpContext?.User?.Identity?.Name ?? "System";
            var now = DateTime.UtcNow;

            foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedBy = currentUser;
                        entry.Entity.CreatedOn = now;
                        break;
                    case EntityState.Modified:
                        entry.Entity.LastModifiedBy = currentUser;
                        entry.Entity.LastModifiedOn = now;
                        break;
                }
            }
        }
    }
}