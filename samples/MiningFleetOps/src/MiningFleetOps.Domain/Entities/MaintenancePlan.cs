namespace MiningFleetOps.Domain.Entities;

public sealed partial class MaintenancePlan
{
    public int MaintenancePlanId { get; set; }

    public int EquipmentClassId { get; set; }

    public string PlanCode { get; set; } = string.Empty;

    public string PlanName { get; set; } = string.Empty;

    public decimal? IntervalHours { get; set; }

    public int? IntervalDays { get; set; }

    public decimal EstimatedDurationHours { get; set; }

    public bool IsActive { get; set; }

}
