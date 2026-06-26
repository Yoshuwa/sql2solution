namespace MiningFleetOps.Application.Common;

public sealed record AuditTrailDto(
    Guid Id,
    string Resource,
    string ResourceKey,
    string Action,
    DateTimeOffset OccurredAtUtc,
    string? UserId,
    string? UserName,
    string? TenantId,
    string? Summary,
    string? ChangesJson);