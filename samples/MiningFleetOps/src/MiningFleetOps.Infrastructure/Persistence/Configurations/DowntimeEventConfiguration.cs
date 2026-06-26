using MiningFleetOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MiningFleetOps.Infrastructure.Persistence.Configurations;

public sealed class DowntimeEventConfiguration : IEntityTypeConfiguration<DowntimeEvent>
{
    public void Configure(EntityTypeBuilder<DowntimeEvent> builder)
    {
        builder.ToTable("DowntimeEvent", "mining");
        builder.HasKey(x => x.DowntimeEventId);
        builder.Property(x => x.DowntimeEventId)
            .HasColumnName("DowntimeEventId")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.EquipmentId)
            .HasColumnName("EquipmentId");
        builder.Property(x => x.WorkOrderId)
            .HasColumnName("WorkOrderId");
        builder.Property(x => x.StartedAt)
            .HasColumnName("StartedAt");
        builder.Property(x => x.EndedAt)
            .HasColumnName("EndedAt");
        builder.Property(x => x.ReasonCategory)
            .HasColumnName("ReasonCategory")
            .HasMaxLength(60)
            .IsRequired();
        builder.Property(x => x.ReasonDetail)
            .HasColumnName("ReasonDetail")
            .HasMaxLength(200);
        builder.Property(x => x.IsPlanned)
            .HasColumnName("IsPlanned");
        builder.Property(x => x.DowntimeHours)
            .HasColumnName("DowntimeHours");
    }
}
