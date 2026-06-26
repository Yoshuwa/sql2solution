namespace MiningFleetOps.Application.Dtos;

public sealed partial record UpdateSiteRequest
(
    string SiteCode,
    string SiteName,
    string Country,
    string? Region,
    string TimeZoneName,
    bool IsActive,
    DateTime CreatedAt
);
