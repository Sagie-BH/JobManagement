using JobManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobManagement.Infrastructure.Data.Configurations
{
    public class JobConfiguration : IEntityTypeConfiguration<Job>
    {
        public void Configure(EntityTypeBuilder<Job> builder)
        {
            builder.HasOne(j => j.AssignedWorker)
                .WithMany(w => w.AssignedJobs)
                .HasForeignKey(j => j.WorkerNodeId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasMany(j => j.ExecutionLogs)
                .WithOne(l => l.Job)
                .HasForeignKey(l => l.JobId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(j => j.ScheduledStartTime);
            builder.HasIndex(j => j.WorkerNodeId);
        }
    }
}
