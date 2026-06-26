namespace MiningFleetOps.Domain.Entities;

public sealed partial class MeterReading
{
    public long MeterReadingId { get; set; }

    public int EquipmentId { get; set; }

    public DateTime ReadingAt { get; set; }

    public decimal HourMeter { get; set; }

    public decimal? OdometerKm { get; set; }

    public string SourceName { get; set; } = string.Empty;

    public int? RecordedByEmployeeId { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

}
