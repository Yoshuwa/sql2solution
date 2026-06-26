namespace MiningFleetOps.Domain.Entities;

public sealed partial class FuelLog
{
    public long FuelLogId { get; set; }

    public int EquipmentId { get; set; }

    public int FuelTypeId { get; set; }

    public DateTime FueledAt { get; set; }

    public int? ShiftId { get; set; }

    public int? EmployeeId { get; set; }

    public int? PitId { get; set; }

    public decimal HourMeter { get; set; }

    public decimal? OdometerKm { get; set; }

    public decimal Liters { get; set; }

    public decimal? UnitCost { get; set; }

    public decimal? HoursSinceLastFuel { get; set; }

    public decimal? FuelBurnLph { get; set; }

    public decimal Co2KgPerL { get; set; }

    public string SourceName { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public decimal? CostAmount { get; set; }

    public decimal? Co2Kg { get; set; }

}
