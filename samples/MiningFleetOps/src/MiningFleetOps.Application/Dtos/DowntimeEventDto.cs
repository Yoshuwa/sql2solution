namespace MiningFleetOps.Application.Dtos;

public sealed partial record DowntimeEventDto
(
    long DowntimeEventId,
    int EquipmentId,
    long? WorkOrderId,
    DateTime StartedAt,
    DateTime? EndedAt,
    string ReasonCategory,
    string? ReasonDetail,
    bool IsPlanned,
    decimal? DowntimeHours
);
