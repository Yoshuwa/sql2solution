using MiningFleetOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MiningFleetOps.Infrastructure.Persistence.Configurations;

public sealed class PartConfiguration : IEntityTypeConfiguration<Part>
{
    public void Configure(EntityTypeBuilder<Part> builder)
    {
        builder.ToTable("Part", "mining");
        builder.HasKey(x => x.PartId);
        builder.Property(x => x.PartId)
            .HasColumnName("PartId")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.PartNumber)
            .HasColumnName("PartNumber")
            .HasMaxLength(80)
            .IsRequired();
        builder.Property(x => x.PartName)
            .HasColumnName("PartName")
            .HasMaxLength(160)
            .IsRequired();
        builder.Property(x => x.PartCategory)
            .HasColumnName("PartCategory")
            .HasMaxLength(80);
        builder.Property(x => x.UnitOfMeasure)
            .HasColumnName("UnitOfMeasure")
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(x => x.StandardCost)
            .HasColumnName("StandardCost");
        builder.Property(x => x.ReorderPoint)
            .HasColumnName("ReorderPoint");
        builder.Property(x => x.OnHandQuantity)
            .HasColumnName("OnHandQuantity");
    }
}
