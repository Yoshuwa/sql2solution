namespace MiningFleetOps.Application.Dtos;

public sealed partial record UpdateWorkOrderPartRequest
(
    long WorkOrderId,
    int PartId,
    decimal QuantityUsed,
    decimal UnitCost,
    decimal? LineCost
);
