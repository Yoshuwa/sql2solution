using MiningFleetOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MiningFleetOps.Infrastructure.Persistence.Configurations;

public sealed class FuelTypeConfiguration : IEntityTypeConfiguration<FuelType>
{
    public void Configure(EntityTypeBuilder<FuelType> builder)
    {
        builder.ToTable("FuelType", "mining");
        builder.HasKey(x => x.FuelTypeId);
        builder.Property(x => x.FuelTypeId)
            .HasColumnName("FuelTypeId")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.FuelCode)
            .HasColumnName("FuelCode")
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(x => x.FuelName)
            .HasColumnName("FuelName")
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(x => x.EnergyDensityMjPerL)
            .HasColumnName("EnergyDensityMjPerL");
        builder.Property(x => x.Co2KgPerL)
            .HasColumnName("Co2KgPerL");
        builder.Property(x => x.IsActive)
            .HasColumnName("IsActive");
    }
}
