using MiningFleetOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MiningFleetOps.Infrastructure.Persistence.Configurations;

public sealed class EquipmentClassConfiguration : IEntityTypeConfiguration<EquipmentClass>
{
    public void Configure(EntityTypeBuilder<EquipmentClass> builder)
    {
        builder.ToTable("EquipmentClass", "mining");
        builder.HasKey(x => x.EquipmentClassId);
        builder.Property(x => x.EquipmentClassId)
            .HasColumnName("EquipmentClassId")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.ClassCode)
            .HasColumnName("ClassCode")
            .HasMaxLength(40)
            .IsRequired();
        builder.Property(x => x.ClassName)
            .HasColumnName("ClassName")
            .HasMaxLength(120)
            .IsRequired();
        builder.Property(x => x.CategoryName)
            .HasColumnName("CategoryName")
            .HasMaxLength(80)
            .IsRequired();
        builder.Property(x => x.TypicalPayloadTonnes)
            .HasColumnName("TypicalPayloadTonnes");
        builder.Property(x => x.DefaultFuelBurnLph)
            .HasColumnName("DefaultFuelBurnLph");
        builder.Property(x => x.MaintenanceIntervalHours)
            .HasColumnName("MaintenanceIntervalHours");
        builder.Property(x => x.OilIntervalHours)
            .HasColumnName("OilIntervalHours");
    }
}
