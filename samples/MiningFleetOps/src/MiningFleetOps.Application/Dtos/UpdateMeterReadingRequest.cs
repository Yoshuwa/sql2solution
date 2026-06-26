namespace MiningFleetOps.Application.Dtos;

public sealed partial record UpdateMeterReadingRequest
(
    int EquipmentId,
    DateTime ReadingAt,
    decimal HourMeter,
    decimal? OdometerKm,
    string SourceName,
    int? RecordedByEmployeeId,
    string? Notes,
    DateTime CreatedAt
);
