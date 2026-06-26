namespace MiningFleetOps.Application.Dtos;

public sealed partial record CreateDowntimeEventRequest
(
    int EquipmentId,
    long? WorkOrderId,
    DateTime StartedAt,
    DateTime? EndedAt,
    string ReasonCategory,
    string? ReasonDetail,
    bool IsPlanned,
    decimal? DowntimeHours
);
