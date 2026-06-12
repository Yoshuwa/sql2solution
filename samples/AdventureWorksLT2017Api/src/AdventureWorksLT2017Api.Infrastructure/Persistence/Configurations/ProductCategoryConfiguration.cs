using AdventureWorksLT2017Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdventureWorksLT2017Api.Infrastructure.Persistence.Configurations;

public sealed class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
{
    public void Configure(EntityTypeBuilder<ProductCategory> builder)
    {
        builder.ToTable("ProductCategory", "SalesLT");
        builder.HasKey(x => x.ProductCategoryID);
        builder.Property(x => x.ProductCategoryID)
            .HasColumnName("ProductCategoryID")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.ParentProductCategoryID)
            .HasColumnName("ParentProductCategoryID");
        builder.Property(x => x.Name)
            .HasColumnName("Name")
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(x => x.Rowguid)
            .HasColumnName("rowguid");
        builder.Property(x => x.ModifiedDate)
            .HasColumnName("ModifiedDate");
    }
}
