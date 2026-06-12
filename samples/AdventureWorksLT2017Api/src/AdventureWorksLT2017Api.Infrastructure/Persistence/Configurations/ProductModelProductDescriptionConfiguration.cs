using AdventureWorksLT2017Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdventureWorksLT2017Api.Infrastructure.Persistence.Configurations;

public sealed class ProductModelProductDescriptionConfiguration : IEntityTypeConfiguration<ProductModelProductDescription>
{
    public void Configure(EntityTypeBuilder<ProductModelProductDescription> builder)
    {
        builder.ToTable("ProductModelProductDescription", "SalesLT");
        builder.HasKey(x => new { x.ProductModelID, x.ProductDescriptionID, x.Culture });
        builder.Property(x => x.ProductModelID)
            .HasColumnName("ProductModelID")
            .ValueGeneratedNever();
        builder.Property(x => x.ProductDescriptionID)
            .HasColumnName("ProductDescriptionID")
            .ValueGeneratedNever();
        builder.Property(x => x.Culture)
            .HasColumnName("Culture")
            .ValueGeneratedNever()
            .HasMaxLength(6)
            .IsRequired();
        builder.Property(x => x.Rowguid)
            .HasColumnName("rowguid");
        builder.Property(x => x.ModifiedDate)
            .HasColumnName("ModifiedDate");
    }
}
