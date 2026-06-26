namespace MiningFleetOps.Application.Dtos;

public sealed partial record EquipmentDto
(
    int EquipmentId,
    int SiteId,
    int EquipmentClassId,
    string AssetTag,
    string? SerialNumber,
    string? Manufacturer,
    string? Model,
    DateTime? CommissionDate,
    int FuelTypeId,
    decimal? TankCapacityL,
    decimal CurrentHourMeter,
    decimal? CurrentOdometerKm,
    string Status,
    bool IsActive,
    DateTime CreatedAt
);
