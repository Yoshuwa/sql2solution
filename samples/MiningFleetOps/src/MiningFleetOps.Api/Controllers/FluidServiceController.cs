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
[Route("api/fluidServices")]
public sealed partial class FluidServiceController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<DataChangeHub> _changes;

    public FluidServiceController(AppDbContext db, IHubContext<DataChangeHub> changes)
    {
        _db = db;
        _changes = changes;
    }

    partial void OnBeforeCreate(CreateFluidServiceRequest request, FluidService item);
    partial void OnAfterCreate(FluidService item);
    partial void OnBeforeUpdate(FluidService item, UpdateFluidServiceRequest request);
    partial void OnBeforeDelete(FluidService item);

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<FluidServiceDto>>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null, [FromQuery] string? filterField = null, [FromQuery] string? filterValue = null, [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        IQueryable<FluidService> query = _db.Set<FluidService>().AsNoTracking();
        query = ApplySearch(query, search);
        query = ApplyFilter(query, filterField, filterValue);
        query = ApplySort(query, sortBy, sortDirection);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<FluidServiceDto>>.Success("records loaded", new PagedResult<FluidServiceDto>(items, page, pageSize, total)));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<FluidServiceDto>>> GetById(long id, CancellationToken ct)
    {
        IQueryable<FluidService> query = _db.Set<FluidService>().AsNoTracking();
        var item = await query.FirstOrDefaultAsync(x => x.FluidServiceId!.Equals(id), ct);
        return item is null ? NotFound(ApiResponse<object>.Warning("record not found")) : Ok(ApiResponse<FluidServiceDto>.Success("record loaded", ToDto(item)));
    }

    [HttpGet("{id}/history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuditTrailDto>>>> GetHistory(long id, CancellationToken ct)
    {
        var canReadRecord = await _db.Set<FluidService>().AsNoTracking().AnyAsync(x => x.FluidServiceId!.Equals(id), ct);
        if (!canReadRecord) return NotFound(ApiResponse<object>.Warning("record not found"));
        await EnsureAuditTrailTableAsync(ct);
        var resourceKey = Convert.ToString(id) ?? string.Empty;
        var history = await _db.AuditTrailEntries
            .AsNoTracking()
            .Where(entry => entry.Resource == "FluidService" && entry.ResourceKey == resourceKey)
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .Take(100)
            .Select(entry => ToAuditTrailDto(entry))
            .ToListAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<AuditTrailDto>>.Success("activity loaded", history));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<FluidServiceDto>>> Create(CreateFluidServiceRequest request, CancellationToken ct)
    {
        var item = new FluidService
        {
            EquipmentId = request.EquipmentId,
            FluidTypeId = request.FluidTypeId,
            ServicedAt = request.ServicedAt,
            HourMeter = request.HourMeter,
            LitersChanged = request.LitersChanged,
            FilterChanged = request.FilterChanged,
            WorkOrderId = request.WorkOrderId,
            TechnicianEmployeeId = request.TechnicianEmployeeId,
            NextDueHourMeter = request.NextDueHourMeter,
            Notes = request.Notes,
        };
        OnBeforeCreate(request, item);
        _db.Set<FluidService>().Add(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Created", Convert.ToString(item.FluidServiceId) ?? string.Empty, $"Created FluidService record {item.FluidServiceId}.", ToDto(item), ct);
        OnAfterCreate(item);
        await NotifyResourceChangedAsync("Created", Convert.ToString(item.FluidServiceId), ct);
        return CreatedAtAction(nameof(GetById), new { id = item.FluidServiceId }, ApiResponse<FluidServiceDto>.Success("record created", ToDto(item)));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, UpdateFluidServiceRequest request, CancellationToken ct)
    {
        var item = await _db.Set<FluidService>().FirstOrDefaultAsync(x => x.FluidServiceId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeUpdate(item, request);
        item.EquipmentId = request.EquipmentId;
        item.FluidTypeId = request.FluidTypeId;
        item.ServicedAt = request.ServicedAt;
        item.HourMeter = request.HourMeter;
        item.LitersChanged = request.LitersChanged;
        item.FilterChanged = request.FilterChanged;
        item.WorkOrderId = request.WorkOrderId;
        item.TechnicianEmployeeId = request.TechnicianEmployeeId;
        item.NextDueHourMeter = request.NextDueHourMeter;
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
        await LogAuditTrailAsync("Updated", Convert.ToString(item.FluidServiceId) ?? string.Empty, $"Updated FluidService record {item.FluidServiceId}.", auditChanges, ct);
        await NotifyResourceChangedAsync("Updated", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record updated", new { updated = 1 }));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch(long id, UpdateFluidServiceRequest request, CancellationToken ct)
    {
        return await Update(id, request, ct);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var item = await _db.Set<FluidService>().FirstOrDefaultAsync(x => x.FluidServiceId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeDelete(item);
        _db.Set<FluidService>().Remove(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Deleted", Convert.ToString(id) ?? string.Empty, $"Hard deleted FluidService record {id}.", ToDto(item), ct);
        await NotifyResourceChangedAsync("Deleted", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record deleted", new { deleted = 1, mode = "Hard" }));
    }

    [HttpPost("bulk/export")]
    public async Task<ActionResult<ApiResponse<PagedResult<FluidServiceDto>>>> ExportBulk(BulkIdsRequest request, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return Ok(ApiResponse<PagedResult<FluidServiceDto>>.Warning("no records selected", new PagedResult<FluidServiceDto>(Array.Empty<FluidServiceDto>(), page, pageSize, 0)));
        IQueryable<FluidService> query = _db.Set<FluidService>().AsNoTracking().Where(x => ids.Contains(x.FluidServiceId));
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<FluidServiceDto>>.Success("records exported", new PagedResult<FluidServiceDto>(items, page, pageSize, total)));
    }

    [HttpPatch("bulk")]
    public async Task<IActionResult> UpdateBulk(BulkUpdateRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        if (string.IsNullOrWhiteSpace(request.Field)) return BadRequest(ApiResponse<object>.Error("error", new { error = "Choose a field to update." }));
        IQueryable<FluidService> query = _db.Set<FluidService>().Where(x => ids.Contains(x.FluidServiceId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return NotFound(ApiResponse<object>.Warning("records not found"));
        if (!ApplyBulkUpdate(items, request, out var error)) return BadRequest(ApiResponse<object>.Error("error", new { error }));
        var auditChanges = items.ToDictionary(item => Convert.ToString(item.FluidServiceId) ?? string.Empty, item => GetEntityChanges(_db.Entry(item)));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Updated", Convert.ToString(item.FluidServiceId) ?? string.Empty, $"Updated FluidService record {item.FluidServiceId} in bulk update.", auditChanges[Convert.ToString(item.FluidServiceId) ?? string.Empty], ct);
        await NotifyResourceChangedAsync("Updated", null, ct);
        return Ok(ApiResponse<object>.Success("records updated", new { updated = items.Count }));
    }

    [HttpPost("bulk/delete")]
    public async Task<IActionResult> DeleteBulk(BulkIdsRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        IQueryable<FluidService> query = _db.Set<FluidService>().Where(x => ids.Contains(x.FluidServiceId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return Ok(ApiResponse<object>.Warning("records not found", new { deleted = 0 }));
        foreach (var item in items)
        {
            OnBeforeDelete(item);
        }
        _db.Set<FluidService>().RemoveRange(items);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Deleted", Convert.ToString(item.FluidServiceId) ?? string.Empty, $"Hard deleted FluidService record {item.FluidServiceId} in bulk delete.", ToDto(item), ct);
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

    private static bool ApplyBulkUpdate(IReadOnlyList<FluidService> items, BulkUpdateRequest request, out string error)
    {
        error = string.Empty;
        return request.Field.Trim().ToLowerInvariant() switch
        {
            "equipmentid" => ApplyBulkEquipmentId(items, request.Value, out error),
            "fluidtypeid" => ApplyBulkFluidTypeId(items, request.Value, out error),
            "servicedat" => ApplyBulkServicedAt(items, request.Value, out error),
            "hourmeter" => ApplyBulkHourMeter(items, request.Value, out error),
            "literschanged" => ApplyBulkLitersChanged(items, request.Value, out error),
            "filterchanged" => ApplyBulkFilterChanged(items, request.Value, out error),
            "workorderid" => ApplyBulkWorkOrderId(items, request.Value, out error),
            "technicianemployeeid" => ApplyBulkTechnicianEmployeeId(items, request.Value, out error),
            "nextduehourmeter" => ApplyBulkNextDueHourMeter(items, request.Value, out error),
            "notes" => ApplyBulkNotes(items, request.Value, out error),
            _ => FailBulkUpdate("Field is not bulk editable.", out error)
        };
    }

    private static bool ApplyBulkEquipmentId(IReadOnlyList<FluidService> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("EquipmentId requires a int value.", out error);
        foreach (var item in items) item.EquipmentId = value;
        return true;
    }

    private static bool ApplyBulkFluidTypeId(IReadOnlyList<FluidService> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("FluidTypeId requires a int value.", out error);
        foreach (var item in items) item.FluidTypeId = value;
        return true;
    }

    private static bool ApplyBulkServicedAt(IReadOnlyList<FluidService> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!DateTime.TryParse(raw, out var value)) return FailBulkUpdate("ServicedAt requires a DateTime value.", out error);
        foreach (var item in items) item.ServicedAt = value;
        return true;
    }

    private static bool ApplyBulkHourMeter(IReadOnlyList<FluidService> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("HourMeter requires a decimal value.", out error);
        foreach (var item in items) item.HourMeter = value;
        return true;
    }

    private static bool ApplyBulkLitersChanged(IReadOnlyList<FluidService> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("LitersChanged requires a decimal value.", out error);
        foreach (var item in items) item.LitersChanged = value;
        return true;
    }

    private static bool ApplyBulkFilterChanged(IReadOnlyList<FluidService> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!bool.TryParse(raw, out var value)) return FailBulkUpdate("FilterChanged requires a boolean value.", out error);
        foreach (var item in items) item.FilterChanged = value;
        return true;
    }

    private static bool ApplyBulkWorkOrderId(IReadOnlyList<FluidService> items, string? raw, out string error)
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

    private static bool ApplyBulkTechnicianEmployeeId(IReadOnlyList<FluidService> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.TechnicianEmployeeId = null;
            return true;
        }
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("TechnicianEmployeeId requires a int value.", out error);
        foreach (var item in items) item.TechnicianEmployeeId = value;
        return true;
    }

    private static bool ApplyBulkNextDueHourMeter(IReadOnlyList<FluidService> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.NextDueHourMeter = null;
            return true;
        }
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("NextDueHourMeter requires a decimal value.", out error);
        foreach (var item in items) item.NextDueHourMeter = value;
        return true;
    }

    private static bool ApplyBulkNotes(IReadOnlyList<FluidService> items, string? raw, out string error)
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


    private static IQueryable<FluidService> ApplySearch(IQueryable<FluidService> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        search = search.Trim();
        return query.Where(x => (x.Notes != null && x.Notes.Contains(search)));
    }

    private static IQueryable<FluidService> ApplyFilter(IQueryable<FluidService> query, string? filterField, string? filterValue)
    {
        if (string.IsNullOrWhiteSpace(filterField) || string.IsNullOrWhiteSpace(filterValue)) return query;
        filterField = filterField.Trim();
        filterValue = filterValue.Trim();
        return filterField.ToLowerInvariant() switch
        {
            "fluidserviceid" => long.TryParse(filterValue, out var FluidServiceIdValue) ? query.Where(x => x.FluidServiceId == FluidServiceIdValue) : query,
            "equipmentid" => int.TryParse(filterValue, out var EquipmentIdValue) ? query.Where(x => x.EquipmentId == EquipmentIdValue) : query,
            "fluidtypeid" => int.TryParse(filterValue, out var FluidTypeIdValue) ? query.Where(x => x.FluidTypeId == FluidTypeIdValue) : query,
            "servicedat" => DateTime.TryParse(filterValue, out var ServicedAtValue) ? query.Where(x => x.ServicedAt == ServicedAtValue) : query,
            "hourmeter" => decimal.TryParse(filterValue, out var HourMeterValue) ? query.Where(x => x.HourMeter == HourMeterValue) : query,
            "literschanged" => decimal.TryParse(filterValue, out var LitersChangedValue) ? query.Where(x => x.LitersChanged == LitersChangedValue) : query,
            "filterchanged" => bool.TryParse(filterValue, out var FilterChangedValue) ? query.Where(x => x.FilterChanged == FilterChangedValue) : query,
            "workorderid" => long.TryParse(filterValue, out var WorkOrderIdValue) ? query.Where(x => x.WorkOrderId == WorkOrderIdValue) : query,
            "technicianemployeeid" => int.TryParse(filterValue, out var TechnicianEmployeeIdValue) ? query.Where(x => x.TechnicianEmployeeId == TechnicianEmployeeIdValue) : query,
            "nextduehourmeter" => decimal.TryParse(filterValue, out var NextDueHourMeterValue) ? query.Where(x => x.NextDueHourMeter == NextDueHourMeterValue) : query,
            "notes" => query.Where(x => x.Notes != null && x.Notes.Contains(filterValue)),
            _ => query
        };
    }

    private static IQueryable<FluidService> ApplySort(IQueryable<FluidService> query, string? sortBy, string? sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) || string.Equals(sortDirection, "descending", StringComparison.OrdinalIgnoreCase);
        var field = string.IsNullOrWhiteSpace(sortBy) ? "FluidServiceId" : sortBy.Trim();
        return field.ToLowerInvariant() switch
        {
            "fluidserviceid" => descending ? query.OrderByDescending(x => x.FluidServiceId) : query.OrderBy(x => x.FluidServiceId),
            "equipmentid" => descending ? query.OrderByDescending(x => x.EquipmentId) : query.OrderBy(x => x.EquipmentId),
            "fluidtypeid" => descending ? query.OrderByDescending(x => x.FluidTypeId) : query.OrderBy(x => x.FluidTypeId),
            "servicedat" => descending ? query.OrderByDescending(x => x.ServicedAt) : query.OrderBy(x => x.ServicedAt),
            "hourmeter" => descending ? query.OrderByDescending(x => x.HourMeter) : query.OrderBy(x => x.HourMeter),
            "literschanged" => descending ? query.OrderByDescending(x => x.LitersChanged) : query.OrderBy(x => x.LitersChanged),
            "filterchanged" => descending ? query.OrderByDescending(x => x.FilterChanged) : query.OrderBy(x => x.FilterChanged),
            "workorderid" => descending ? query.OrderByDescending(x => x.WorkOrderId) : query.OrderBy(x => x.WorkOrderId),
            "technicianemployeeid" => descending ? query.OrderByDescending(x => x.TechnicianEmployeeId) : query.OrderBy(x => x.TechnicianEmployeeId),
            "nextduehourmeter" => descending ? query.OrderByDescending(x => x.NextDueHourMeter) : query.OrderBy(x => x.NextDueHourMeter),
            "notes" => descending ? query.OrderByDescending(x => x.Notes) : query.OrderBy(x => x.Notes),
            _ => descending ? query.OrderByDescending(x => x.FluidServiceId) : query.OrderBy(x => x.FluidServiceId)
        };
    }
    private static FluidServiceDto ToDto(FluidService item) => new(
        item.FluidServiceId,
        item.EquipmentId,
        item.FluidTypeId,
        item.ServicedAt,
        item.HourMeter,
        item.LitersChanged,
        item.FilterChanged,
        item.WorkOrderId,
        item.TechnicianEmployeeId,
        item.NextDueHourMeter,
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
            Resource = "FluidService",
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
        _changes.Clients.All.SendAsync(DataChangeHub.DataChangedMethod, new DataChangeNotification("FluidService", action, resourceKey, DateTimeOffset.UtcNow), ct);

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
