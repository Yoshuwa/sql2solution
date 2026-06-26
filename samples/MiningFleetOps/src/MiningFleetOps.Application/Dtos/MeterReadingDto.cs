namespace MiningFleetOps.Application.Dtos;

public sealed partial record MeterReadingDto
(
    long MeterReadingId,
    int EquipmentId,
    DateTime ReadingAt,
    decimal HourMeter,
    decimal? OdometerKm,
    string SourceName,
    int? RecordedByEmployeeId,
    string? Notes,
    DateTime CreatedAt
);
