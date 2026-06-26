namespace MiningFleetOps.Application.Dtos;

public sealed partial record CreateFuelLogRequest
(
    int EquipmentId,
    int FuelTypeId,
    DateTime FueledAt,
    int? ShiftId,
    int? EmployeeId,
    int? PitId,
    decimal HourMeter,
    decimal? OdometerKm,
    decimal Liters,
    decimal? UnitCost,
    decimal? HoursSinceLastFuel,
    decimal? FuelBurnLph,
    decimal Co2KgPerL,
    string SourceName,
    string? Notes,
    DateTime CreatedAt,
    decimal? CostAmount,
    decimal? Co2Kg
);
