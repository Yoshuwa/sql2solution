namespace MiningFleetOps.Application.Dtos;

public sealed partial record PitDto
(
    int PitId,
    int SiteId,
    string PitCode,
    string PitName,
    decimal? BenchElevationM,
    bool IsActive
);
