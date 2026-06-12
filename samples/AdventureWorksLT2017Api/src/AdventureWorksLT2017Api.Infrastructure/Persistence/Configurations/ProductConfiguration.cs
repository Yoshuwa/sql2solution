using AdventureWorksLT2017Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdventureWorksLT2017Api.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Product", "SalesLT");
        builder.HasKey(x => x.ProductID);
        builder.Property(x => x.ProductID)
            .HasColumnName("ProductID")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.Name)
            .HasColumnName("Name")
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(x => x.ProductNumber)
            .HasColumnName("ProductNumber")
            .HasMaxLength(25)
            .IsRequired();
        builder.Property(x => x.Color)
            .HasColumnName("Color")
            .HasMaxLength(15);
        builder.Property(x => x.StandardCost)
            .HasColumnName("StandardCost");
        builder.Property(x => x.ListPrice)
            .HasColumnName("ListPrice");
        builder.Property(x => x.Size)
            .HasColumnName("Size")
            .HasMaxLength(5);
        builder.Property(x => x.Weight)
            .HasColumnName("Weight");
        builder.Property(x => x.ProductCategoryID)
            .HasColumnName("ProductCategoryID");
        builder.Property(x => x.ProductModelID)
            .HasColumnName("ProductModelID");
        builder.Property(x => x.SellStartDate)
            .HasColumnName("SellStartDate");
        builder.Property(x => x.SellEndDate)
            .HasColumnName("SellEndDate");
        builder.Property(x => x.DiscontinuedDate)
            .HasColumnName("DiscontinuedDate");
        builder.Property(x => x.ThumbNailPhoto)
            .HasColumnName("ThumbNailPhoto");
        builder.Property(x => x.ThumbnailPhotoFileName)
            .HasColumnName("ThumbnailPhotoFileName")
            .HasMaxLength(50);
        builder.Property(x => x.Rowguid)
            .HasColumnName("rowguid");
        builder.Property(x => x.ModifiedDate)
            .HasColumnName("ModifiedDate");
    }
}
