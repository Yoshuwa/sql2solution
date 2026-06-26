using MiningFleetOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MiningFleetOps.Infrastructure.Persistence.Configurations;

public sealed class FluidTypeConfiguration : IEntityTypeConfiguration<FluidType>
{
    public void Configure(EntityTypeBuilder<FluidType> builder)
    {
        builder.ToTable("FluidType", "mining");
        builder.HasKey(x => x.FluidTypeId);
        builder.Property(x => x.FluidTypeId)
            .HasColumnName("FluidTypeId")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.FluidCode)
            .HasColumnName("FluidCode")
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(x => x.FluidName)
            .HasColumnName("FluidName")
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(x => x.FluidCategory)
            .HasColumnName("FluidCategory")
            .HasMaxLength(40)
            .IsRequired();
        builder.Property(x => x.DefaultIntervalHours)
            .HasColumnName("DefaultIntervalHours");
        builder.Property(x => x.IsActive)
            .HasColumnName("IsActive");
    }
}
