namespace MiningFleetOps.Domain.Entities;

public sealed partial class Equipment
{
    public int EquipmentId { get; set; }

    public int SiteId { get; set; }

    public int EquipmentClassId { get; set; }

    public string AssetTag { get; set; } = string.Empty;

    public string? SerialNumber { get; set; }

    public string? Manufacturer { get; set; }

    public string? Model { get; set; }

    public DateTime? CommissionDate { get; set; }

    public int FuelTypeId { get; set; }

    public decimal? TankCapacityL { get; set; }

    public decimal CurrentHourMeter { get; set; }

    public decimal? CurrentOdometerKm { get; set; }

    public string Status { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

}
