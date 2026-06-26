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
[Route("api/workOrderParts")]
public sealed partial class WorkOrderPartController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<DataChangeHub> _changes;

    public WorkOrderPartController(AppDbContext db, IHubContext<DataChangeHub> changes)
    {
        _db = db;
        _changes = changes;
    }

    partial void OnBeforeCreate(CreateWorkOrderPartRequest request, WorkOrderPart item);
    partial void OnAfterCreate(WorkOrderPart item);
    partial void OnBeforeUpdate(WorkOrderPart item, UpdateWorkOrderPartRequest request);
    partial void OnBeforeDelete(WorkOrderPart item);

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<WorkOrderPartDto>>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null, [FromQuery] string? filterField = null, [FromQuery] string? filterValue = null, [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        IQueryable<WorkOrderPart> query = _db.Set<WorkOrderPart>().AsNoTracking();
        query = ApplySearch(query, search);
        query = ApplyFilter(query, filterField, filterValue);
        query = ApplySort(query, sortBy, sortDirection);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<WorkOrderPartDto>>.Success("records loaded", new PagedResult<WorkOrderPartDto>(items, page, pageSize, total)));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<WorkOrderPartDto>>> GetById(long id, CancellationToken ct)
    {
        IQueryable<WorkOrderPart> query = _db.Set<WorkOrderPart>().AsNoTracking();
        var item = await query.FirstOrDefaultAsync(x => x.WorkOrderPartId!.Equals(id), ct);
        return item is null ? NotFound(ApiResponse<object>.Warning("record not found")) : Ok(ApiResponse<WorkOrderPartDto>.Success("record loaded", ToDto(item)));
    }

    [HttpGet("{id}/history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuditTrailDto>>>> GetHistory(long id, CancellationToken ct)
    {
        var canReadRecord = await _db.Set<WorkOrderPart>().AsNoTracking().AnyAsync(x => x.WorkOrderPartId!.Equals(id), ct);
        if (!canReadRecord) return NotFound(ApiResponse<object>.Warning("record not found"));
        await EnsureAuditTrailTableAsync(ct);
        var resourceKey = Convert.ToString(id) ?? string.Empty;
        var history = await _db.AuditTrailEntries
            .AsNoTracking()
            .Where(entry => entry.Resource == "WorkOrderPart" && entry.ResourceKey == resourceKey)
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .Take(100)
            .Select(entry => ToAuditTrailDto(entry))
            .ToListAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<AuditTrailDto>>.Success("activity loaded", history));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<WorkOrderPartDto>>> Create(CreateWorkOrderPartRequest request, CancellationToken ct)
    {
        var item = new WorkOrderPart
        {
            WorkOrderId = request.WorkOrderId,
            PartId = request.PartId,
            QuantityUsed = request.QuantityUsed,
            UnitCost = request.UnitCost,
            LineCost = request.LineCost,
        };
        OnBeforeCreate(request, item);
        _db.Set<WorkOrderPart>().Add(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Created", Convert.ToString(item.WorkOrderPartId) ?? string.Empty, $"Created WorkOrderPart record {item.WorkOrderPartId}.", ToDto(item), ct);
        OnAfterCreate(item);
        await NotifyResourceChangedAsync("Created", Convert.ToString(item.WorkOrderPartId), ct);
        return CreatedAtAction(nameof(GetById), new { id = item.WorkOrderPartId }, ApiResponse<WorkOrderPartDto>.Success("record created", ToDto(item)));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, UpdateWorkOrderPartRequest request, CancellationToken ct)
    {
        var item = await _db.Set<WorkOrderPart>().FirstOrDefaultAsync(x => x.WorkOrderPartId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeUpdate(item, request);
        item.WorkOrderId = request.WorkOrderId;
        item.PartId = request.PartId;
        item.QuantityUsed = request.QuantityUsed;
        item.UnitCost = request.UnitCost;
        item.LineCost = request.LineCost;
        var auditChanges = GetEntityChanges(_db.Entry(item));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Updated", Convert.ToString(item.WorkOrderPartId) ?? string.Empty, $"Updated WorkOrderPart record {item.WorkOrderPartId}.", auditChanges, ct);
        await NotifyResourceChangedAsync("Updated", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record updated", new { updated = 1 }));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch(long id, UpdateWorkOrderPartRequest request, CancellationToken ct)
    {
        return await Update(id, request, ct);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var item = await _db.Set<WorkOrderPart>().FirstOrDefaultAsync(x => x.WorkOrderPartId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeDelete(item);
        _db.Set<WorkOrderPart>().Remove(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Deleted", Convert.ToString(id) ?? string.Empty, $"Hard deleted WorkOrderPart record {id}.", ToDto(item), ct);
        await NotifyResourceChangedAsync("Deleted", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record deleted", new { deleted = 1, mode = "Hard" }));
    }

    [HttpPost("bulk/export")]
    public async Task<ActionResult<ApiResponse<PagedResult<WorkOrderPartDto>>>> ExportBulk(BulkIdsRequest request, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return Ok(ApiResponse<PagedResult<WorkOrderPartDto>>.Warning("no records selected", new PagedResult<WorkOrderPartDto>(Array.Empty<WorkOrderPartDto>(), page, pageSize, 0)));
        IQueryable<WorkOrderPart> query = _db.Set<WorkOrderPart>().AsNoTracking().Where(x => ids.Contains(x.WorkOrderPartId));
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<WorkOrderPartDto>>.Success("records exported", new PagedResult<WorkOrderPartDto>(items, page, pageSize, total)));
    }

    [HttpPatch("bulk")]
    public async Task<IActionResult> UpdateBulk(BulkUpdateRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        if (string.IsNullOrWhiteSpace(request.Field)) return BadRequest(ApiResponse<object>.Error("error", new { error = "Choose a field to update." }));
        IQueryable<WorkOrderPart> query = _db.Set<WorkOrderPart>().Where(x => ids.Contains(x.WorkOrderPartId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return NotFound(ApiResponse<object>.Warning("records not found"));
        if (!ApplyBulkUpdate(items, request, out var error)) return BadRequest(ApiResponse<object>.Error("error", new { error }));
        var auditChanges = items.ToDictionary(item => Convert.ToString(item.WorkOrderPartId) ?? string.Empty, item => GetEntityChanges(_db.Entry(item)));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Updated", Convert.ToString(item.WorkOrderPartId) ?? string.Empty, $"Updated WorkOrderPart record {item.WorkOrderPartId} in bulk update.", auditChanges[Convert.ToString(item.WorkOrderPartId) ?? string.Empty], ct);
        await NotifyResourceChangedAsync("Updated", null, ct);
        return Ok(ApiResponse<object>.Success("records updated", new { updated = items.Count }));
    }

    [HttpPost("bulk/delete")]
    public async Task<IActionResult> DeleteBulk(BulkIdsRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        IQueryable<WorkOrderPart> query = _db.Set<WorkOrderPart>().Where(x => ids.Contains(x.WorkOrderPartId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return Ok(ApiResponse<object>.Warning("records not found", new { deleted = 0 }));
        foreach (var item in items)
        {
            OnBeforeDelete(item);
        }
        _db.Set<WorkOrderPart>().RemoveRange(items);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Deleted", Convert.ToString(item.WorkOrderPartId) ?? string.Empty, $"Hard deleted WorkOrderPart record {item.WorkOrderPartId} in bulk delete.", ToDto(item), ct);
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

    private static bool ApplyBulkUpdate(IReadOnlyList<WorkOrderPart> items, BulkUpdateRequest request, out string error)
    {
        error = string.Empty;
        return request.Field.Trim().ToLowerInvariant() switch
        {
            "workorderid" => ApplyBulkWorkOrderId(items, request.Value, out error),
            "partid" => ApplyBulkPartId(items, request.Value, out error),
            "quantityused" => ApplyBulkQuantityUsed(items, request.Value, out error),
            "unitcost" => ApplyBulkUnitCost(items, request.Value, out error),
            "linecost" => ApplyBulkLineCost(items, request.Value, out error),
            _ => FailBulkUpdate("Field is not bulk editable.", out error)
        };
    }

    private static bool ApplyBulkWorkOrderId(IReadOnlyList<WorkOrderPart> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!long.TryParse(raw, out var value)) return FailBulkUpdate("WorkOrderId requires a long value.", out error);
        foreach (var item in items) item.WorkOrderId = value;
        return true;
    }

    private static bool ApplyBulkPartId(IReadOnlyList<WorkOrderPart> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("PartId requires a int value.", out error);
        foreach (var item in items) item.PartId = value;
        return true;
    }

    private static bool ApplyBulkQuantityUsed(IReadOnlyList<WorkOrderPart> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("QuantityUsed requires a decimal value.", out error);
        foreach (var item in items) item.QuantityUsed = value;
        return true;
    }

    private static bool ApplyBulkUnitCost(IReadOnlyList<WorkOrderPart> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("UnitCost requires a decimal value.", out error);
        foreach (var item in items) item.UnitCost = value;
        return true;
    }

    private static bool ApplyBulkLineCost(IReadOnlyList<WorkOrderPart> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.LineCost = null;
            return true;
        }
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("LineCost requires a decimal value.", out error);
        foreach (var item in items) item.LineCost = value;
        return true;
    }

    private static bool FailBulkUpdate(string message, out string error)
    {
        error = message;
        return false;
    }


    private static IQueryable<WorkOrderPart> ApplySearch(IQueryable<WorkOrderPart> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        search = search.Trim();
        return query;
    }

    private static IQueryable<WorkOrderPart> ApplyFilter(IQueryable<WorkOrderPart> query, string? filterField, string? filterValue)
    {
        if (string.IsNullOrWhiteSpace(filterField) || string.IsNullOrWhiteSpace(filterValue)) return query;
        filterField = filterField.Trim();
        filterValue = filterValue.Trim();
        return filterField.ToLowerInvariant() switch
        {
            "workorderpartid" => long.TryParse(filterValue, out var WorkOrderPartIdValue) ? query.Where(x => x.WorkOrderPartId == WorkOrderPartIdValue) : query,
            "workorderid" => long.TryParse(filterValue, out var WorkOrderIdValue) ? query.Where(x => x.WorkOrderId == WorkOrderIdValue) : query,
            "partid" => int.TryParse(filterValue, out var PartIdValue) ? query.Where(x => x.PartId == PartIdValue) : query,
            "quantityused" => decimal.TryParse(filterValue, out var QuantityUsedValue) ? query.Where(x => x.QuantityUsed == QuantityUsedValue) : query,
            "unitcost" => decimal.TryParse(filterValue, out var UnitCostValue) ? query.Where(x => x.UnitCost == UnitCostValue) : query,
            "linecost" => decimal.TryParse(filterValue, out var LineCostValue) ? query.Where(x => x.LineCost == LineCostValue) : query,
            _ => query
        };
    }

    private static IQueryable<WorkOrderPart> ApplySort(IQueryable<WorkOrderPart> query, string? sortBy, string? sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) || string.Equals(sortDirection, "descending", StringComparison.OrdinalIgnoreCase);
        var field = string.IsNullOrWhiteSpace(sortBy) ? "WorkOrderPartId" : sortBy.Trim();
        return field.ToLowerInvariant() switch
        {
            "workorderpartid" => descending ? query.OrderByDescending(x => x.WorkOrderPartId) : query.OrderBy(x => x.WorkOrderPartId),
            "workorderid" => descending ? query.OrderByDescending(x => x.WorkOrderId) : query.OrderBy(x => x.WorkOrderId),
            "partid" => descending ? query.OrderByDescending(x => x.PartId) : query.OrderBy(x => x.PartId),
            "quantityused" => descending ? query.OrderByDescending(x => x.QuantityUsed) : query.OrderBy(x => x.QuantityUsed),
            "unitcost" => descending ? query.OrderByDescending(x => x.UnitCost) : query.OrderBy(x => x.UnitCost),
            "linecost" => descending ? query.OrderByDescending(x => x.LineCost) : query.OrderBy(x => x.LineCost),
            _ => descending ? query.OrderByDescending(x => x.WorkOrderPartId) : query.OrderBy(x => x.WorkOrderPartId)
        };
    }
    private static WorkOrderPartDto ToDto(WorkOrderPart item) => new(
        item.WorkOrderPartId,
        item.WorkOrderId,
        item.PartId,
        item.QuantityUsed,
        item.UnitCost,
        item.LineCost
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
            Resource = "WorkOrderPart",
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
        _changes.Clients.All.SendAsync(DataChangeHub.DataChangedMethod, new DataChangeNotification("WorkOrderPart", action, resourceKey, DateTimeOffset.UtcNow), ct);

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
