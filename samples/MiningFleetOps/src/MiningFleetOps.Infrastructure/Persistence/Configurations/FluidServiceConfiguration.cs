using MiningFleetOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MiningFleetOps.Infrastructure.Persistence.Configurations;

public sealed class FluidServiceConfiguration : IEntityTypeConfiguration<FluidService>
{
    public void Configure(EntityTypeBuilder<FluidService> builder)
    {
        builder.ToTable("FluidService", "mining");
        builder.HasKey(x => x.FluidServiceId);
        builder.Property(x => x.FluidServiceId)
            .HasColumnName("FluidServiceId")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.EquipmentId)
            .HasColumnName("EquipmentId");
        builder.Property(x => x.FluidTypeId)
            .HasColumnName("FluidTypeId");
        builder.Property(x => x.ServicedAt)
            .HasColumnName("ServicedAt");
        builder.Property(x => x.HourMeter)
            .HasColumnName("HourMeter");
        builder.Property(x => x.LitersChanged)
            .HasColumnName("LitersChanged");
        builder.Property(x => x.FilterChanged)
            .HasColumnName("FilterChanged");
        builder.Property(x => x.WorkOrderId)
            .HasColumnName("WorkOrderId");
        builder.Property(x => x.TechnicianEmployeeId)
            .HasColumnName("TechnicianEmployeeId");
        builder.Property(x => x.NextDueHourMeter)
            .HasColumnName("NextDueHourMeter");
        builder.Property(x => x.Notes)
            .HasColumnName("Notes")
            .HasMaxLength(500);
    }
}
