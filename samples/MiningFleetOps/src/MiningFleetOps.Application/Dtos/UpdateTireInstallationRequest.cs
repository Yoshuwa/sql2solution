namespace MiningFleetOps.Application.Dtos;

public sealed partial record UpdateTireInstallationRequest
(
    int TireId,
    int EquipmentId,
    string PositionCode,
    DateTime InstalledAt,
    DateTime? RemovedAt,
    decimal InstallHourMeter,
    decimal? RemoveHourMeter,
    string? RemovalReason
);
