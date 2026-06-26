namespace MiningFleetOps.Application.Dtos;

public sealed partial record CreateEquipmentClassRequest
(
    string ClassCode,
    string ClassName,
    string CategoryName,
    decimal? TypicalPayloadTonnes,
    decimal? DefaultFuelBurnLph,
    decimal MaintenanceIntervalHours,
    decimal OilIntervalHours
);
