using AdventureWorksLT2017Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdventureWorksLT2017Api.Infrastructure.Persistence.Configurations;

public sealed class SalesOrderHeaderConfiguration : IEntityTypeConfiguration<SalesOrderHeader>
{
    public void Configure(EntityTypeBuilder<SalesOrderHeader> builder)
    {
        builder.ToTable("SalesOrderHeader", "SalesLT");
        builder.HasKey(x => x.SalesOrderID);
        builder.Property(x => x.SalesOrderID)
            .HasColumnName("SalesOrderID")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.RevisionNumber)
            .HasColumnName("RevisionNumber");
        builder.Property(x => x.OrderDate)
            .HasColumnName("OrderDate");
        builder.Property(x => x.DueDate)
            .HasColumnName("DueDate");
        builder.Property(x => x.ShipDate)
            .HasColumnName("ShipDate");
        builder.Property(x => x.Status)
            .HasColumnName("Status");
        builder.Property(x => x.OnlineOrderFlag)
            .HasColumnName("OnlineOrderFlag");
        builder.Property(x => x.SalesOrderNumber)
            .HasColumnName("SalesOrderNumber")
            .HasMaxLength(25)
            .IsRequired();
        builder.Property(x => x.PurchaseOrderNumber)
            .HasColumnName("PurchaseOrderNumber")
            .HasMaxLength(25);
        builder.Property(x => x.AccountNumber)
            .HasColumnName("AccountNumber")
            .HasMaxLength(15);
        builder.Property(x => x.CustomerID)
            .HasColumnName("CustomerID");
        builder.Property(x => x.ShipToAddressID)
            .HasColumnName("ShipToAddressID");
        builder.Property(x => x.BillToAddressID)
            .HasColumnName("BillToAddressID");
        builder.Property(x => x.ShipMethod)
            .HasColumnName("ShipMethod")
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(x => x.CreditCardApprovalCode)
            .HasColumnName("CreditCardApprovalCode")
            .HasMaxLength(15);
        builder.Property(x => x.SubTotal)
            .HasColumnName("SubTotal");
        builder.Property(x => x.TaxAmt)
            .HasColumnName("TaxAmt");
        builder.Property(x => x.Freight)
            .HasColumnName("Freight");
        builder.Property(x => x.TotalDue)
            .HasColumnName("TotalDue");
        builder.Property(x => x.Comment)
            .HasColumnName("Comment");
        builder.Property(x => x.Rowguid)
            .HasColumnName("rowguid");
        builder.Property(x => x.ModifiedDate)
            .HasColumnName("ModifiedDate");
    }
}
