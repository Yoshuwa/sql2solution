namespace MiningFleetOps.Domain.Entities;

public sealed partial class FluidService
{
    public long FluidServiceId { get; set; }

    public int EquipmentId { get; set; }

    public int FluidTypeId { get; set; }

    public DateTime ServicedAt { get; set; }

    public decimal HourMeter { get; set; }

    public decimal LitersChanged { get; set; }

    public bool FilterChanged { get; set; }

    public long? WorkOrderId { get; set; }

    public int? TechnicianEmployeeId { get; set; }

    public decimal? NextDueHourMeter { get; set; }

    public string? Notes { get; set; }

}
