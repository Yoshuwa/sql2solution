namespace MiningFleetOps.Application.Dtos;

public sealed partial record CreateTireInspectionRequest
(
    long TireInstallationId,
    DateTime InspectedAt,
    decimal HourMeter,
    decimal TreadDepthMm,
    decimal? PressureKpa,
    decimal? TemperatureC,
    string ConditionRating,
    string? Notes
);
