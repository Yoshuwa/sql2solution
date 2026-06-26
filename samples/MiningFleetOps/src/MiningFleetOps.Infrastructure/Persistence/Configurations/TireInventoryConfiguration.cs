using MiningFleetOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MiningFleetOps.Infrastructure.Persistence.Configurations;

public sealed class TireInventoryConfiguration : IEntityTypeConfiguration<TireInventory>
{
    public void Configure(EntityTypeBuilder<TireInventory> builder)
    {
        builder.ToTable("TireInventory", "mining");
        builder.HasKey(x => x.TireId);
        builder.Property(x => x.TireId)
            .HasColumnName("TireId")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.TireSerialNumber)
            .HasColumnName("TireSerialNumber")
            .HasMaxLength(80)
            .IsRequired();
        builder.Property(x => x.Manufacturer)
            .HasColumnName("Manufacturer")
            .HasMaxLength(80);
        builder.Property(x => x.TireSize)
            .HasColumnName("TireSize")
            .HasMaxLength(40)
            .IsRequired();
        builder.Property(x => x.TireType)
            .HasColumnName("TireType")
            .HasMaxLength(40)
            .IsRequired();
        builder.Property(x => x.PurchaseDate)
            .HasColumnName("PurchaseDate");
        builder.Property(x => x.PurchaseCost)
            .HasColumnName("PurchaseCost");
        builder.Property(x => x.OriginalTreadDepthMm)
            .HasColumnName("OriginalTreadDepthMm");
        builder.Property(x => x.Status)
            .HasColumnName("Status")
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(x => x.CreatedAt)
            .HasColumnName("CreatedAt");
    }
}
