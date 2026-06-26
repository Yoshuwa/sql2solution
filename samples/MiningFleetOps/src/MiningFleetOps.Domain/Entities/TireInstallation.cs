namespace MiningFleetOps.Domain.Entities;

public sealed partial class TireInstallation
{
    public long TireInstallationId { get; set; }

    public int TireId { get; set; }

    public int EquipmentId { get; set; }

    public string PositionCode { get; set; } = string.Empty;

    public DateTime InstalledAt { get; set; }

    public DateTime? RemovedAt { get; set; }

    public decimal InstallHourMeter { get; set; }

    public decimal? RemoveHourMeter { get; set; }

    public string? RemovalReason { get; set; }

}
