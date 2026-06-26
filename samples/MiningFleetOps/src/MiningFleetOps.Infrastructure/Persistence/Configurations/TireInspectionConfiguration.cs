using MiningFleetOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MiningFleetOps.Infrastructure.Persistence.Configurations;

public sealed class TireInspectionConfiguration : IEntityTypeConfiguration<TireInspection>
{
    public void Configure(EntityTypeBuilder<TireInspection> builder)
    {
        builder.ToTable("TireInspection", "mining");
        builder.HasKey(x => x.TireInspectionId);
        builder.Property(x => x.TireInspectionId)
            .HasColumnName("TireInspectionId")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.TireInstallationId)
            .HasColumnName("TireInstallationId");
        builder.Property(x => x.InspectedAt)
            .HasColumnName("InspectedAt");
        builder.Property(x => x.HourMeter)
            .HasColumnName("HourMeter");
        builder.Property(x => x.TreadDepthMm)
            .HasColumnName("TreadDepthMm");
        builder.Property(x => x.PressureKpa)
            .HasColumnName("PressureKpa");
        builder.Property(x => x.TemperatureC)
            .HasColumnName("TemperatureC");
        builder.Property(x => x.ConditionRating)
            .HasColumnName("ConditionRating")
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(x => x.Notes)
            .HasColumnName("Notes")
            .HasMaxLength(500);
    }
}
