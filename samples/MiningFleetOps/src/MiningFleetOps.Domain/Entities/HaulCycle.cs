namespace MiningFleetOps.Domain.Entities;

public sealed partial class HaulCycle
{
    public long HaulCycleId { get; set; }

    public int EquipmentId { get; set; }

    public int? OperatorEmployeeId { get; set; }

    public int? ShiftId { get; set; }

    public int? PitId { get; set; }

    public int MaterialId { get; set; }

    public DateTime CycleStartedAt { get; set; }

    public DateTime CycleEndedAt { get; set; }

    public decimal LoadedTonnes { get; set; }

    public decimal? DistanceKm { get; set; }

    public decimal? FuelLitersEstimated { get; set; }

    public decimal? TonnesPerHour { get; set; }

    public decimal? CycleMinutes { get; set; }

    public decimal? TonnesKm { get; set; }

}
