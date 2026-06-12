using AdventureWorksLT2017Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdventureWorksLT2017Api.Infrastructure.Persistence.Configurations;

public sealed class ProductDescriptionConfiguration : IEntityTypeConfiguration<ProductDescription>
{
    public void Configure(EntityTypeBuilder<ProductDescription> builder)
    {
        builder.ToTable("ProductDescription", "SalesLT");
        builder.HasKey(x => x.ProductDescriptionID);
        builder.Property(x => x.ProductDescriptionID)
            .HasColumnName("ProductDescriptionID")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.Description)
            .HasColumnName("Description")
            .HasMaxLength(400)
            .IsRequired();
        builder.Property(x => x.Rowguid)
            .HasColumnName("rowguid");
        builder.Property(x => x.ModifiedDate)
            .HasColumnName("ModifiedDate");
    }
}
