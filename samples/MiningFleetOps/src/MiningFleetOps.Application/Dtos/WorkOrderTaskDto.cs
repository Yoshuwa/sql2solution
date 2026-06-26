namespace MiningFleetOps.Application.Dtos;

public sealed partial record WorkOrderTaskDto
(
    long WorkOrderTaskId,
    long WorkOrderId,
    int TaskSequence,
    string TaskDescription,
    bool IsCompleted,
    DateTime? CompletedAt,
    int? CompletedByEmployeeId
);
