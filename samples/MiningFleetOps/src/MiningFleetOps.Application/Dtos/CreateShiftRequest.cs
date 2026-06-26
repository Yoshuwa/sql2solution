namespace MiningFleetOps.Application.Dtos;

public sealed partial record CreateShiftRequest
(
    int SiteId,
    string ShiftCode,
    string ShiftName,
    TimeSpan StartTime,
    TimeSpan EndTime,
    decimal PlannedHours
);
