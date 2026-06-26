using MiningFleetOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MiningFleetOps.Infrastructure.Persistence.Configurations;

public sealed class WorkOrderTaskConfiguration : IEntityTypeConfiguration<WorkOrderTask>
{
    public void Configure(EntityTypeBuilder<WorkOrderTask> builder)
    {
        builder.ToTable("WorkOrderTask", "mining");
        builder.HasKey(x => x.WorkOrderTaskId);
        builder.Property(x => x.WorkOrderTaskId)
            .HasColumnName("WorkOrderTaskId")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.WorkOrderId)
            .HasColumnName("WorkOrderId");
        builder.Property(x => x.TaskSequence)
            .HasColumnName("TaskSequence");
        builder.Property(x => x.TaskDescription)
            .HasColumnName("TaskDescription")
            .HasMaxLength(500)
            .IsRequired();
        builder.Property(x => x.IsCompleted)
            .HasColumnName("IsCompleted");
        builder.Property(x => x.CompletedAt)
            .HasColumnName("CompletedAt");
        builder.Property(x => x.CompletedByEmployeeId)
            .HasColumnName("CompletedByEmployeeId");
    }
}
