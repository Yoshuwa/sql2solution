namespace MiningFleetOps.Application.Dtos;

public sealed partial record CreateFluidServiceRequest
(
    int EquipmentId,
    int FluidTypeId,
    DateTime ServicedAt,
    decimal HourMeter,
    decimal LitersChanged,
    bool FilterChanged,
    long? WorkOrderId,
    int? TechnicianEmployeeId,
    decimal? NextDueHourMeter,
    string? Notes
);
