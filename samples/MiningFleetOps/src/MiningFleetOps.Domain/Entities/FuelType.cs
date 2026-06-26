namespace MiningFleetOps.Domain.Entities;

public sealed partial class FuelType
{
    public int FuelTypeId { get; set; }

    public string FuelCode { get; set; } = string.Empty;

    public string FuelName { get; set; } = string.Empty;

    public decimal? EnergyDensityMjPerL { get; set; }

    public decimal? Co2KgPerL { get; set; }

    public bool IsActive { get; set; }

}
