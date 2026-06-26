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
[Route("api/tireInspections")]
public sealed partial class TireInspectionController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<DataChangeHub> _changes;

    public TireInspectionController(AppDbContext db, IHubContext<DataChangeHub> changes)
    {
        _db = db;
        _changes = changes;
    }

    partial void OnBeforeCreate(CreateTireInspectionRequest request, TireInspection item);
    partial void OnAfterCreate(TireInspection item);
    partial void OnBeforeUpdate(TireInspection item, UpdateTireInspectionRequest request);
    partial void OnBeforeDelete(TireInspection item);

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<TireInspectionDto>>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null, [FromQuery] string? filterField = null, [FromQuery] string? filterValue = null, [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        IQueryable<TireInspection> query = _db.Set<TireInspection>().AsNoTracking();
        query = ApplySearch(query, search);
        query = ApplyFilter(query, filterField, filterValue);
        query = ApplySort(query, sortBy, sortDirection);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<TireInspectionDto>>.Success("records loaded", new PagedResult<TireInspectionDto>(items, page, pageSize, total)));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<TireInspectionDto>>> GetById(long id, CancellationToken ct)
    {
        IQueryable<TireInspection> query = _db.Set<TireInspection>().AsNoTracking();
        var item = await query.FirstOrDefaultAsync(x => x.TireInspectionId!.Equals(id), ct);
        return item is null ? NotFound(ApiResponse<object>.Warning("record not found")) : Ok(ApiResponse<TireInspectionDto>.Success("record loaded", ToDto(item)));
    }

    [HttpGet("{id}/history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuditTrailDto>>>> GetHistory(long id, CancellationToken ct)
    {
        var canReadRecord = await _db.Set<TireInspection>().AsNoTracking().AnyAsync(x => x.TireInspectionId!.Equals(id), ct);
        if (!canReadRecord) return NotFound(ApiResponse<object>.Warning("record not found"));
        await EnsureAuditTrailTableAsync(ct);
        var resourceKey = Convert.ToString(id) ?? string.Empty;
        var history = await _db.AuditTrailEntries
            .AsNoTracking()
            .Where(entry => entry.Resource == "TireInspection" && entry.ResourceKey == resourceKey)
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .Take(100)
            .Select(entry => ToAuditTrailDto(entry))
            .ToListAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<AuditTrailDto>>.Success("activity loaded", history));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<TireInspectionDto>>> Create(CreateTireInspectionRequest request, CancellationToken ct)
    {
        var item = new TireInspection
        {
            TireInstallationId = request.TireInstallationId,
            InspectedAt = request.InspectedAt,
            HourMeter = request.HourMeter,
            TreadDepthMm = request.TreadDepthMm,
            PressureKpa = request.PressureKpa,
            TemperatureC = request.TemperatureC,
            ConditionRating = request.ConditionRating,
            Notes = request.Notes,
        };
        OnBeforeCreate(request, item);
        _db.Set<TireInspection>().Add(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Created", Convert.ToString(item.TireInspectionId) ?? string.Empty, $"Created TireInspection record {item.TireInspectionId}.", ToDto(item), ct);
        OnAfterCreate(item);
        await NotifyResourceChangedAsync("Created", Convert.ToString(item.TireInspectionId), ct);
        return CreatedAtAction(nameof(GetById), new { id = item.TireInspectionId }, ApiResponse<TireInspectionDto>.Success("record created", ToDto(item)));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, UpdateTireInspectionRequest request, CancellationToken ct)
    {
        var item = await _db.Set<TireInspection>().FirstOrDefaultAsync(x => x.TireInspectionId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeUpdate(item, request);
        item.TireInstallationId = request.TireInstallationId;
        item.InspectedAt = request.InspectedAt;
        item.HourMeter = request.HourMeter;
        item.TreadDepthMm = request.TreadDepthMm;
        item.PressureKpa = request.PressureKpa;
        item.TemperatureC = request.TemperatureC;
        item.ConditionRating = request.ConditionRating;
        item.Notes = request.Notes;
        var auditChanges = GetEntityChanges(_db.Entry(item));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Updated", Convert.ToString(item.TireInspectionId) ?? string.Empty, $"Updated TireInspection record {item.TireInspectionId}.", auditChanges, ct);
        await NotifyResourceChangedAsync("Updated", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record updated", new { updated = 1 }));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch(long id, UpdateTireInspectionRequest request, CancellationToken ct)
    {
        return await Update(id, request, ct);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var item = await _db.Set<TireInspection>().FirstOrDefaultAsync(x => x.TireInspectionId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeDelete(item);
        _db.Set<TireInspection>().Remove(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Deleted", Convert.ToString(id) ?? string.Empty, $"Hard deleted TireInspection record {id}.", ToDto(item), ct);
        await NotifyResourceChangedAsync("Deleted", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record deleted", new { deleted = 1, mode = "Hard" }));
    }

    [HttpPost("bulk/export")]
    public async Task<ActionResult<ApiResponse<PagedResult<TireInspectionDto>>>> ExportBulk(BulkIdsRequest request, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return Ok(ApiResponse<PagedResult<TireInspectionDto>>.Warning("no records selected", new PagedResult<TireInspectionDto>(Array.Empty<TireInspectionDto>(), page, pageSize, 0)));
        IQueryable<TireInspection> query = _db.Set<TireInspection>().AsNoTracking().Where(x => ids.Contains(x.TireInspectionId));
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<TireInspectionDto>>.Success("records exported", new PagedResult<TireInspectionDto>(items, page, pageSize, total)));
    }

    [HttpPatch("bulk")]
    public async Task<IActionResult> UpdateBulk(BulkUpdateRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        if (string.IsNullOrWhiteSpace(request.Field)) return BadRequest(ApiResponse<object>.Error("error", new { error = "Choose a field to update." }));
        IQueryable<TireInspection> query = _db.Set<TireInspection>().Where(x => ids.Contains(x.TireInspectionId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return NotFound(ApiResponse<object>.Warning("records not found"));
        if (!ApplyBulkUpdate(items, request, out var error)) return BadRequest(ApiResponse<object>.Error("error", new { error }));
        var auditChanges = items.ToDictionary(item => Convert.ToString(item.TireInspectionId) ?? string.Empty, item => GetEntityChanges(_db.Entry(item)));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Updated", Convert.ToString(item.TireInspectionId) ?? string.Empty, $"Updated TireInspection record {item.TireInspectionId} in bulk update.", auditChanges[Convert.ToString(item.TireInspectionId) ?? string.Empty], ct);
        await NotifyResourceChangedAsync("Updated", null, ct);
        return Ok(ApiResponse<object>.Success("records updated", new { updated = items.Count }));
    }

    [HttpPost("bulk/delete")]
    public async Task<IActionResult> DeleteBulk(BulkIdsRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        IQueryable<TireInspection> query = _db.Set<TireInspection>().Where(x => ids.Contains(x.TireInspectionId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return Ok(ApiResponse<object>.Warning("records not found", new { deleted = 0 }));
        foreach (var item in items)
        {
            OnBeforeDelete(item);
        }
        _db.Set<TireInspection>().RemoveRange(items);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Deleted", Convert.ToString(item.TireInspectionId) ?? string.Empty, $"Hard deleted TireInspection record {item.TireInspectionId} in bulk delete.", ToDto(item), ct);
        await NotifyResourceChangedAsync("Deleted", null, ct);
        return Ok(ApiResponse<object>.Success("records deleted", new { deleted = items.Count, mode = "Hard" }));
    }

    public sealed record BulkIdsRequest(IReadOnlyList<string>? Ids);
    public sealed record BulkUpdateRequest(IReadOnlyList<string>? Ids, string Field, string? Value);

    private static IReadOnlyList<long> ParseBulkIds(IReadOnlyList<string>? rawIds)
    {
        var ids = new List<long>();
        foreach (var raw in rawIds ?? Array.Empty<string>())
        {
            if (TryParseBulkId(raw, out var id)) ids.Add(id);
        }
        return ids.Distinct().ToList();
    }

    private static bool TryParseBulkId(string? raw, out long id)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            id = default;
            return false;
        }
        raw = raw.Trim();
        return long.TryParse(raw, out id);
    }

    private static bool ApplyBulkUpdate(IReadOnlyList<TireInspection> items, BulkUpdateRequest request, out string error)
    {
        error = string.Empty;
        return request.Field.Trim().ToLowerInvariant() switch
        {
            "tireinstallationid" => ApplyBulkTireInstallationId(items, request.Value, out error),
            "inspectedat" => ApplyBulkInspectedAt(items, request.Value, out error),
            "hourmeter" => ApplyBulkHourMeter(items, request.Value, out error),
            "treaddepthmm" => ApplyBulkTreadDepthMm(items, request.Value, out error),
            "pressurekpa" => ApplyBulkPressureKpa(items, request.Value, out error),
            "temperaturec" => ApplyBulkTemperatureC(items, request.Value, out error),
            "conditionrating" => ApplyBulkConditionRating(items, request.Value, out error),
            "notes" => ApplyBulkNotes(items, request.Value, out error),
            _ => FailBulkUpdate("Field is not bulk editable.", out error)
        };
    }

    private static bool ApplyBulkTireInstallationId(IReadOnlyList<TireInspection> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!long.TryParse(raw, out var value)) return FailBulkUpdate("TireInstallationId requires a long value.", out error);
        foreach (var item in items) item.TireInstallationId = value;
        return true;
    }

    private static bool ApplyBulkInspectedAt(IReadOnlyList<TireInspection> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!DateTime.TryParse(raw, out var value)) return FailBulkUpdate("InspectedAt requires a DateTime value.", out error);
        foreach (var item in items) item.InspectedAt = value;
        return true;
    }

    private static bool ApplyBulkHourMeter(IReadOnlyList<TireInspection> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("HourMeter requires a decimal value.", out error);
        foreach (var item in items) item.HourMeter = value;
        return true;
    }

    private static bool ApplyBulkTreadDepthMm(IReadOnlyList<TireInspection> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("TreadDepthMm requires a decimal value.", out error);
        foreach (var item in items) item.TreadDepthMm = value;
        return true;
    }

    private static bool ApplyBulkPressureKpa(IReadOnlyList<TireInspection> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.PressureKpa = null;
            return true;
        }
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("PressureKpa requires a decimal value.", out error);
        foreach (var item in items) item.PressureKpa = value;
        return true;
    }

    private static bool ApplyBulkTemperatureC(IReadOnlyList<TireInspection> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.TemperatureC = null;
            return true;
        }
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("TemperatureC requires a decimal value.", out error);
        foreach (var item in items) item.TemperatureC = value;
        return true;
    }

    private static bool ApplyBulkConditionRating(IReadOnlyList<TireInspection> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.ConditionRating = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkNotes(IReadOnlyList<TireInspection> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.Notes = null;
            return true;
        }
        foreach (var item in items) item.Notes = raw;
        return true;
    }

    private static bool FailBulkUpdate(string message, out string error)
    {
        error = message;
        return false;
    }


    private static IQueryable<TireInspection> ApplySearch(IQueryable<TireInspection> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        search = search.Trim();
        return query.Where(x => (x.ConditionRating != null && x.ConditionRating.Contains(search)) || (x.Notes != null && x.Notes.Contains(search)));
    }

    private static IQueryable<TireInspection> ApplyFilter(IQueryable<TireInspection> query, string? filterField, string? filterValue)
    {
        if (string.IsNullOrWhiteSpace(filterField) || string.IsNullOrWhiteSpace(filterValue)) return query;
        filterField = filterField.Trim();
        filterValue = filterValue.Trim();
        return filterField.ToLowerInvariant() switch
        {
            "tireinspectionid" => long.TryParse(filterValue, out var TireInspectionIdValue) ? query.Where(x => x.TireInspectionId == TireInspectionIdValue) : query,
            "tireinstallationid" => long.TryParse(filterValue, out var TireInstallationIdValue) ? query.Where(x => x.TireInstallationId == TireInstallationIdValue) : query,
            "inspectedat" => DateTime.TryParse(filterValue, out var InspectedAtValue) ? query.Where(x => x.InspectedAt == InspectedAtValue) : query,
            "hourmeter" => decimal.TryParse(filterValue, out var HourMeterValue) ? query.Where(x => x.HourMeter == HourMeterValue) : query,
            "treaddepthmm" => decimal.TryParse(filterValue, out var TreadDepthMmValue) ? query.Where(x => x.TreadDepthMm == TreadDepthMmValue) : query,
            "pressurekpa" => decimal.TryParse(filterValue, out var PressureKpaValue) ? query.Where(x => x.PressureKpa == PressureKpaValue) : query,
            "temperaturec" => decimal.TryParse(filterValue, out var TemperatureCValue) ? query.Where(x => x.TemperatureC == TemperatureCValue) : query,
            "conditionrating" => query.Where(x => x.ConditionRating != null && x.ConditionRating.Contains(filterValue)),
            "notes" => query.Where(x => x.Notes != null && x.Notes.Contains(filterValue)),
            _ => query
        };
    }

    private static IQueryable<TireInspection> ApplySort(IQueryable<TireInspection> query, string? sortBy, string? sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) || string.Equals(sortDirection, "descending", StringComparison.OrdinalIgnoreCase);
        var field = string.IsNullOrWhiteSpace(sortBy) ? "TireInspectionId" : sortBy.Trim();
        return field.ToLowerInvariant() switch
        {
            "tireinspectionid" => descending ? query.OrderByDescending(x => x.TireInspectionId) : query.OrderBy(x => x.TireInspectionId),
            "tireinstallationid" => descending ? query.OrderByDescending(x => x.TireInstallationId) : query.OrderBy(x => x.TireInstallationId),
            "inspectedat" => descending ? query.OrderByDescending(x => x.InspectedAt) : query.OrderBy(x => x.InspectedAt),
            "hourmeter" => descending ? query.OrderByDescending(x => x.HourMeter) : query.OrderBy(x => x.HourMeter),
            "treaddepthmm" => descending ? query.OrderByDescending(x => x.TreadDepthMm) : query.OrderBy(x => x.TreadDepthMm),
            "pressurekpa" => descending ? query.OrderByDescending(x => x.PressureKpa) : query.OrderBy(x => x.PressureKpa),
            "temperaturec" => descending ? query.OrderByDescending(x => x.TemperatureC) : query.OrderBy(x => x.TemperatureC),
            "conditionrating" => descending ? query.OrderByDescending(x => x.ConditionRating) : query.OrderBy(x => x.ConditionRating),
            "notes" => descending ? query.OrderByDescending(x => x.Notes) : query.OrderBy(x => x.Notes),
            _ => descending ? query.OrderByDescending(x => x.TireInspectionId) : query.OrderBy(x => x.TireInspectionId)
        };
    }
    private static TireInspectionDto ToDto(TireInspection item) => new(
        item.TireInspectionId,
        item.TireInstallationId,
        item.InspectedAt,
        item.HourMeter,
        item.TreadDepthMm,
        item.PressureKpa,
        item.TemperatureC,
        item.ConditionRating,
        item.Notes
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
            Resource = "TireInspection",
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
        _changes.Clients.All.SendAsync(DataChangeHub.DataChangedMethod, new DataChangeNotification("TireInspection", action, resourceKey, DateTimeOffset.UtcNow), ct);

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
