using MiningFleetOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MiningFleetOps.Infrastructure.Persistence.Configurations;

public sealed class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.ToTable("WorkOrder", "mining");
        builder.HasKey(x => x.WorkOrderId);
        builder.Property(x => x.WorkOrderId)
            .HasColumnName("WorkOrderId")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.WorkOrderNumber)
            .HasColumnName("WorkOrderNumber")
            .HasMaxLength(40)
            .IsRequired();
        builder.Property(x => x.EquipmentId)
            .HasColumnName("EquipmentId");
        builder.Property(x => x.MaintenancePlanId)
            .HasColumnName("MaintenancePlanId");
        builder.Property(x => x.OpenedAt)
            .HasColumnName("OpenedAt");
        builder.Property(x => x.ClosedAt)
            .HasColumnName("ClosedAt");
        builder.Property(x => x.PriorityName)
            .HasColumnName("PriorityName")
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(x => x.WorkOrderType)
            .HasColumnName("WorkOrderType")
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(x => x.Status)
            .HasColumnName("Status")
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(x => x.OpenHourMeter)
            .HasColumnName("OpenHourMeter");
        builder.Property(x => x.CloseHourMeter)
            .HasColumnName("CloseHourMeter");
        builder.Property(x => x.ProblemDescription)
            .HasColumnName("ProblemDescription")
            .HasMaxLength(1000)
            .IsRequired();
        builder.Property(x => x.CorrectiveAction)
            .HasColumnName("CorrectiveAction")
            .HasMaxLength(1000);
        builder.Property(x => x.LaborHours)
            .HasColumnName("LaborHours");
        builder.Property(x => x.EstimatedCost)
            .HasColumnName("EstimatedCost");
        builder.Property(x => x.ActualCost)
            .HasColumnName("ActualCost");
        builder.Property(x => x.CreatedByEmployeeId)
            .HasColumnName("CreatedByEmployeeId");
        builder.Property(x => x.ClosedByEmployeeId)
            .HasColumnName("ClosedByEmployeeId");
        builder.Property(x => x.DowntimeHours)
            .HasColumnName("DowntimeHours");
    }
}
