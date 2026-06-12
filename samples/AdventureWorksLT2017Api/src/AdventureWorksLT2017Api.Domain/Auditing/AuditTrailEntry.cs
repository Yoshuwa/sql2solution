namespace AdventureWorksLT2017Api.Domain.Auditing;

public sealed partial class AuditTrailEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Resource { get; set; } = "";
    public string ResourceKey { get; set; } = "";
    public string Action { get; set; } = "";
    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? TenantId { get; set; }
    public string? Summary { get; set; }
    public string? ChangesJson { get; set; }
}