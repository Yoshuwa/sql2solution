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
[Route("api/downtimeEvents")]
public sealed partial class DowntimeEventController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<DataChangeHub> _changes;

    public DowntimeEventController(AppDbContext db, IHubContext<DataChangeHub> changes)
    {
        _db = db;
        _changes = changes;
    }

    partial void OnBeforeCreate(CreateDowntimeEventRequest request, DowntimeEvent item);
    partial void OnAfterCreate(DowntimeEvent item);
    partial void OnBeforeUpdate(DowntimeEvent item, UpdateDowntimeEventRequest request);
    partial void OnBeforeDelete(DowntimeEvent item);

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<DowntimeEventDto>>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null, [FromQuery] string? filterField = null, [FromQuery] string? filterValue = null, [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        IQueryable<DowntimeEvent> query = _db.Set<DowntimeEvent>().AsNoTracking();
        query = ApplySearch(query, search);
        query = ApplyFilter(query, filterField, filterValue);
        query = ApplySort(query, sortBy, sortDirection);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<DowntimeEventDto>>.Success("records loaded", new PagedResult<DowntimeEventDto>(items, page, pageSize, total)));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<DowntimeEventDto>>> GetById(long id, CancellationToken ct)
    {
        IQueryable<DowntimeEvent> query = _db.Set<DowntimeEvent>().AsNoTracking();
        var item = await query.FirstOrDefaultAsync(x => x.DowntimeEventId!.Equals(id), ct);
        return item is null ? NotFound(ApiResponse<object>.Warning("record not found")) : Ok(ApiResponse<DowntimeEventDto>.Success("record loaded", ToDto(item)));
    }

    [HttpGet("{id}/history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuditTrailDto>>>> GetHistory(long id, CancellationToken ct)
    {
        var canReadRecord = await _db.Set<DowntimeEvent>().AsNoTracking().AnyAsync(x => x.DowntimeEventId!.Equals(id), ct);
        if (!canReadRecord) return NotFound(ApiResponse<object>.Warning("record not found"));
        await EnsureAuditTrailTableAsync(ct);
        var resourceKey = Convert.ToString(id) ?? string.Empty;
        var history = await _db.AuditTrailEntries
            .AsNoTracking()
            .Where(entry => entry.Resource == "DowntimeEvent" && entry.ResourceKey == resourceKey)
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .Take(100)
            .Select(entry => ToAuditTrailDto(entry))
            .ToListAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<AuditTrailDto>>.Success("activity loaded", history));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<DowntimeEventDto>>> Create(CreateDowntimeEventRequest request, CancellationToken ct)
    {
        var item = new DowntimeEvent
        {
            EquipmentId = request.EquipmentId,
            WorkOrderId = request.WorkOrderId,
            StartedAt = request.StartedAt,
            EndedAt = request.EndedAt,
            ReasonCategory = request.ReasonCategory,
            ReasonDetail = request.ReasonDetail,
            IsPlanned = request.IsPlanned,
            DowntimeHours = request.DowntimeHours,
        };
        OnBeforeCreate(request, item);
        _db.Set<DowntimeEvent>().Add(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Created", Convert.ToString(item.DowntimeEventId) ?? string.Empty, $"Created DowntimeEvent record {item.DowntimeEventId}.", ToDto(item), ct);
        OnAfterCreate(item);
        await NotifyResourceChangedAsync("Created", Convert.ToString(item.DowntimeEventId), ct);
        return CreatedAtAction(nameof(GetById), new { id = item.DowntimeEventId }, ApiResponse<DowntimeEventDto>.Success("record created", ToDto(item)));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, UpdateDowntimeEventRequest request, CancellationToken ct)
    {
        var item = await _db.Set<DowntimeEvent>().FirstOrDefaultAsync(x => x.DowntimeEventId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeUpdate(item, request);
        item.EquipmentId = request.EquipmentId;
        item.WorkOrderId = request.WorkOrderId;
        item.StartedAt = request.StartedAt;
        item.EndedAt = request.EndedAt;
        item.ReasonCategory = request.ReasonCategory;
        item.ReasonDetail = request.ReasonDetail;
        item.IsPlanned = request.IsPlanned;
        item.DowntimeHours = request.DowntimeHours;
        var auditChanges = GetEntityChanges(_db.Entry(item));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Updated", Convert.ToString(item.DowntimeEventId) ?? string.Empty, $"Updated DowntimeEvent record {item.DowntimeEventId}.", auditChanges, ct);
        await NotifyResourceChangedAsync("Updated", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record updated", new { updated = 1 }));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch(long id, UpdateDowntimeEventRequest request, CancellationToken ct)
    {
        return await Update(id, request, ct);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var item = await _db.Set<DowntimeEvent>().FirstOrDefaultAsync(x => x.DowntimeEventId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeDelete(item);
        _db.Set<DowntimeEvent>().Remove(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Deleted", Convert.ToString(id) ?? string.Empty, $"Hard deleted DowntimeEvent record {id}.", ToDto(item), ct);
        await NotifyResourceChangedAsync("Deleted", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record deleted", new { deleted = 1, mode = "Hard" }));
    }

    [HttpPost("bulk/export")]
    public async Task<ActionResult<ApiResponse<PagedResult<DowntimeEventDto>>>> ExportBulk(BulkIdsRequest request, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return Ok(ApiResponse<PagedResult<DowntimeEventDto>>.Warning("no records selected", new PagedResult<DowntimeEventDto>(Array.Empty<DowntimeEventDto>(), page, pageSize, 0)));
        IQueryable<DowntimeEvent> query = _db.Set<DowntimeEvent>().AsNoTracking().Where(x => ids.Contains(x.DowntimeEventId));
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<DowntimeEventDto>>.Success("records exported", new PagedResult<DowntimeEventDto>(items, page, pageSize, total)));
    }

    [HttpPatch("bulk")]
    public async Task<IActionResult> UpdateBulk(BulkUpdateRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        if (string.IsNullOrWhiteSpace(request.Field)) return BadRequest(ApiResponse<object>.Error("error", new { error = "Choose a field to update." }));
        IQueryable<DowntimeEvent> query = _db.Set<DowntimeEvent>().Where(x => ids.Contains(x.DowntimeEventId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return NotFound(ApiResponse<object>.Warning("records not found"));
        if (!ApplyBulkUpdate(items, request, out var error)) return BadRequest(ApiResponse<object>.Error("error", new { error }));
        var auditChanges = items.ToDictionary(item => Convert.ToString(item.DowntimeEventId) ?? string.Empty, item => GetEntityChanges(_db.Entry(item)));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Updated", Convert.ToString(item.DowntimeEventId) ?? string.Empty, $"Updated DowntimeEvent record {item.DowntimeEventId} in bulk update.", auditChanges[Convert.ToString(item.DowntimeEventId) ?? string.Empty], ct);
        await NotifyResourceChangedAsync("Updated", null, ct);
        return Ok(ApiResponse<object>.Success("records updated", new { updated = items.Count }));
    }

    [HttpPost("bulk/delete")]
    public async Task<IActionResult> DeleteBulk(BulkIdsRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        IQueryable<DowntimeEvent> query = _db.Set<DowntimeEvent>().Where(x => ids.Contains(x.DowntimeEventId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return Ok(ApiResponse<object>.Warning("records not found", new { deleted = 0 }));
        foreach (var item in items)
        {
            OnBeforeDelete(item);
        }
        _db.Set<DowntimeEvent>().RemoveRange(items);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Deleted", Convert.ToString(item.DowntimeEventId) ?? string.Empty, $"Hard deleted DowntimeEvent record {item.DowntimeEventId} in bulk delete.", ToDto(item), ct);
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

    private static bool ApplyBulkUpdate(IReadOnlyList<DowntimeEvent> items, BulkUpdateRequest request, out string error)
    {
        error = string.Empty;
        return request.Field.Trim().ToLowerInvariant() switch
        {
            "equipmentid" => ApplyBulkEquipmentId(items, request.Value, out error),
            "workorderid" => ApplyBulkWorkOrderId(items, request.Value, out error),
            "startedat" => ApplyBulkStartedAt(items, request.Value, out error),
            "endedat" => ApplyBulkEndedAt(items, request.Value, out error),
            "reasoncategory" => ApplyBulkReasonCategory(items, request.Value, out error),
            "reasondetail" => ApplyBulkReasonDetail(items, request.Value, out error),
            "isplanned" => ApplyBulkIsPlanned(items, request.Value, out error),
            "downtimehours" => ApplyBulkDowntimeHours(items, request.Value, out error),
            _ => FailBulkUpdate("Field is not bulk editable.", out error)
        };
    }

    private static bool ApplyBulkEquipmentId(IReadOnlyList<DowntimeEvent> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("EquipmentId requires a int value.", out error);
        foreach (var item in items) item.EquipmentId = value;
        return true;
    }

    private static bool ApplyBulkWorkOrderId(IReadOnlyList<DowntimeEvent> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.WorkOrderId = null;
            return true;
        }
        if (!long.TryParse(raw, out var value)) return FailBulkUpdate("WorkOrderId requires a long value.", out error);
        foreach (var item in items) item.WorkOrderId = value;
        return true;
    }

    private static bool ApplyBulkStartedAt(IReadOnlyList<DowntimeEvent> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!DateTime.TryParse(raw, out var value)) return FailBulkUpdate("StartedAt requires a DateTime value.", out error);
        foreach (var item in items) item.StartedAt = value;
        return true;
    }

    private static bool ApplyBulkEndedAt(IReadOnlyList<DowntimeEvent> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.EndedAt = null;
            return true;
        }
        if (!DateTime.TryParse(raw, out var value)) return FailBulkUpdate("EndedAt requires a DateTime value.", out error);
        foreach (var item in items) item.EndedAt = value;
        return true;
    }

    private static bool ApplyBulkReasonCategory(IReadOnlyList<DowntimeEvent> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.ReasonCategory = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkReasonDetail(IReadOnlyList<DowntimeEvent> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.ReasonDetail = null;
            return true;
        }
        foreach (var item in items) item.ReasonDetail = raw;
        return true;
    }

    private static bool ApplyBulkIsPlanned(IReadOnlyList<DowntimeEvent> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!bool.TryParse(raw, out var value)) return FailBulkUpdate("IsPlanned requires a boolean value.", out error);
        foreach (var item in items) item.IsPlanned = value;
        return true;
    }

    private static bool ApplyBulkDowntimeHours(IReadOnlyList<DowntimeEvent> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.DowntimeHours = null;
            return true;
        }
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("DowntimeHours requires a decimal value.", out error);
        foreach (var item in items) item.DowntimeHours = value;
        return true;
    }

    private static bool FailBulkUpdate(string message, out string error)
    {
        error = message;
        return false;
    }


    private static IQueryable<DowntimeEvent> ApplySearch(IQueryable<DowntimeEvent> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        search = search.Trim();
        return query.Where(x => (x.ReasonCategory != null && x.ReasonCategory.Contains(search)) || (x.ReasonDetail != null && x.ReasonDetail.Contains(search)));
    }

    private static IQueryable<DowntimeEvent> ApplyFilter(IQueryable<DowntimeEvent> query, string? filterField, string? filterValue)
    {
        if (string.IsNullOrWhiteSpace(filterField) || string.IsNullOrWhiteSpace(filterValue)) return query;
        filterField = filterField.Trim();
        filterValue = filterValue.Trim();
        return filterField.ToLowerInvariant() switch
        {
            "downtimeeventid" => long.TryParse(filterValue, out var DowntimeEventIdValue) ? query.Where(x => x.DowntimeEventId == DowntimeEventIdValue) : query,
            "equipmentid" => int.TryParse(filterValue, out var EquipmentIdValue) ? query.Where(x => x.EquipmentId == EquipmentIdValue) : query,
            "workorderid" => long.TryParse(filterValue, out var WorkOrderIdValue) ? query.Where(x => x.WorkOrderId == WorkOrderIdValue) : query,
            "startedat" => DateTime.TryParse(filterValue, out var StartedAtValue) ? query.Where(x => x.StartedAt == StartedAtValue) : query,
            "endedat" => DateTime.TryParse(filterValue, out var EndedAtValue) ? query.Where(x => x.EndedAt == EndedAtValue) : query,
            "reasoncategory" => query.Where(x => x.ReasonCategory != null && x.ReasonCategory.Contains(filterValue)),
            "reasondetail" => query.Where(x => x.ReasonDetail != null && x.ReasonDetail.Contains(filterValue)),
            "isplanned" => bool.TryParse(filterValue, out var IsPlannedValue) ? query.Where(x => x.IsPlanned == IsPlannedValue) : query,
            "downtimehours" => decimal.TryParse(filterValue, out var DowntimeHoursValue) ? query.Where(x => x.DowntimeHours == DowntimeHoursValue) : query,
            _ => query
        };
    }

    private static IQueryable<DowntimeEvent> ApplySort(IQueryable<DowntimeEvent> query, string? sortBy, string? sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) || string.Equals(sortDirection, "descending", StringComparison.OrdinalIgnoreCase);
        var field = string.IsNullOrWhiteSpace(sortBy) ? "DowntimeEventId" : sortBy.Trim();
        return field.ToLowerInvariant() switch
        {
            "downtimeeventid" => descending ? query.OrderByDescending(x => x.DowntimeEventId) : query.OrderBy(x => x.DowntimeEventId),
            "equipmentid" => descending ? query.OrderByDescending(x => x.EquipmentId) : query.OrderBy(x => x.EquipmentId),
            "workorderid" => descending ? query.OrderByDescending(x => x.WorkOrderId) : query.OrderBy(x => x.WorkOrderId),
            "startedat" => descending ? query.OrderByDescending(x => x.StartedAt) : query.OrderBy(x => x.StartedAt),
            "endedat" => descending ? query.OrderByDescending(x => x.EndedAt) : query.OrderBy(x => x.EndedAt),
            "reasoncategory" => descending ? query.OrderByDescending(x => x.ReasonCategory) : query.OrderBy(x => x.ReasonCategory),
            "reasondetail" => descending ? query.OrderByDescending(x => x.ReasonDetail) : query.OrderBy(x => x.ReasonDetail),
            "isplanned" => descending ? query.OrderByDescending(x => x.IsPlanned) : query.OrderBy(x => x.IsPlanned),
            "downtimehours" => descending ? query.OrderByDescending(x => x.DowntimeHours) : query.OrderBy(x => x.DowntimeHours),
            _ => descending ? query.OrderByDescending(x => x.DowntimeEventId) : query.OrderBy(x => x.DowntimeEventId)
        };
    }
    private static DowntimeEventDto ToDto(DowntimeEvent item) => new(
        item.DowntimeEventId,
        item.EquipmentId,
        item.WorkOrderId,
        item.StartedAt,
        item.EndedAt,
        item.ReasonCategory,
        item.ReasonDetail,
        item.IsPlanned,
        item.DowntimeHours
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
            Resource = "DowntimeEvent",
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
        _changes.Clients.All.SendAsync(DataChangeHub.DataChangedMethod, new DataChangeNotification("DowntimeEvent", action, resourceKey, DateTimeOffset.UtcNow), ct);

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
