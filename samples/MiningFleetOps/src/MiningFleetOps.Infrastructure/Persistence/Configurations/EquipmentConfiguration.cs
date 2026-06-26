using MiningFleetOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MiningFleetOps.Infrastructure.Persistence.Configurations;

public sealed class EquipmentConfiguration : IEntityTypeConfiguration<Equipment>
{
    public void Configure(EntityTypeBuilder<Equipment> builder)
    {
        builder.ToTable("Equipment", "mining");
        builder.HasKey(x => x.EquipmentId);
        builder.Property(x => x.EquipmentId)
            .HasColumnName("EquipmentId")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.SiteId)
            .HasColumnName("SiteId");
        builder.Property(x => x.EquipmentClassId)
            .HasColumnName("EquipmentClassId");
        builder.Property(x => x.AssetTag)
            .HasColumnName("AssetTag")
            .HasMaxLength(40)
            .IsRequired();
        builder.Property(x => x.SerialNumber)
            .HasColumnName("SerialNumber")
            .HasMaxLength(80);
        builder.Property(x => x.Manufacturer)
            .HasColumnName("Manufacturer")
            .HasMaxLength(80);
        builder.Property(x => x.Model)
            .HasColumnName("Model")
            .HasMaxLength(80);
        builder.Property(x => x.CommissionDate)
            .HasColumnName("CommissionDate");
        builder.Property(x => x.FuelTypeId)
            .HasColumnName("FuelTypeId");
        builder.Property(x => x.TankCapacityL)
            .HasColumnName("TankCapacityL");
        builder.Property(x => x.CurrentHourMeter)
            .HasColumnName("CurrentHourMeter");
        builder.Property(x => x.CurrentOdometerKm)
            .HasColumnName("CurrentOdometerKm");
        builder.Property(x => x.Status)
            .HasColumnName("Status")
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(x => x.IsActive)
            .HasColumnName("IsActive");
        builder.Property(x => x.CreatedAt)
            .HasColumnName("CreatedAt");
    }
}
