namespace MiningFleetOps.Application.Dtos;

public sealed partial record HaulCycleDto
(
    long HaulCycleId,
    int EquipmentId,
    int? OperatorEmployeeId,
    int? ShiftId,
    int? PitId,
    int MaterialId,
    DateTime CycleStartedAt,
    DateTime CycleEndedAt,
    decimal LoadedTonnes,
    decimal? DistanceKm,
    decimal? FuelLitersEstimated,
    decimal? TonnesPerHour,
    decimal? CycleMinutes,
    decimal? TonnesKm
);
