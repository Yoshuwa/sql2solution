namespace MiningFleetOps.Application.Dtos;

public sealed partial record FluidServiceDto
(
    long FluidServiceId,
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
