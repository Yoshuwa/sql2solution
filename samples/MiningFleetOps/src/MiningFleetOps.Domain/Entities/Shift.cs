namespace MiningFleetOps.Domain.Entities;

public sealed partial class Shift
{
    public int ShiftId { get; set; }

    public int SiteId { get; set; }

    public string ShiftCode { get; set; } = string.Empty;

    public string ShiftName { get; set; } = string.Empty;

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public decimal PlannedHours { get; set; }

}
