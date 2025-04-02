using JobManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobManagement.Infrastructure.Data.Configurations
{
    public class JobMetricConfiguration : IEntityTypeConfiguration<JobMetric>
    {
        public void Configure(EntityTypeBuilder<JobMetric> builder)
        {
            builder.HasIndex(m => m.Timestamp);
        }
    }

    public class WorkerMetricConfiguration : IEntityTypeConfiguration<WorkerMetric>
    {
        public void Configure(EntityTypeBuilder<WorkerMetric> builder)
        {
            builder.HasIndex(m => m.Timestamp);
        }
    }

    public class QueueMetricConfiguration : IEntityTypeConfiguration<QueueMetric>
    {
        public void Configure(EntityTypeBuilder<QueueMetric> builder)
        {
            builder.HasIndex(m => m.Timestamp);
        }
    }

    public class MetricSnapshotConfiguration : IEntityTypeConfiguration<MetricSnapshot>
    {
        public void Configure(EntityTypeBuilder<MetricSnapshot> builder)
        {
            builder.HasIndex(m => m.Timestamp);
            builder.HasIndex(m => m.SnapshotType);
            builder.Property(m => m.SnapshotData).HasColumnType("nvarchar(max)");
        }
    }
}