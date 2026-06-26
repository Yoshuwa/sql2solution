namespace MiningFleetOps.Application.Dtos;

public sealed partial record UpdateWorkOrderTaskRequest
(
    long WorkOrderId,
    int TaskSequence,
    string TaskDescription,
    bool IsCompleted,
    DateTime? CompletedAt,
    int? CompletedByEmployeeId
);
