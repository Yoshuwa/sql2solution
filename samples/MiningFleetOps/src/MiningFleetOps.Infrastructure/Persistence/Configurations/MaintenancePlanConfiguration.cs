using MiningFleetOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MiningFleetOps.Infrastructure.Persistence.Configurations;

public sealed class MaintenancePlanConfiguration : IEntityTypeConfiguration<MaintenancePlan>
{
    public void Configure(EntityTypeBuilder<MaintenancePlan> builder)
    {
        builder.ToTable("MaintenancePlan", "mining");
        builder.HasKey(x => x.MaintenancePlanId);
        builder.Property(x => x.MaintenancePlanId)
            .HasColumnName("MaintenancePlanId")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.EquipmentClassId)
            .HasColumnName("EquipmentClassId");
        builder.Property(x => x.PlanCode)
            .HasColumnName("PlanCode")
            .HasMaxLength(40)
            .IsRequired();
        builder.Property(x => x.PlanName)
            .HasColumnName("PlanName")
            .HasMaxLength(160)
            .IsRequired();
        builder.Property(x => x.IntervalHours)
            .HasColumnName("IntervalHours");
        builder.Property(x => x.IntervalDays)
            .HasColumnName("IntervalDays");
        builder.Property(x => x.EstimatedDurationHours)
            .HasColumnName("EstimatedDurationHours");
        builder.Property(x => x.IsActive)
            .HasColumnName("IsActive");
    }
}
