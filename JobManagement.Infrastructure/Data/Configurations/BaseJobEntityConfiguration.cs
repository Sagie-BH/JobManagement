using JobManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobManagement.Infrastructure.Data.Configurations
{
    public class BaseJobEntityConfiguration : IEntityTypeConfiguration<BaseJobEntity>
    {
        public void Configure(EntityTypeBuilder<BaseJobEntity> builder)
        {
            builder.HasIndex(j => j.Status);
            builder.HasIndex(j => j.Priority);
            builder.HasIndex(j => j.StartTime);
            builder.HasIndex(j => j.EndTime);
        }
    }
}