using JobManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobManagement.Infrastructure.Data.Configurations
{
    public class JobLogConfiguration : IEntityTypeConfiguration<JobLog>
    {
        public void Configure(EntityTypeBuilder<JobLog> builder)
        {
            builder.HasIndex(l => l.JobId);
            builder.HasIndex(l => l.LogType);
            builder.HasIndex(l => l.Timestamp);
        }
    }
}