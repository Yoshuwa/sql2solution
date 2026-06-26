using MiningFleetOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MiningFleetOps.Infrastructure.Persistence.Configurations;

public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employee", "mining");
        builder.HasKey(x => x.EmployeeId);
        builder.Property(x => x.EmployeeId)
            .HasColumnName("EmployeeId")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.SiteId)
            .HasColumnName("SiteId");
        builder.Property(x => x.EmployeeCode)
            .HasColumnName("EmployeeCode")
            .HasMaxLength(40)
            .IsRequired();
        builder.Property(x => x.FullName)
            .HasColumnName("FullName")
            .HasMaxLength(160)
            .IsRequired();
        builder.Property(x => x.RoleName)
            .HasColumnName("RoleName")
            .HasMaxLength(80)
            .IsRequired();
        builder.Property(x => x.LicenseClass)
            .HasColumnName("LicenseClass")
            .HasMaxLength(40);
        builder.Property(x => x.Phone)
            .HasColumnName("Phone")
            .HasMaxLength(40);
        builder.Property(x => x.Email)
            .HasColumnName("Email")
            .HasMaxLength(254);
        builder.Property(x => x.IsActive)
            .HasColumnName("IsActive");
        builder.Property(x => x.CreatedAt)
            .HasColumnName("CreatedAt");
    }
}
