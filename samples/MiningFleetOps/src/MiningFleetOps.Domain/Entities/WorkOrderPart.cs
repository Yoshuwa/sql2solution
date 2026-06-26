namespace MiningFleetOps.Domain.Entities;

public sealed partial class WorkOrderPart
{
    public long WorkOrderPartId { get; set; }

    public long WorkOrderId { get; set; }

    public int PartId { get; set; }

    public decimal QuantityUsed { get; set; }

    public decimal UnitCost { get; set; }

    public decimal? LineCost { get; set; }

}
