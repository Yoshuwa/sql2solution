namespace MiningFleetOps.Domain.Entities;

public sealed partial class Site
{
    public int SiteId { get; set; }

    public string SiteCode { get; set; } = string.Empty;

    public string SiteName { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public string? Region { get; set; }

    public string TimeZoneName { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

}
