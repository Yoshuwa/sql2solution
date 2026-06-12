using AdventureWorksLT2017Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdventureWorksLT2017Api.Infrastructure.Persistence.Configurations;

public sealed class ProductModelConfiguration : IEntityTypeConfiguration<ProductModel>
{
    public void Configure(EntityTypeBuilder<ProductModel> builder)
    {
        builder.ToTable("ProductModel", "SalesLT");
        builder.HasKey(x => x.ProductModelID);
        builder.Property(x => x.ProductModelID)
            .HasColumnName("ProductModelID")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.Name)
            .HasColumnName("Name")
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(x => x.CatalogDescription)
            .HasColumnName("CatalogDescription");
        builder.Property(x => x.Rowguid)
            .HasColumnName("rowguid");
        builder.Property(x => x.ModifiedDate)
            .HasColumnName("ModifiedDate");
    }
}
