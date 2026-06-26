namespace MiningFleetOps.Application.Dtos;

public sealed partial record ShiftDto
(
    int ShiftId,
    int SiteId,
    string ShiftCode,
    string ShiftName,
    TimeSpan StartTime,
    TimeSpan EndTime,
    decimal PlannedHours
);
