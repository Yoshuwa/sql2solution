namespace MiningFleetOps.Application.Dtos;

public sealed partial record CreateMaintenancePlanRequest
(
    int EquipmentClassId,
    string PlanCode,
    string PlanName,
    decimal? IntervalHours,
    int? IntervalDays,
    decimal EstimatedDurationHours,
    bool IsActive
);
