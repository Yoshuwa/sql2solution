namespace MiningFleetOps.Application.Dtos;

public sealed partial record UpdateShiftRequest
(
    int SiteId,
    string ShiftCode,
    string ShiftName,
    TimeSpan StartTime,
    TimeSpan EndTime,
    decimal PlannedHours
);
