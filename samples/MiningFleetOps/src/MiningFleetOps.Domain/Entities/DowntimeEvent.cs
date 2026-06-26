namespace MiningFleetOps.Domain.Entities;

public sealed partial class DowntimeEvent
{
    public long DowntimeEventId { get; set; }

    public int EquipmentId { get; set; }

    public long? WorkOrderId { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public string ReasonCategory { get; set; } = string.Empty;

    public string? ReasonDetail { get; set; }

    public bool IsPlanned { get; set; }

    public decimal? DowntimeHours { get; set; }

}
