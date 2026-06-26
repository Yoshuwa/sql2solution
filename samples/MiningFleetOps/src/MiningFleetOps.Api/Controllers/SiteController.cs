using MiningFleetOps.Application.Common;
using MiningFleetOps.Application.Dtos;
using MiningFleetOps.Api.Realtime;
using MiningFleetOps.Domain.Auditing;
using MiningFleetOps.Domain.Entities;
using MiningFleetOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using System.Text.Json;

namespace MiningFleetOps.Api.Controllers;

[ApiController]
[Route("api/sites")]
public sealed partial class SiteController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<DataChangeHub> _changes;

    public SiteController(AppDbContext db, IHubContext<DataChangeHub> changes)
    {
        _db = db;
        _changes = changes;
    }

    partial void OnBeforeCreate(CreateSiteRequest request, Site item);
    partial void OnAfterCreate(Site item);
    partial void OnBeforeUpdate(Site item, UpdateSiteRequest request);
    partial void OnBeforeDelete(Site item);

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<SiteDto>>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null, [FromQuery] string? filterField = null, [FromQuery] string? filterValue = null, [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        IQueryable<Site> query = _db.Set<Site>().AsNoTracking();
        query = ApplySearch(query, search);
        query = ApplyFilter(query, filterField, filterValue);
        query = ApplySort(query, sortBy, sortDirection);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<SiteDto>>.Success("records loaded", new PagedResult<SiteDto>(items, page, pageSize, total)));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<SiteDto>>> GetById(int id, CancellationToken ct)
    {
        IQueryable<Site> query = _db.Set<Site>().AsNoTracking();
        var item = await query.FirstOrDefaultAsync(x => x.SiteId!.Equals(id), ct);
        return item is null ? NotFound(ApiResponse<object>.Warning("record not found")) : Ok(ApiResponse<SiteDto>.Success("record loaded", ToDto(item)));
    }

    [HttpGet("{id}/history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuditTrailDto>>>> GetHistory(int id, CancellationToken ct)
    {
        var canReadRecord = await _db.Set<Site>().AsNoTracking().AnyAsync(x => x.SiteId!.Equals(id), ct);
        if (!canReadRecord) return NotFound(ApiResponse<object>.Warning("record not found"));
        await EnsureAuditTrailTableAsync(ct);
        var resourceKey = Convert.ToString(id) ?? string.Empty;
        var history = await _db.AuditTrailEntries
            .AsNoTracking()
            .Where(entry => entry.Resource == "Site" && entry.ResourceKey == resourceKey)
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .Take(100)
            .Select(entry => ToAuditTrailDto(entry))
            .ToListAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<AuditTrailDto>>.Success("activity loaded", history));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<SiteDto>>> Create(CreateSiteRequest request, CancellationToken ct)
    {
        var item = new Site
        {
            SiteCode = request.SiteCode,
            SiteName = request.SiteName,
            Country = request.Country,
            Region = request.Region,
            TimeZoneName = request.TimeZoneName,
            IsActive = request.IsActive,
            CreatedAt = request.CreatedAt,
        };
        OnBeforeCreate(request, item);
        _db.Set<Site>().Add(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Created", Convert.ToString(item.SiteId) ?? string.Empty, $"Created Site record {item.SiteId}.", ToDto(item), ct);
        OnAfterCreate(item);
        await NotifyResourceChangedAsync("Created", Convert.ToString(item.SiteId), ct);
        return CreatedAtAction(nameof(GetById), new { id = item.SiteId }, ApiResponse<SiteDto>.Success("record created", ToDto(item)));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateSiteRequest request, CancellationToken ct)
    {
        var item = await _db.Set<Site>().FirstOrDefaultAsync(x => x.SiteId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeUpdate(item, request);
        item.SiteCode = request.SiteCode;
        item.SiteName = request.SiteName;
        item.Country = request.Country;
        item.Region = request.Region;
        item.TimeZoneName = request.TimeZoneName;
        item.IsActive = request.IsActive;
        item.CreatedAt = request.CreatedAt;
        var auditChanges = GetEntityChanges(_db.Entry(item));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Updated", Convert.ToString(item.SiteId) ?? string.Empty, $"Updated Site record {item.SiteId}.", auditChanges, ct);
        await NotifyResourceChangedAsync("Updated", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record updated", new { updated = 1 }));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch(int id, UpdateSiteRequest request, CancellationToken ct)
    {
        return await Update(id, request, ct);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var item = await _db.Set<Site>().FirstOrDefaultAsync(x => x.SiteId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeDelete(item);
        _db.Set<Site>().Remove(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Deleted", Convert.ToString(id) ?? string.Empty, $"Hard deleted Site record {id}.", ToDto(item), ct);
        await NotifyResourceChangedAsync("Deleted", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record deleted", new { deleted = 1, mode = "Hard" }));
    }

    [HttpPost("bulk/export")]
    public async Task<ActionResult<ApiResponse<PagedResult<SiteDto>>>> ExportBulk(BulkIdsRequest request, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return Ok(ApiResponse<PagedResult<SiteDto>>.Warning("no records selected", new PagedResult<SiteDto>(Array.Empty<SiteDto>(), page, pageSize, 0)));
        IQueryable<Site> query = _db.Set<Site>().AsNoTracking().Where(x => ids.Contains(x.SiteId));
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<SiteDto>>.Success("records exported", new PagedResult<SiteDto>(items, page, pageSize, total)));
    }

    [HttpPatch("bulk")]
    public async Task<IActionResult> UpdateBulk(BulkUpdateRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        if (string.IsNullOrWhiteSpace(request.Field)) return BadRequest(ApiResponse<object>.Error("error", new { error = "Choose a field to update." }));
        IQueryable<Site> query = _db.Set<Site>().Where(x => ids.Contains(x.SiteId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return NotFound(ApiResponse<object>.Warning("records not found"));
        if (!ApplyBulkUpdate(items, request, out var error)) return BadRequest(ApiResponse<object>.Error("error", new { error }));
        var auditChanges = items.ToDictionary(item => Convert.ToString(item.SiteId) ?? string.Empty, item => GetEntityChanges(_db.Entry(item)));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Updated", Convert.ToString(item.SiteId) ?? string.Empty, $"Updated Site record {item.SiteId} in bulk update.", auditChanges[Convert.ToString(item.SiteId) ?? string.Empty], ct);
        await NotifyResourceChangedAsync("Updated", null, ct);
        return Ok(ApiResponse<object>.Success("records updated", new { updated = items.Count }));
    }

    [HttpPost("bulk/delete")]
    public async Task<IActionResult> DeleteBulk(BulkIdsRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        IQueryable<Site> query = _db.Set<Site>().Where(x => ids.Contains(x.SiteId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return Ok(ApiResponse<object>.Warning("records not found", new { deleted = 0 }));
        foreach (var item in items)
        {
            OnBeforeDelete(item);
        }
        _db.Set<Site>().RemoveRange(items);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Deleted", Convert.ToString(item.SiteId) ?? string.Empty, $"Hard deleted Site record {item.SiteId} in bulk delete.", ToDto(item), ct);
        await NotifyResourceChangedAsync("Deleted", null, ct);
        return Ok(ApiResponse<object>.Success("records deleted", new { deleted = items.Count, mode = "Hard" }));
    }

    public sealed record BulkIdsRequest(IReadOnlyList<string>? Ids);
    public sealed record BulkUpdateRequest(IReadOnlyList<string>? Ids, string Field, string? Value);

    private static IReadOnlyList<int> ParseBulkIds(IReadOnlyList<string>? rawIds)
    {
        var ids = new List<int>();
        foreach (var raw in rawIds ?? Array.Empty<string>())
        {
            if (TryParseBulkId(raw, out var id)) ids.Add(id);
        }
        return ids.Distinct().ToList();
    }

    private static bool TryParseBulkId(string? raw, out int id)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            id = default;
            return false;
        }
        raw = raw.Trim();
        return int.TryParse(raw, out id);
    }

    private static bool ApplyBulkUpdate(IReadOnlyList<Site> items, BulkUpdateRequest request, out string error)
    {
        error = string.Empty;
        return request.Field.Trim().ToLowerInvariant() switch
        {
            "sitecode" => ApplyBulkSiteCode(items, request.Value, out error),
            "sitename" => ApplyBulkSiteName(items, request.Value, out error),
            "country" => ApplyBulkCountry(items, request.Value, out error),
            "region" => ApplyBulkRegion(items, request.Value, out error),
            "timezonename" => ApplyBulkTimeZoneName(items, request.Value, out error),
            "isactive" => ApplyBulkIsActive(items, request.Value, out error),
            "createdat" => ApplyBulkCreatedAt(items, request.Value, out error),
            _ => FailBulkUpdate("Field is not bulk editable.", out error)
        };
    }

    private static bool ApplyBulkSiteCode(IReadOnlyList<Site> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.SiteCode = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkSiteName(IReadOnlyList<Site> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.SiteName = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkCountry(IReadOnlyList<Site> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.Country = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkRegion(IReadOnlyList<Site> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.Region = null;
            return true;
        }
        foreach (var item in items) item.Region = raw;
        return true;
    }

    private static bool ApplyBulkTimeZoneName(IReadOnlyList<Site> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.TimeZoneName = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkIsActive(IReadOnlyList<Site> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!bool.TryParse(raw, out var value)) return FailBulkUpdate("IsActive requires a boolean value.", out error);
        foreach (var item in items) item.IsActive = value;
        return true;
    }

    private static bool ApplyBulkCreatedAt(IReadOnlyList<Site> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!DateTime.TryParse(raw, out var value)) return FailBulkUpdate("CreatedAt requires a DateTime value.", out error);
        foreach (var item in items) item.CreatedAt = value;
        return true;
    }

    private static bool FailBulkUpdate(string message, out string error)
    {
        error = message;
        return false;
    }


    private static IQueryable<Site> ApplySearch(IQueryable<Site> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        search = search.Trim();
        return query.Where(x => (x.SiteCode != null && x.SiteCode.Contains(search)) || (x.SiteName != null && x.SiteName.Contains(search)) || (x.Country != null && x.Country.Contains(search)) || (x.Region != null && x.Region.Contains(search)) || (x.TimeZoneName != null && x.TimeZoneName.Contains(search)));
    }

    private static IQueryable<Site> ApplyFilter(IQueryable<Site> query, string? filterField, string? filterValue)
    {
        if (string.IsNullOrWhiteSpace(filterField) || string.IsNullOrWhiteSpace(filterValue)) return query;
        filterField = filterField.Trim();
        filterValue = filterValue.Trim();
        return filterField.ToLowerInvariant() switch
        {
            "siteid" => int.TryParse(filterValue, out var SiteIdValue) ? query.Where(x => x.SiteId == SiteIdValue) : query,
            "sitecode" => query.Where(x => x.SiteCode != null && x.SiteCode.Contains(filterValue)),
            "sitename" => query.Where(x => x.SiteName != null && x.SiteName.Contains(filterValue)),
            "country" => query.Where(x => x.Country != null && x.Country.Contains(filterValue)),
            "region" => query.Where(x => x.Region != null && x.Region.Contains(filterValue)),
            "timezonename" => query.Where(x => x.TimeZoneName != null && x.TimeZoneName.Contains(filterValue)),
            "isactive" => bool.TryParse(filterValue, out var IsActiveValue) ? query.Where(x => x.IsActive == IsActiveValue) : query,
            "createdat" => DateTime.TryParse(filterValue, out var CreatedAtValue) ? query.Where(x => x.CreatedAt == CreatedAtValue) : query,
            _ => query
        };
    }

    private static IQueryable<Site> ApplySort(IQueryable<Site> query, string? sortBy, string? sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) || string.Equals(sortDirection, "descending", StringComparison.OrdinalIgnoreCase);
        var field = string.IsNullOrWhiteSpace(sortBy) ? "SiteId" : sortBy.Trim();
        return field.ToLowerInvariant() switch
        {
            "siteid" => descending ? query.OrderByDescending(x => x.SiteId) : query.OrderBy(x => x.SiteId),
            "sitecode" => descending ? query.OrderByDescending(x => x.SiteCode) : query.OrderBy(x => x.SiteCode),
            "sitename" => descending ? query.OrderByDescending(x => x.SiteName) : query.OrderBy(x => x.SiteName),
            "country" => descending ? query.OrderByDescending(x => x.Country) : query.OrderBy(x => x.Country),
            "region" => descending ? query.OrderByDescending(x => x.Region) : query.OrderBy(x => x.Region),
            "timezonename" => descending ? query.OrderByDescending(x => x.TimeZoneName) : query.OrderBy(x => x.TimeZoneName),
            "isactive" => descending ? query.OrderByDescending(x => x.IsActive) : query.OrderBy(x => x.IsActive),
            "createdat" => descending ? query.OrderByDescending(x => x.CreatedAt) : query.OrderBy(x => x.CreatedAt),
            _ => descending ? query.OrderByDescending(x => x.SiteId) : query.OrderBy(x => x.SiteId)
        };
    }
    private static SiteDto ToDto(Site item) => new(
        item.SiteId,
        item.SiteCode,
        item.SiteName,
        item.Country,
        item.Region,
        item.TimeZoneName,
        item.IsActive,
        item.CreatedAt
    );

    private static AuditTrailDto ToAuditTrailDto(AuditTrailEntry entry) => new(
        entry.Id,
        entry.Resource,
        entry.ResourceKey,
        entry.Action,
        entry.OccurredAtUtc,
        entry.UserId,
        entry.UserName,
        entry.TenantId,
        entry.Summary,
        entry.ChangesJson
    );

    private static IReadOnlyList<object> GetEntityChanges(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry) =>
        entry.Properties
            .Where(property => property.IsModified && !Equals(property.OriginalValue, property.CurrentValue))
            .Select(property => (object)new
            {
                column = property.Metadata.Name,
                before = property.OriginalValue,
                after = property.CurrentValue
            })
            .ToList();

    private async Task LogAuditTrailAsync(string action, string resourceKey, string summary, object? changes, CancellationToken ct)
    {
        await EnsureAuditTrailTableAsync(ct);
        _db.AuditTrailEntries.Add(new AuditTrailEntry
        {
            Resource = "Site",
            ResourceKey = resourceKey,
            Action = action,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            UserName = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Name),
            TenantId = User.FindFirstValue("tenant_id"),
            Summary = summary,
            ChangesJson = changes is null ? null : JsonSerializer.Serialize(changes)
        });
        await _db.SaveChangesAsync(ct);
    }

    private Task NotifyResourceChangedAsync(string action, string? resourceKey, CancellationToken ct) =>
        _changes.Clients.All.SendAsync(DataChangeHub.DataChangedMethod, new DataChangeNotification("Site", action, resourceKey, DateTimeOffset.UtcNow), ct);

    private async Task EnsureAuditTrailTableAsync(CancellationToken ct)
    {
        var provider = _db.Database.ProviderName ?? string.Empty;
        if (!provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
            return;

        await _db.Database.ExecuteSqlRawAsync("""
        IF OBJECT_ID(N'[dbo].[AuditTrailEntries]', N'U') IS NULL
        BEGIN
            CREATE TABLE [dbo].[AuditTrailEntries] (
                [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_AuditTrailEntries] PRIMARY KEY,
                [Resource] nvarchar(256) NOT NULL,
                [ResourceKey] nvarchar(256) NOT NULL,
                [Action] nvarchar(64) NOT NULL,
                [OccurredAtUtc] datetimeoffset NOT NULL,
                [UserId] nvarchar(256) NULL,
                [UserName] nvarchar(256) NULL,
                [TenantId] nvarchar(256) NULL,
                [Summary] nvarchar(1024) NULL,
                [ChangesJson] nvarchar(max) NULL
            );

            CREATE INDEX [IX_AuditTrailEntries_Resource_ResourceKey_OccurredAtUtc]
                ON [dbo].[AuditTrailEntries] ([Resource], [ResourceKey], [OccurredAtUtc]);
        END
        """, ct);
    }
}
