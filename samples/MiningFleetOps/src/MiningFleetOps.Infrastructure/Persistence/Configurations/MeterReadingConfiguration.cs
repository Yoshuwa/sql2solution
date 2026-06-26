using MiningFleetOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MiningFleetOps.Infrastructure.Persistence.Configurations;

public sealed class MeterReadingConfiguration : IEntityTypeConfiguration<MeterReading>
{
    public void Configure(EntityTypeBuilder<MeterReading> builder)
    {
        builder.ToTable("MeterReading", "mining");
        builder.HasKey(x => x.MeterReadingId);
        builder.Property(x => x.MeterReadingId)
            .HasColumnName("MeterReadingId")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.EquipmentId)
            .HasColumnName("EquipmentId");
        builder.Property(x => x.ReadingAt)
            .HasColumnName("ReadingAt");
        builder.Property(x => x.HourMeter)
            .HasColumnName("HourMeter");
        builder.Property(x => x.OdometerKm)
            .HasColumnName("OdometerKm");
        builder.Property(x => x.SourceName)
            .HasColumnName("SourceName")
            .HasMaxLength(40)
            .IsRequired();
        builder.Property(x => x.RecordedByEmployeeId)
            .HasColumnName("RecordedByEmployeeId");
        builder.Property(x => x.Notes)
            .HasColumnName("Notes")
            .HasMaxLength(500);
        builder.Property(x => x.CreatedAt)
            .HasColumnName("CreatedAt");
    }
}
