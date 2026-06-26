using MiningFleetOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MiningFleetOps.Infrastructure.Persistence.Configurations;

public sealed class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.ToTable("Shift", "mining");
        builder.HasKey(x => x.ShiftId);
        builder.Property(x => x.ShiftId)
            .HasColumnName("ShiftId")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.SiteId)
            .HasColumnName("SiteId");
        builder.Property(x => x.ShiftCode)
            .HasColumnName("ShiftCode")
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(x => x.ShiftName)
            .HasColumnName("ShiftName")
            .HasMaxLength(80)
            .IsRequired();
        builder.Property(x => x.StartTime)
            .HasColumnName("StartTime");
        builder.Property(x => x.EndTime)
            .HasColumnName("EndTime");
        builder.Property(x => x.PlannedHours)
            .HasColumnName("PlannedHours");
    }
}
