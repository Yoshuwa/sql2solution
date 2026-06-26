namespace MiningFleetOps.Domain.Entities;

public sealed partial class Pit
{
    public int PitId { get; set; }

    public int SiteId { get; set; }

    public string PitCode { get; set; } = string.Empty;

    public string PitName { get; set; } = string.Empty;

    public decimal? BenchElevationM { get; set; }

    public bool IsActive { get; set; }

}
