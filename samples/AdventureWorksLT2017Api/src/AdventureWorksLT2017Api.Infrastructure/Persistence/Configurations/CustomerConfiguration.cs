using AdventureWorksLT2017Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdventureWorksLT2017Api.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customer", "SalesLT");
        builder.HasKey(x => x.CustomerID);
        builder.Property(x => x.CustomerID)
            .HasColumnName("CustomerID")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.NameStyle)
            .HasColumnName("NameStyle");
        builder.Property(x => x.Title)
            .HasColumnName("Title")
            .HasMaxLength(8);
        builder.Property(x => x.FirstName)
            .HasColumnName("FirstName")
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(x => x.MiddleName)
            .HasColumnName("MiddleName")
            .HasMaxLength(50);
        builder.Property(x => x.LastName)
            .HasColumnName("LastName")
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(x => x.Suffix)
            .HasColumnName("Suffix")
            .HasMaxLength(10);
        builder.Property(x => x.CompanyName)
            .HasColumnName("CompanyName")
            .HasMaxLength(128);
        builder.Property(x => x.SalesPerson)
            .HasColumnName("SalesPerson")
            .HasMaxLength(256);
        builder.Property(x => x.EmailAddress)
            .HasColumnName("EmailAddress")
            .HasMaxLength(50);
        builder.Property(x => x.Phone)
            .HasColumnName("Phone")
            .HasMaxLength(25);
        builder.Property(x => x.PasswordHash)
            .HasColumnName("PasswordHash")
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(x => x.PasswordSalt)
            .HasColumnName("PasswordSalt")
            .HasMaxLength(10)
            .IsRequired();
        builder.Property(x => x.Rowguid)
            .HasColumnName("rowguid");
        builder.Property(x => x.ModifiedDate)
            .HasColumnName("ModifiedDate");
    }
}
