using MiningFleetOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MiningFleetOps.Infrastructure.Persistence.Configurations;

public sealed class TireInstallationConfiguration : IEntityTypeConfiguration<TireInstallation>
{
    public void Configure(EntityTypeBuilder<TireInstallation> builder)
    {
        builder.ToTable("TireInstallation", "mining");
        builder.HasKey(x => x.TireInstallationId);
        builder.Property(x => x.TireInstallationId)
            .HasColumnName("TireInstallationId")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.TireId)
            .HasColumnName("TireId");
        builder.Property(x => x.EquipmentId)
            .HasColumnName("EquipmentId");
        builder.Property(x => x.PositionCode)
            .HasColumnName("PositionCode")
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(x => x.InstalledAt)
            .HasColumnName("InstalledAt");
        builder.Property(x => x.RemovedAt)
            .HasColumnName("RemovedAt");
        builder.Property(x => x.InstallHourMeter)
            .HasColumnName("InstallHourMeter");
        builder.Property(x => x.RemoveHourMeter)
            .HasColumnName("RemoveHourMeter");
        builder.Property(x => x.RemovalReason)
            .HasColumnName("RemovalReason")
            .HasMaxLength(200);
    }
}
