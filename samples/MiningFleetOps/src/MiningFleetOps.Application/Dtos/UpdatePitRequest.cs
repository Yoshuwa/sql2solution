namespace MiningFleetOps.Application.Dtos;

public sealed partial record UpdatePitRequest
(
    int SiteId,
    string PitCode,
    string PitName,
    decimal? BenchElevationM,
    bool IsActive
);
