namespace MiningFleetOps.Domain.Entities;

public sealed partial class FluidSample
{
    public long FluidSampleId { get; set; }

    public int EquipmentId { get; set; }

    public int FluidTypeId { get; set; }

    public DateTime SampledAt { get; set; }

    public decimal HourMeter { get; set; }

    public string? LabReference { get; set; }

    public decimal? IronPpm { get; set; }

    public decimal? CopperPpm { get; set; }

    public decimal? SiliconPpm { get; set; }

    public decimal? ViscosityCst { get; set; }

    public decimal? WaterPercent { get; set; }

    public string Severity { get; set; } = string.Empty;

    public string? Recommendation { get; set; }

}
