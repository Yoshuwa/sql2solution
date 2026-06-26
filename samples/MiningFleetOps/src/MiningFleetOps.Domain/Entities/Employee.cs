namespace MiningFleetOps.Domain.Entities;

public sealed partial class Employee
{
    public int EmployeeId { get; set; }

    public int SiteId { get; set; }

    public string EmployeeCode { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    public string? LicenseClass { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

}
