namespace MiningFleetOps.Domain.Entities;

public sealed partial class FluidType
{
    public int FluidTypeId { get; set; }

    public string FluidCode { get; set; } = string.Empty;

    public string FluidName { get; set; } = string.Empty;

    public string FluidCategory { get; set; } = string.Empty;

    public decimal? DefaultIntervalHours { get; set; }

    public bool IsActive { get; set; }

}
