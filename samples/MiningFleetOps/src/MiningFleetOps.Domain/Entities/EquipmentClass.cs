namespace MiningFleetOps.Domain.Entities;

public sealed partial class EquipmentClass
{
    public int EquipmentClassId { get; set; }

    public string ClassCode { get; set; } = string.Empty;

    public string ClassName { get; set; } = string.Empty;

    public string CategoryName { get; set; } = string.Empty;

    public decimal? TypicalPayloadTonnes { get; set; }

    public decimal? DefaultFuelBurnLph { get; set; }

    public decimal MaintenanceIntervalHours { get; set; }

    public decimal OilIntervalHours { get; set; }

}
