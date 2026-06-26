using MiningFleetOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MiningFleetOps.Infrastructure.Persistence.Configurations;

public sealed class FluidSampleConfiguration : IEntityTypeConfiguration<FluidSample>
{
    public void Configure(EntityTypeBuilder<FluidSample> builder)
    {
        builder.ToTable("FluidSample", "mining");
        builder.HasKey(x => x.FluidSampleId);
        builder.Property(x => x.FluidSampleId)
            .HasColumnName("FluidSampleId")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.EquipmentId)
            .HasColumnName("EquipmentId");
        builder.Property(x => x.FluidTypeId)
            .HasColumnName("FluidTypeId");
        builder.Property(x => x.SampledAt)
            .HasColumnName("SampledAt");
        builder.Property(x => x.HourMeter)
            .HasColumnName("HourMeter");
        builder.Property(x => x.LabReference)
            .HasColumnName("LabReference")
            .HasMaxLength(80);
        builder.Property(x => x.IronPpm)
            .HasColumnName("IronPpm");
        builder.Property(x => x.CopperPpm)
            .HasColumnName("CopperPpm");
        builder.Property(x => x.SiliconPpm)
            .HasColumnName("SiliconPpm");
        builder.Property(x => x.ViscosityCst)
            .HasColumnName("ViscosityCst");
        builder.Property(x => x.WaterPercent)
            .HasColumnName("WaterPercent");
        builder.Property(x => x.Severity)
            .HasColumnName("Severity")
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(x => x.Recommendation)
            .HasColumnName("Recommendation")
            .HasMaxLength(500);
    }
}
