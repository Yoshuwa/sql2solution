using MiningFleetOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MiningFleetOps.Infrastructure.Persistence.Configurations;

public sealed class WorkOrderPartConfiguration : IEntityTypeConfiguration<WorkOrderPart>
{
    public void Configure(EntityTypeBuilder<WorkOrderPart> builder)
    {
        builder.ToTable("WorkOrderPart", "mining");
        builder.HasKey(x => x.WorkOrderPartId);
        builder.Property(x => x.WorkOrderPartId)
            .HasColumnName("WorkOrderPartId")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.WorkOrderId)
            .HasColumnName("WorkOrderId");
        builder.Property(x => x.PartId)
            .HasColumnName("PartId");
        builder.Property(x => x.QuantityUsed)
            .HasColumnName("QuantityUsed");
        builder.Property(x => x.UnitCost)
            .HasColumnName("UnitCost");
        builder.Property(x => x.LineCost)
            .HasColumnName("LineCost");
    }
}
