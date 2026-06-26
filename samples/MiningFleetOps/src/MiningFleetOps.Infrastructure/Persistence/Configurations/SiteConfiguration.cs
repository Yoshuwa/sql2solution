using MiningFleetOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MiningFleetOps.Infrastructure.Persistence.Configurations;

public sealed class SiteConfiguration : IEntityTypeConfiguration<Site>
{
    public void Configure(EntityTypeBuilder<Site> builder)
    {
        builder.ToTable("Site", "mining");
        builder.HasKey(x => x.SiteId);
        builder.Property(x => x.SiteId)
            .HasColumnName("SiteId")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.SiteCode)
            .HasColumnName("SiteCode")
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(x => x.SiteName)
            .HasColumnName("SiteName")
            .HasMaxLength(160)
            .IsRequired();
        builder.Property(x => x.Country)
            .HasColumnName("Country")
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(x => x.Region)
            .HasColumnName("Region")
            .HasMaxLength(100);
        builder.Property(x => x.TimeZoneName)
            .HasColumnName("TimeZoneName")
            .HasMaxLength(80)
            .IsRequired();
        builder.Property(x => x.IsActive)
            .HasColumnName("IsActive");
        builder.Property(x => x.CreatedAt)
            .HasColumnName("CreatedAt");
    }
}
