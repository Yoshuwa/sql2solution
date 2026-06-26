namespace MiningFleetOps.Application.Dtos;

public sealed partial record EmployeeDto
(
    int EmployeeId,
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
