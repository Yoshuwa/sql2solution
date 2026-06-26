namespace MiningFleetOps.Domain.Entities;

public sealed partial class TireInventory
{
    public int TireId { get; set; }

    public string TireSerialNumber { get; set; } = string.Empty;

    public string? Manufacturer { get; set; }

    public string TireSize { get; set; } = string.Empty;

    public string TireType { get; set; } = string.Empty;

    public DateTime? PurchaseDate { get; set; }

    public decimal? PurchaseCost { get; set; }

    public decimal OriginalTreadDepthMm { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

}
