namespace MiningFleetOps.Application.Dtos;

public sealed partial record CreateWorkOrderPartRequest
(
    long WorkOrderId,
    int PartId,
    decimal QuantityUsed,
    decimal UnitCost,
    decimal? LineCost
);
