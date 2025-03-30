using JobManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobManagement.Infrastructure.Data.Configurations
{
    public class WorkerNodeConfiguration : IEntityTypeConfiguration<WorkerNode>
    {
        public void Configure(EntityTypeBuilder<WorkerNode> builder)
        {
            builder.HasIndex(w => w.Name).IsUnique();
            builder.HasIndex(w => w.Status);
            builder.HasIndex(w => w.LastHeartbeat);

            builder.Property(w => w.Capacity).HasDefaultValue(5);
            builder.Property(w => w.CurrentLoad).HasDefaultValue(0);
        }
    }
}