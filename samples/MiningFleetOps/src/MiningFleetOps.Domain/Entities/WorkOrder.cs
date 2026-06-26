namespace MiningFleetOps.Domain.Entities;

public sealed partial class WorkOrder
{
    public long WorkOrderId { get; set; }

    public string WorkOrderNumber { get; set; } = string.Empty;

    public int EquipmentId { get; set; }

    public int? MaintenancePlanId { get; set; }

    public DateTime OpenedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public string PriorityName { get; set; } = string.Empty;

    public string WorkOrderType { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public decimal OpenHourMeter { get; set; }

    public decimal? CloseHourMeter { get; set; }

    public string ProblemDescription { get; set; } = string.Empty;

    public string? CorrectiveAction { get; set; }

    public decimal LaborHours { get; set; }

    public decimal? EstimatedCost { get; set; }

    public decimal? ActualCost { get; set; }

    public int? CreatedByEmployeeId { get; set; }

    public int? ClosedByEmployeeId { get; set; }

    public decimal? DowntimeHours { get; set; }

}
