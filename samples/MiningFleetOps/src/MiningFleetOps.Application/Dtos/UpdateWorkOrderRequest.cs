namespace MiningFleetOps.Application.Dtos;

public sealed partial record UpdateWorkOrderRequest
(
    string WorkOrderNumber,
    int EquipmentId,
    int? MaintenancePlanId,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    string PriorityName,
    string WorkOrderType,
    string Status,
    decimal OpenHourMeter,
    decimal? CloseHourMeter,
    string ProblemDescription,
    string? CorrectiveAction,
    decimal LaborHours,
    decimal? EstimatedCost,
    decimal? ActualCost,
    int? CreatedByEmployeeId,
    int? ClosedByEmployeeId,
    decimal? DowntimeHours
);
