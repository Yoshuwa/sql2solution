namespace MiningFleetOps.Application.Dtos;

public sealed partial record CreatePitRequest
(
    int SiteId,
    string PitCode,
    string PitName,
    decimal? BenchElevationM,
    bool IsActive
);
