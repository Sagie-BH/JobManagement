using Microsoft.EntityFrameworkCore;

namespace JobManagement.Infrastructure.Data.Extensions
{
    /// <summary>
    /// Extension methods for configuring the database model
    /// </summary>
    public static class ModelBuilderExtensions
    {
        #region Auth Configuration
        public static void ApplyAuthConfiguration(this ModelBuilder modelBuilder)
        {
            // Configure authentication relationships
            modelBuilder.ConfigureUserRoles();
            modelBuilder.ConfigureRolePermissions();
            modelBuilder.ConfigureRefreshTokens();
        }

        private static void ConfigureUserRoles(this ModelBuilder modelBuilder)
        {
            // Configure UserRole as a join entity with composite key
            modelBuilder.Entity<Domain.Entities.UserRole>()
                .HasKey(ur => new { ur.UserId, ur.RoleId });

            modelBuilder.Entity<Domain.Entities.UserRole>()
                .HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId);

            modelBuilder.Entity<Domain.Entities.UserRole>()
                .HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId);

            // Add indexes
            modelBuilder.Entity<Domain.Entities.User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<Domain.Entities.User>()
                .HasIndex(u => u.Email)
                .IsUnique();
        }

        private static void ConfigureRolePermissions(this ModelBuilder modelBuilder)
        {
            // Configure RolePermission as a join entity with composite key
            modelBuilder.Entity<Domain.Entities.RolePermission>()
                .HasKey(rp => new { rp.RoleId, rp.PermissionId });

            modelBuilder.Entity<Domain.Entities.RolePermission>()
                .HasOne(rp => rp.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(rp => rp.RoleId);

            modelBuilder.Entity<Domain.Entities.RolePermission>()
                .HasOne(rp => rp.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionId);

            // Add indexes
            modelBuilder.Entity<Domain.Entities.Role>()
                .HasIndex(r => r.Name)
                .IsUnique();

            modelBuilder.Entity<Domain.Entities.Permission>()
                .HasIndex(p => p.Name)
                .IsUnique();
        }

        private static void ConfigureRefreshTokens(this ModelBuilder modelBuilder)
        {
            // Configure RefreshToken
            modelBuilder.Entity<Domain.Entities.RefreshToken>()
                .HasOne(rt => rt.User)
                .WithMany()
                .HasForeignKey(rt => rt.UserId);

            // Add index
            modelBuilder.Entity<Domain.Entities.RefreshToken>()
                .HasIndex(rt => rt.Token)
                .IsUnique();
        }
        #endregion

        #region Job Configuration
        public static void ApplyJobConfiguration(this ModelBuilder modelBuilder)
        {
            // Configure Job entity and relationships
            modelBuilder.Entity<Domain.Entities.Job>()
                .HasMany(j => j.ExecutionLogs)
                .WithOne(l => l.Job)
                .HasForeignKey(l => l.JobId)
                .OnDelete(DeleteBehavior.Cascade);

            // Add indexes for common query patterns
            modelBuilder.Entity<Domain.Entities.Job>()
                .HasIndex(j => j.Status);

            modelBuilder.Entity<Domain.Entities.Job>()
                .HasIndex(j => j.Priority);

            modelBuilder.Entity<Domain.Entities.Job>()
                .HasIndex(j => j.ScheduledStartTime);

            modelBuilder.Entity<Domain.Entities.JobLog>()
                .HasIndex(l => l.JobId);

            modelBuilder.Entity<Domain.Entities.JobLog>()
                .HasIndex(l => l.LogType);

            modelBuilder.Entity<Domain.Entities.JobLog>()
                .HasIndex(l => l.Timestamp);
        }
        #endregion

        #region Worker Configuration
        public static void ApplyWorkerConfiguration(this ModelBuilder modelBuilder)
        {
            // Configure WorkerNode entity and relationships
            modelBuilder.Entity<Domain.Entities.WorkerNode>()
                .HasMany(w => w.AssignedJobs)
                .WithOne(j => j.AssignedWorker)
                .HasForeignKey(j => j.WorkerNodeId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            // Add indexes for common query patterns
            modelBuilder.Entity<Domain.Entities.WorkerNode>()
                .HasIndex(w => w.Name)
                .IsUnique();

            modelBuilder.Entity<Domain.Entities.WorkerNode>()
                .HasIndex(w => w.Status);

            modelBuilder.Entity<Domain.Entities.WorkerNode>()
                .HasIndex(w => w.LastHeartbeat);

            // Default values
            modelBuilder.Entity<Domain.Entities.WorkerNode>()
                .Property(w => w.Capacity)
                .HasDefaultValue(5);

            modelBuilder.Entity<Domain.Entities.WorkerNode>()
                .Property(w => w.CurrentLoad)
                .HasDefaultValue(0);
        }
        #endregion

        #region Metrics Configuration
        public static void ApplyMetricsConfiguration(this ModelBuilder modelBuilder)
        {
            // Add indexes for all metrics tables based on timestamp
            modelBuilder.Entity<Domain.Entities.JobMetric>()
                .HasIndex(m => m.Timestamp);

            modelBuilder.Entity<Domain.Entities.WorkerMetric>()
                .HasIndex(m => m.Timestamp);

            modelBuilder.Entity<Domain.Entities.QueueMetric>()
                .HasIndex(m => m.Timestamp);

            modelBuilder.Entity<Domain.Entities.MetricSnapshot>()
                .HasIndex(m => m.Timestamp);

            modelBuilder.Entity<Domain.Entities.MetricSnapshot>()
                .HasIndex(m => m.SnapshotType);

            // Configure large text storage for snapshot data
            modelBuilder.Entity<Domain.Entities.MetricSnapshot>()
                .Property(m => m.SnapshotData)
                .HasColumnType("nvarchar(max)");
        }
        #endregion

        #region Queue Configuration
        public static void ApplyQueueConfiguration(this ModelBuilder modelBuilder)
        {
            // Configure JobQueueState
            modelBuilder.Entity<Queue.JobQueueState>()
                .Property(q => q.QueueData)
                .HasColumnType("nvarchar(max)");
        }
        #endregion
    }
}