using MiningFleetOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MiningFleetOps.Infrastructure.Persistence.Configurations;

public sealed class PitConfiguration : IEntityTypeConfiguration<Pit>
{
    public void Configure(EntityTypeBuilder<Pit> builder)
    {
        builder.ToTable("Pit", "mining");
        builder.HasKey(x => x.PitId);
        builder.Property(x => x.PitId)
            .HasColumnName("PitId")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.SiteId)
            .HasColumnName("SiteId");
        builder.Property(x => x.PitCode)
            .HasColumnName("PitCode")
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(x => x.PitName)
            .HasColumnName("PitName")
            .HasMaxLength(160)
            .IsRequired();
        builder.Property(x => x.BenchElevationM)
            .HasColumnName("BenchElevationM");
        builder.Property(x => x.IsActive)
            .HasColumnName("IsActive");
    }
}
