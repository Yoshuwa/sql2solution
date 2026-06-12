using AdventureWorksLT2017Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdventureWorksLT2017Api.Infrastructure.Persistence.Configurations;

public sealed class SalesOrderDetailConfiguration : IEntityTypeConfiguration<SalesOrderDetail>
{
    public void Configure(EntityTypeBuilder<SalesOrderDetail> builder)
    {
        builder.ToTable("SalesOrderDetail", "SalesLT");
        builder.HasKey(x => new { x.SalesOrderID, x.SalesOrderDetailID });
        builder.Property(x => x.SalesOrderID)
            .HasColumnName("SalesOrderID")
            .ValueGeneratedNever();
        builder.Property(x => x.SalesOrderDetailID)
            .HasColumnName("SalesOrderDetailID")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.OrderQty)
            .HasColumnName("OrderQty");
        builder.Property(x => x.ProductID)
            .HasColumnName("ProductID");
        builder.Property(x => x.UnitPrice)
            .HasColumnName("UnitPrice");
        builder.Property(x => x.UnitPriceDiscount)
            .HasColumnName("UnitPriceDiscount");
        builder.Property(x => x.LineTotal)
            .HasColumnName("LineTotal");
        builder.Property(x => x.Rowguid)
            .HasColumnName("rowguid");
        builder.Property(x => x.ModifiedDate)
            .HasColumnName("ModifiedDate");
    }
}
