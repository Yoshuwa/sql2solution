namespace MiningFleetOps.Domain.Entities;

public sealed partial class Material
{
    public int MaterialId { get; set; }

    public string MaterialCode { get; set; } = string.Empty;

    public string MaterialName { get; set; } = string.Empty;

    public decimal? DensityTonnesPerM3 { get; set; }

    public bool IsOre { get; set; }

}
