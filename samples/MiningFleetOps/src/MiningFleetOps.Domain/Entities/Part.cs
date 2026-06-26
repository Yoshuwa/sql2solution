namespace MiningFleetOps.Domain.Entities;

public sealed partial class Part
{
    public int PartId { get; set; }

    public string PartNumber { get; set; } = string.Empty;

    public string PartName { get; set; } = string.Empty;

    public string? PartCategory { get; set; }

    public string UnitOfMeasure { get; set; } = string.Empty;

    public decimal? StandardCost { get; set; }

    public decimal ReorderPoint { get; set; }

    public decimal OnHandQuantity { get; set; }

}
