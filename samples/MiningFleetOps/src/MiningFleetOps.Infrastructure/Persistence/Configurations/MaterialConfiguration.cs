using MiningFleetOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MiningFleetOps.Infrastructure.Persistence.Configurations;

public sealed class MaterialConfiguration : IEntityTypeConfiguration<Material>
{
    public void Configure(EntityTypeBuilder<Material> builder)
    {
        builder.ToTable("Material", "mining");
        builder.HasKey(x => x.MaterialId);
        builder.Property(x => x.MaterialId)
            .HasColumnName("MaterialId")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.MaterialCode)
            .HasColumnName("MaterialCode")
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(x => x.MaterialName)
            .HasColumnName("MaterialName")
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(x => x.DensityTonnesPerM3)
            .HasColumnName("DensityTonnesPerM3");
        builder.Property(x => x.IsOre)
            .HasColumnName("IsOre");
    }
}
