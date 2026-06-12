using AdventureWorksLT2017Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdventureWorksLT2017Api.Infrastructure.Persistence.Configurations;

public sealed class CustomerAddressConfiguration : IEntityTypeConfiguration<CustomerAddress>
{
    public void Configure(EntityTypeBuilder<CustomerAddress> builder)
    {
        builder.ToTable("CustomerAddress", "SalesLT");
        builder.HasKey(x => new { x.CustomerID, x.AddressID });
        builder.Property(x => x.CustomerID)
            .HasColumnName("CustomerID")
            .ValueGeneratedNever();
        builder.Property(x => x.AddressID)
            .HasColumnName("AddressID")
            .ValueGeneratedNever();
        builder.Property(x => x.AddressType)
            .HasColumnName("AddressType")
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(x => x.Rowguid)
            .HasColumnName("rowguid");
        builder.Property(x => x.ModifiedDate)
            .HasColumnName("ModifiedDate");
    }
}
