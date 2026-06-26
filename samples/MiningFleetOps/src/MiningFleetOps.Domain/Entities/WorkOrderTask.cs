namespace MiningFleetOps.Domain.Entities;

public sealed partial class WorkOrderTask
{
    public long WorkOrderTaskId { get; set; }

    public long WorkOrderId { get; set; }

    public int TaskSequence { get; set; }

    public string TaskDescription { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }

    public DateTime? CompletedAt { get; set; }

    public int? CompletedByEmployeeId { get; set; }

}
