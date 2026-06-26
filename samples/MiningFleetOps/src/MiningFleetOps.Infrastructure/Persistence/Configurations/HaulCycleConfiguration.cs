using MiningFleetOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MiningFleetOps.Infrastructure.Persistence.Configurations;

public sealed class HaulCycleConfiguration : IEntityTypeConfiguration<HaulCycle>
{
    public void Configure(EntityTypeBuilder<HaulCycle> builder)
    {
        builder.ToTable("HaulCycle", "mining");
        builder.HasKey(x => x.HaulCycleId);
        builder.Property(x => x.HaulCycleId)
            .HasColumnName("HaulCycleId")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.EquipmentId)
            .HasColumnName("EquipmentId");
        builder.Property(x => x.OperatorEmployeeId)
            .HasColumnName("OperatorEmployeeId");
        builder.Property(x => x.ShiftId)
            .HasColumnName("ShiftId");
        builder.Property(x => x.PitId)
            .HasColumnName("PitId");
        builder.Property(x => x.MaterialId)
            .HasColumnName("MaterialId");
        builder.Property(x => x.CycleStartedAt)
            .HasColumnName("CycleStartedAt");
        builder.Property(x => x.CycleEndedAt)
            .HasColumnName("CycleEndedAt");
        builder.Property(x => x.LoadedTonnes)
            .HasColumnName("LoadedTonnes");
        builder.Property(x => x.DistanceKm)
            .HasColumnName("DistanceKm");
        builder.Property(x => x.FuelLitersEstimated)
            .HasColumnName("FuelLitersEstimated");
        builder.Property(x => x.TonnesPerHour)
            .HasColumnName("TonnesPerHour");
        builder.Property(x => x.CycleMinutes)
            .HasColumnName("CycleMinutes");
        builder.Property(x => x.TonnesKm)
            .HasColumnName("TonnesKm");
    }
}
