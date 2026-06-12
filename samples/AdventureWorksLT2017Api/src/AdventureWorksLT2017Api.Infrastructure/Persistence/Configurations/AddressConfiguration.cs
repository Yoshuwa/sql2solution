using AdventureWorksLT2017Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdventureWorksLT2017Api.Infrastructure.Persistence.Configurations;

public sealed class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.ToTable("Address", "SalesLT");
        builder.HasKey(x => x.AddressID);
        builder.Property(x => x.AddressID)
            .HasColumnName("AddressID")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.AddressLine1)
            .HasColumnName("AddressLine1")
            .HasMaxLength(60)
            .IsRequired();
        builder.Property(x => x.AddressLine2)
            .HasColumnName("AddressLine2")
            .HasMaxLength(60);
        builder.Property(x => x.City)
            .HasColumnName("City")
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(x => x.StateProvince)
            .HasColumnName("StateProvince")
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(x => x.CountryRegion)
            .HasColumnName("CountryRegion")
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(x => x.PostalCode)
            .HasColumnName("PostalCode")
            .HasMaxLength(15)
            .IsRequired();
        builder.Property(x => x.Rowguid)
            .HasColumnName("rowguid");
        builder.Property(x => x.ModifiedDate)
            .HasColumnName("ModifiedDate");
    }
}
