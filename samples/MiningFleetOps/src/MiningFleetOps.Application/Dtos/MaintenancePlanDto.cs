namespace MiningFleetOps.Application.Dtos;

public sealed partial record MaintenancePlanDto
(
    int MaintenancePlanId,
    int EquipmentClassId,
    string PlanCode,
    string PlanName,
    decimal? IntervalHours,
    int? IntervalDays,
    decimal EstimatedDurationHours,
    bool IsActive
);
