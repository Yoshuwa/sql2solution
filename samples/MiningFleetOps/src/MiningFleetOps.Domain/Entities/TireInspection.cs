namespace MiningFleetOps.Domain.Entities;

public sealed partial class TireInspection
{
    public long TireInspectionId { get; set; }

    public long TireInstallationId { get; set; }

    public DateTime InspectedAt { get; set; }

    public decimal HourMeter { get; set; }

    public decimal TreadDepthMm { get; set; }

    public decimal? PressureKpa { get; set; }

    public decimal? TemperatureC { get; set; }

    public string ConditionRating { get; set; } = string.Empty;

    public string? Notes { get; set; }

}
