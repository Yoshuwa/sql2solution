namespace MiningFleetOps.Application.Dtos;

public sealed partial record WorkOrderPartDto
(
    long WorkOrderPartId,
    long WorkOrderId,
    int PartId,
    decimal QuantityUsed,
    decimal UnitCost,
    decimal? LineCost
);
