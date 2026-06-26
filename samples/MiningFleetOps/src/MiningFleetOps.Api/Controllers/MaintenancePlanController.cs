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
[Route("api/maintenancePlans")]
public sealed partial class MaintenancePlanController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<DataChangeHub> _changes;

    public MaintenancePlanController(AppDbContext db, IHubContext<DataChangeHub> changes)
    {
        _db = db;
        _changes = changes;
    }

    partial void OnBeforeCreate(CreateMaintenancePlanRequest request, MaintenancePlan item);
    partial void OnAfterCreate(MaintenancePlan item);
    partial void OnBeforeUpdate(MaintenancePlan item, UpdateMaintenancePlanRequest request);
    partial void OnBeforeDelete(MaintenancePlan item);

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<MaintenancePlanDto>>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null, [FromQuery] string? filterField = null, [FromQuery] string? filterValue = null, [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        IQueryable<MaintenancePlan> query = _db.Set<MaintenancePlan>().AsNoTracking();
        query = ApplySearch(query, search);
        query = ApplyFilter(query, filterField, filterValue);
        query = ApplySort(query, sortBy, sortDirection);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<MaintenancePlanDto>>.Success("records loaded", new PagedResult<MaintenancePlanDto>(items, page, pageSize, total)));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<MaintenancePlanDto>>> GetById(int id, CancellationToken ct)
    {
        IQueryable<MaintenancePlan> query = _db.Set<MaintenancePlan>().AsNoTracking();
        var item = await query.FirstOrDefaultAsync(x => x.MaintenancePlanId!.Equals(id), ct);
        return item is null ? NotFound(ApiResponse<object>.Warning("record not found")) : Ok(ApiResponse<MaintenancePlanDto>.Success("record loaded", ToDto(item)));
    }

    [HttpGet("{id}/history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuditTrailDto>>>> GetHistory(int id, CancellationToken ct)
    {
        var canReadRecord = await _db.Set<MaintenancePlan>().AsNoTracking().AnyAsync(x => x.MaintenancePlanId!.Equals(id), ct);
        if (!canReadRecord) return NotFound(ApiResponse<object>.Warning("record not found"));
        await EnsureAuditTrailTableAsync(ct);
        var resourceKey = Convert.ToString(id) ?? string.Empty;
        var history = await _db.AuditTrailEntries
            .AsNoTracking()
            .Where(entry => entry.Resource == "MaintenancePlan" && entry.ResourceKey == resourceKey)
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .Take(100)
            .Select(entry => ToAuditTrailDto(entry))
            .ToListAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<AuditTrailDto>>.Success("activity loaded", history));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<MaintenancePlanDto>>> Create(CreateMaintenancePlanRequest request, CancellationToken ct)
    {
        var item = new MaintenancePlan
        {
            EquipmentClassId = request.EquipmentClassId,
            PlanCode = request.PlanCode,
            PlanName = request.PlanName,
            IntervalHours = request.IntervalHours,
            IntervalDays = request.IntervalDays,
            EstimatedDurationHours = request.EstimatedDurationHours,
            IsActive = request.IsActive,
        };
        OnBeforeCreate(request, item);
        _db.Set<MaintenancePlan>().Add(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Created", Convert.ToString(item.MaintenancePlanId) ?? string.Empty, $"Created MaintenancePlan record {item.MaintenancePlanId}.", ToDto(item), ct);
        OnAfterCreate(item);
        await NotifyResourceChangedAsync("Created", Convert.ToString(item.MaintenancePlanId), ct);
        return CreatedAtAction(nameof(GetById), new { id = item.MaintenancePlanId }, ApiResponse<MaintenancePlanDto>.Success("record created", ToDto(item)));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateMaintenancePlanRequest request, CancellationToken ct)
    {
        var item = await _db.Set<MaintenancePlan>().FirstOrDefaultAsync(x => x.MaintenancePlanId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeUpdate(item, request);
        item.EquipmentClassId = request.EquipmentClassId;
        item.PlanCode = request.PlanCode;
        item.PlanName = request.PlanName;
        item.IntervalHours = request.IntervalHours;
        item.IntervalDays = request.IntervalDays;
        item.EstimatedDurationHours = request.EstimatedDurationHours;
        item.IsActive = request.IsActive;
        var auditChanges = GetEntityChanges(_db.Entry(item));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Updated", Convert.ToString(item.MaintenancePlanId) ?? string.Empty, $"Updated MaintenancePlan record {item.MaintenancePlanId}.", auditChanges, ct);
        await NotifyResourceChangedAsync("Updated", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record updated", new { updated = 1 }));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch(int id, UpdateMaintenancePlanRequest request, CancellationToken ct)
    {
        return await Update(id, request, ct);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var item = await _db.Set<MaintenancePlan>().FirstOrDefaultAsync(x => x.MaintenancePlanId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeDelete(item);
        _db.Set<MaintenancePlan>().Remove(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Deleted", Convert.ToString(id) ?? string.Empty, $"Hard deleted MaintenancePlan record {id}.", ToDto(item), ct);
        await NotifyResourceChangedAsync("Deleted", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record deleted", new { deleted = 1, mode = "Hard" }));
    }

    [HttpPost("bulk/export")]
    public async Task<ActionResult<ApiResponse<PagedResult<MaintenancePlanDto>>>> ExportBulk(BulkIdsRequest request, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return Ok(ApiResponse<PagedResult<MaintenancePlanDto>>.Warning("no records selected", new PagedResult<MaintenancePlanDto>(Array.Empty<MaintenancePlanDto>(), page, pageSize, 0)));
        IQueryable<MaintenancePlan> query = _db.Set<MaintenancePlan>().AsNoTracking().Where(x => ids.Contains(x.MaintenancePlanId));
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<MaintenancePlanDto>>.Success("records exported", new PagedResult<MaintenancePlanDto>(items, page, pageSize, total)));
    }

    [HttpPatch("bulk")]
    public async Task<IActionResult> UpdateBulk(BulkUpdateRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        if (string.IsNullOrWhiteSpace(request.Field)) return BadRequest(ApiResponse<object>.Error("error", new { error = "Choose a field to update." }));
        IQueryable<MaintenancePlan> query = _db.Set<MaintenancePlan>().Where(x => ids.Contains(x.MaintenancePlanId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return NotFound(ApiResponse<object>.Warning("records not found"));
        if (!ApplyBulkUpdate(items, request, out var error)) return BadRequest(ApiResponse<object>.Error("error", new { error }));
        var auditChanges = items.ToDictionary(item => Convert.ToString(item.MaintenancePlanId) ?? string.Empty, item => GetEntityChanges(_db.Entry(item)));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Updated", Convert.ToString(item.MaintenancePlanId) ?? string.Empty, $"Updated MaintenancePlan record {item.MaintenancePlanId} in bulk update.", auditChanges[Convert.ToString(item.MaintenancePlanId) ?? string.Empty], ct);
        await NotifyResourceChangedAsync("Updated", null, ct);
        return Ok(ApiResponse<object>.Success("records updated", new { updated = items.Count }));
    }

    [HttpPost("bulk/delete")]
    public async Task<IActionResult> DeleteBulk(BulkIdsRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        IQueryable<MaintenancePlan> query = _db.Set<MaintenancePlan>().Where(x => ids.Contains(x.MaintenancePlanId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return Ok(ApiResponse<object>.Warning("records not found", new { deleted = 0 }));
        foreach (var item in items)
        {
            OnBeforeDelete(item);
        }
        _db.Set<MaintenancePlan>().RemoveRange(items);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Deleted", Convert.ToString(item.MaintenancePlanId) ?? string.Empty, $"Hard deleted MaintenancePlan record {item.MaintenancePlanId} in bulk delete.", ToDto(item), ct);
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

    private static bool ApplyBulkUpdate(IReadOnlyList<MaintenancePlan> items, BulkUpdateRequest request, out string error)
    {
        error = string.Empty;
        return request.Field.Trim().ToLowerInvariant() switch
        {
            "equipmentclassid" => ApplyBulkEquipmentClassId(items, request.Value, out error),
            "plancode" => ApplyBulkPlanCode(items, request.Value, out error),
            "planname" => ApplyBulkPlanName(items, request.Value, out error),
            "intervalhours" => ApplyBulkIntervalHours(items, request.Value, out error),
            "intervaldays" => ApplyBulkIntervalDays(items, request.Value, out error),
            "estimateddurationhours" => ApplyBulkEstimatedDurationHours(items, request.Value, out error),
            "isactive" => ApplyBulkIsActive(items, request.Value, out error),
            _ => FailBulkUpdate("Field is not bulk editable.", out error)
        };
    }

    private static bool ApplyBulkEquipmentClassId(IReadOnlyList<MaintenancePlan> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("EquipmentClassId requires a int value.", out error);
        foreach (var item in items) item.EquipmentClassId = value;
        return true;
    }

    private static bool ApplyBulkPlanCode(IReadOnlyList<MaintenancePlan> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.PlanCode = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkPlanName(IReadOnlyList<MaintenancePlan> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.PlanName = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkIntervalHours(IReadOnlyList<MaintenancePlan> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.IntervalHours = null;
            return true;
        }
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("IntervalHours requires a decimal value.", out error);
        foreach (var item in items) item.IntervalHours = value;
        return true;
    }

    private static bool ApplyBulkIntervalDays(IReadOnlyList<MaintenancePlan> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.IntervalDays = null;
            return true;
        }
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("IntervalDays requires a int value.", out error);
        foreach (var item in items) item.IntervalDays = value;
        return true;
    }

    private static bool ApplyBulkEstimatedDurationHours(IReadOnlyList<MaintenancePlan> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("EstimatedDurationHours requires a decimal value.", out error);
        foreach (var item in items) item.EstimatedDurationHours = value;
        return true;
    }

    private static bool ApplyBulkIsActive(IReadOnlyList<MaintenancePlan> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!bool.TryParse(raw, out var value)) return FailBulkUpdate("IsActive requires a boolean value.", out error);
        foreach (var item in items) item.IsActive = value;
        return true;
    }

    private static bool FailBulkUpdate(string message, out string error)
    {
        error = message;
        return false;
    }


    private static IQueryable<MaintenancePlan> ApplySearch(IQueryable<MaintenancePlan> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        search = search.Trim();
        return query.Where(x => (x.PlanCode != null && x.PlanCode.Contains(search)) || (x.PlanName != null && x.PlanName.Contains(search)));
    }

    private static IQueryable<MaintenancePlan> ApplyFilter(IQueryable<MaintenancePlan> query, string? filterField, string? filterValue)
    {
        if (string.IsNullOrWhiteSpace(filterField) || string.IsNullOrWhiteSpace(filterValue)) return query;
        filterField = filterField.Trim();
        filterValue = filterValue.Trim();
        return filterField.ToLowerInvariant() switch
        {
            "maintenanceplanid" => int.TryParse(filterValue, out var MaintenancePlanIdValue) ? query.Where(x => x.MaintenancePlanId == MaintenancePlanIdValue) : query,
            "equipmentclassid" => int.TryParse(filterValue, out var EquipmentClassIdValue) ? query.Where(x => x.EquipmentClassId == EquipmentClassIdValue) : query,
            "plancode" => query.Where(x => x.PlanCode != null && x.PlanCode.Contains(filterValue)),
            "planname" => query.Where(x => x.PlanName != null && x.PlanName.Contains(filterValue)),
            "intervalhours" => decimal.TryParse(filterValue, out var IntervalHoursValue) ? query.Where(x => x.IntervalHours == IntervalHoursValue) : query,
            "intervaldays" => int.TryParse(filterValue, out var IntervalDaysValue) ? query.Where(x => x.IntervalDays == IntervalDaysValue) : query,
            "estimateddurationhours" => decimal.TryParse(filterValue, out var EstimatedDurationHoursValue) ? query.Where(x => x.EstimatedDurationHours == EstimatedDurationHoursValue) : query,
            "isactive" => bool.TryParse(filterValue, out var IsActiveValue) ? query.Where(x => x.IsActive == IsActiveValue) : query,
            _ => query
        };
    }

    private static IQueryable<MaintenancePlan> ApplySort(IQueryable<MaintenancePlan> query, string? sortBy, string? sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) || string.Equals(sortDirection, "descending", StringComparison.OrdinalIgnoreCase);
        var field = string.IsNullOrWhiteSpace(sortBy) ? "MaintenancePlanId" : sortBy.Trim();
        return field.ToLowerInvariant() switch
        {
            "maintenanceplanid" => descending ? query.OrderByDescending(x => x.MaintenancePlanId) : query.OrderBy(x => x.MaintenancePlanId),
            "equipmentclassid" => descending ? query.OrderByDescending(x => x.EquipmentClassId) : query.OrderBy(x => x.EquipmentClassId),
            "plancode" => descending ? query.OrderByDescending(x => x.PlanCode) : query.OrderBy(x => x.PlanCode),
            "planname" => descending ? query.OrderByDescending(x => x.PlanName) : query.OrderBy(x => x.PlanName),
            "intervalhours" => descending ? query.OrderByDescending(x => x.IntervalHours) : query.OrderBy(x => x.IntervalHours),
            "intervaldays" => descending ? query.OrderByDescending(x => x.IntervalDays) : query.OrderBy(x => x.IntervalDays),
            "estimateddurationhours" => descending ? query.OrderByDescending(x => x.EstimatedDurationHours) : query.OrderBy(x => x.EstimatedDurationHours),
            "isactive" => descending ? query.OrderByDescending(x => x.IsActive) : query.OrderBy(x => x.IsActive),
            _ => descending ? query.OrderByDescending(x => x.MaintenancePlanId) : query.OrderBy(x => x.MaintenancePlanId)
        };
    }
    private static MaintenancePlanDto ToDto(MaintenancePlan item) => new(
        item.MaintenancePlanId,
        item.EquipmentClassId,
        item.PlanCode,
        item.PlanName,
        item.IntervalHours,
        item.IntervalDays,
        item.EstimatedDurationHours,
        item.IsActive
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
            Resource = "MaintenancePlan",
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
        _changes.Clients.All.SendAsync(DataChangeHub.DataChangedMethod, new DataChangeNotification("MaintenancePlan", action, resourceKey, DateTimeOffset.UtcNow), ct);

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
