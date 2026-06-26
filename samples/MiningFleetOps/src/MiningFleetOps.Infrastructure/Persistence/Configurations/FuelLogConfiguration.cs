using MiningFleetOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MiningFleetOps.Infrastructure.Persistence.Configurations;

public sealed class FuelLogConfiguration : IEntityTypeConfiguration<FuelLog>
{
    public void Configure(EntityTypeBuilder<FuelLog> builder)
    {
        builder.ToTable("FuelLog", "mining");
        builder.HasKey(x => x.FuelLogId);
        builder.Property(x => x.FuelLogId)
            .HasColumnName("FuelLogId")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.EquipmentId)
            .HasColumnName("EquipmentId");
        builder.Property(x => x.FuelTypeId)
            .HasColumnName("FuelTypeId");
        builder.Property(x => x.FueledAt)
            .HasColumnName("FueledAt");
        builder.Property(x => x.ShiftId)
            .HasColumnName("ShiftId");
        builder.Property(x => x.EmployeeId)
            .HasColumnName("EmployeeId");
        builder.Property(x => x.PitId)
            .HasColumnName("PitId");
        builder.Property(x => x.HourMeter)
            .HasColumnName("HourMeter");
        builder.Property(x => x.OdometerKm)
            .HasColumnName("OdometerKm");
        builder.Property(x => x.Liters)
            .HasColumnName("Liters");
        builder.Property(x => x.UnitCost)
            .HasColumnName("UnitCost");
        builder.Property(x => x.HoursSinceLastFuel)
            .HasColumnName("HoursSinceLastFuel");
        builder.Property(x => x.FuelBurnLph)
            .HasColumnName("FuelBurnLph");
        builder.Property(x => x.Co2KgPerL)
            .HasColumnName("Co2KgPerL");
        builder.Property(x => x.SourceName)
            .HasColumnName("SourceName")
            .HasMaxLength(40)
            .IsRequired();
        builder.Property(x => x.Notes)
            .HasColumnName("Notes")
            .HasMaxLength(500);
        builder.Property(x => x.CreatedAt)
            .HasColumnName("CreatedAt");
        builder.Property(x => x.CostAmount)
            .HasColumnName("CostAmount");
        builder.Property(x => x.Co2Kg)
            .HasColumnName("Co2Kg");
    }
}
