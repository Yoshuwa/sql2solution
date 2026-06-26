namespace MiningFleetOps.Application.Dtos;

public sealed partial record UpdateEmployeeRequest
(
    int SiteId,
    string EmployeeCode,
    string FullName,
    string RoleName,
    string? LicenseClass,
    string? Phone,
    string? Email,
    bool IsActive,
    DateTime CreatedAt
);
