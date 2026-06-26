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
[Route("api/parts")]
public sealed partial class PartController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<DataChangeHub> _changes;

    public PartController(AppDbContext db, IHubContext<DataChangeHub> changes)
    {
        _db = db;
        _changes = changes;
    }

    partial void OnBeforeCreate(CreatePartRequest request, Part item);
    partial void OnAfterCreate(Part item);
    partial void OnBeforeUpdate(Part item, UpdatePartRequest request);
    partial void OnBeforeDelete(Part item);

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<PartDto>>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null, [FromQuery] string? filterField = null, [FromQuery] string? filterValue = null, [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        IQueryable<Part> query = _db.Set<Part>().AsNoTracking();
        query = ApplySearch(query, search);
        query = ApplyFilter(query, filterField, filterValue);
        query = ApplySort(query, sortBy, sortDirection);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<PartDto>>.Success("records loaded", new PagedResult<PartDto>(items, page, pageSize, total)));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<PartDto>>> GetById(int id, CancellationToken ct)
    {
        IQueryable<Part> query = _db.Set<Part>().AsNoTracking();
        var item = await query.FirstOrDefaultAsync(x => x.PartId!.Equals(id), ct);
        return item is null ? NotFound(ApiResponse<object>.Warning("record not found")) : Ok(ApiResponse<PartDto>.Success("record loaded", ToDto(item)));
    }

    [HttpGet("{id}/history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuditTrailDto>>>> GetHistory(int id, CancellationToken ct)
    {
        var canReadRecord = await _db.Set<Part>().AsNoTracking().AnyAsync(x => x.PartId!.Equals(id), ct);
        if (!canReadRecord) return NotFound(ApiResponse<object>.Warning("record not found"));
        await EnsureAuditTrailTableAsync(ct);
        var resourceKey = Convert.ToString(id) ?? string.Empty;
        var history = await _db.AuditTrailEntries
            .AsNoTracking()
            .Where(entry => entry.Resource == "Part" && entry.ResourceKey == resourceKey)
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .Take(100)
            .Select(entry => ToAuditTrailDto(entry))
            .ToListAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<AuditTrailDto>>.Success("activity loaded", history));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<PartDto>>> Create(CreatePartRequest request, CancellationToken ct)
    {
        var item = new Part
        {
            PartNumber = request.PartNumber,
            PartName = request.PartName,
            PartCategory = request.PartCategory,
            UnitOfMeasure = request.UnitOfMeasure,
            StandardCost = request.StandardCost,
            ReorderPoint = request.ReorderPoint,
            OnHandQuantity = request.OnHandQuantity,
        };
        OnBeforeCreate(request, item);
        _db.Set<Part>().Add(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Created", Convert.ToString(item.PartId) ?? string.Empty, $"Created Part record {item.PartId}.", ToDto(item), ct);
        OnAfterCreate(item);
        await NotifyResourceChangedAsync("Created", Convert.ToString(item.PartId), ct);
        return CreatedAtAction(nameof(GetById), new { id = item.PartId }, ApiResponse<PartDto>.Success("record created", ToDto(item)));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdatePartRequest request, CancellationToken ct)
    {
        var item = await _db.Set<Part>().FirstOrDefaultAsync(x => x.PartId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeUpdate(item, request);
        item.PartNumber = request.PartNumber;
        item.PartName = request.PartName;
        item.PartCategory = request.PartCategory;
        item.UnitOfMeasure = request.UnitOfMeasure;
        item.StandardCost = request.StandardCost;
        item.ReorderPoint = request.ReorderPoint;
        item.OnHandQuantity = request.OnHandQuantity;
        var auditChanges = GetEntityChanges(_db.Entry(item));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Updated", Convert.ToString(item.PartId) ?? string.Empty, $"Updated Part record {item.PartId}.", auditChanges, ct);
        await NotifyResourceChangedAsync("Updated", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record updated", new { updated = 1 }));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch(int id, UpdatePartRequest request, CancellationToken ct)
    {
        return await Update(id, request, ct);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var item = await _db.Set<Part>().FirstOrDefaultAsync(x => x.PartId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeDelete(item);
        _db.Set<Part>().Remove(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Deleted", Convert.ToString(id) ?? string.Empty, $"Hard deleted Part record {id}.", ToDto(item), ct);
        await NotifyResourceChangedAsync("Deleted", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record deleted", new { deleted = 1, mode = "Hard" }));
    }

    [HttpPost("bulk/export")]
    public async Task<ActionResult<ApiResponse<PagedResult<PartDto>>>> ExportBulk(BulkIdsRequest request, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return Ok(ApiResponse<PagedResult<PartDto>>.Warning("no records selected", new PagedResult<PartDto>(Array.Empty<PartDto>(), page, pageSize, 0)));
        IQueryable<Part> query = _db.Set<Part>().AsNoTracking().Where(x => ids.Contains(x.PartId));
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<PartDto>>.Success("records exported", new PagedResult<PartDto>(items, page, pageSize, total)));
    }

    [HttpPatch("bulk")]
    public async Task<IActionResult> UpdateBulk(BulkUpdateRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        if (string.IsNullOrWhiteSpace(request.Field)) return BadRequest(ApiResponse<object>.Error("error", new { error = "Choose a field to update." }));
        IQueryable<Part> query = _db.Set<Part>().Where(x => ids.Contains(x.PartId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return NotFound(ApiResponse<object>.Warning("records not found"));
        if (!ApplyBulkUpdate(items, request, out var error)) return BadRequest(ApiResponse<object>.Error("error", new { error }));
        var auditChanges = items.ToDictionary(item => Convert.ToString(item.PartId) ?? string.Empty, item => GetEntityChanges(_db.Entry(item)));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Updated", Convert.ToString(item.PartId) ?? string.Empty, $"Updated Part record {item.PartId} in bulk update.", auditChanges[Convert.ToString(item.PartId) ?? string.Empty], ct);
        await NotifyResourceChangedAsync("Updated", null, ct);
        return Ok(ApiResponse<object>.Success("records updated", new { updated = items.Count }));
    }

    [HttpPost("bulk/delete")]
    public async Task<IActionResult> DeleteBulk(BulkIdsRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        IQueryable<Part> query = _db.Set<Part>().Where(x => ids.Contains(x.PartId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return Ok(ApiResponse<object>.Warning("records not found", new { deleted = 0 }));
        foreach (var item in items)
        {
            OnBeforeDelete(item);
        }
        _db.Set<Part>().RemoveRange(items);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Deleted", Convert.ToString(item.PartId) ?? string.Empty, $"Hard deleted Part record {item.PartId} in bulk delete.", ToDto(item), ct);
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

    private static bool ApplyBulkUpdate(IReadOnlyList<Part> items, BulkUpdateRequest request, out string error)
    {
        error = string.Empty;
        return request.Field.Trim().ToLowerInvariant() switch
        {
            "partnumber" => ApplyBulkPartNumber(items, request.Value, out error),
            "partname" => ApplyBulkPartName(items, request.Value, out error),
            "partcategory" => ApplyBulkPartCategory(items, request.Value, out error),
            "unitofmeasure" => ApplyBulkUnitOfMeasure(items, request.Value, out error),
            "standardcost" => ApplyBulkStandardCost(items, request.Value, out error),
            "reorderpoint" => ApplyBulkReorderPoint(items, request.Value, out error),
            "onhandquantity" => ApplyBulkOnHandQuantity(items, request.Value, out error),
            _ => FailBulkUpdate("Field is not bulk editable.", out error)
        };
    }

    private static bool ApplyBulkPartNumber(IReadOnlyList<Part> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.PartNumber = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkPartName(IReadOnlyList<Part> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.PartName = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkPartCategory(IReadOnlyList<Part> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.PartCategory = null;
            return true;
        }
        foreach (var item in items) item.PartCategory = raw;
        return true;
    }

    private static bool ApplyBulkUnitOfMeasure(IReadOnlyList<Part> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.UnitOfMeasure = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkStandardCost(IReadOnlyList<Part> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.StandardCost = null;
            return true;
        }
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("StandardCost requires a decimal value.", out error);
        foreach (var item in items) item.StandardCost = value;
        return true;
    }

    private static bool ApplyBulkReorderPoint(IReadOnlyList<Part> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("ReorderPoint requires a decimal value.", out error);
        foreach (var item in items) item.ReorderPoint = value;
        return true;
    }

    private static bool ApplyBulkOnHandQuantity(IReadOnlyList<Part> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("OnHandQuantity requires a decimal value.", out error);
        foreach (var item in items) item.OnHandQuantity = value;
        return true;
    }

    private static bool FailBulkUpdate(string message, out string error)
    {
        error = message;
        return false;
    }


    private static IQueryable<Part> ApplySearch(IQueryable<Part> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        search = search.Trim();
        return query.Where(x => (x.PartNumber != null && x.PartNumber.Contains(search)) || (x.PartName != null && x.PartName.Contains(search)) || (x.PartCategory != null && x.PartCategory.Contains(search)) || (x.UnitOfMeasure != null && x.UnitOfMeasure.Contains(search)));
    }

    private static IQueryable<Part> ApplyFilter(IQueryable<Part> query, string? filterField, string? filterValue)
    {
        if (string.IsNullOrWhiteSpace(filterField) || string.IsNullOrWhiteSpace(filterValue)) return query;
        filterField = filterField.Trim();
        filterValue = filterValue.Trim();
        return filterField.ToLowerInvariant() switch
        {
            "partid" => int.TryParse(filterValue, out var PartIdValue) ? query.Where(x => x.PartId == PartIdValue) : query,
            "partnumber" => query.Where(x => x.PartNumber != null && x.PartNumber.Contains(filterValue)),
            "partname" => query.Where(x => x.PartName != null && x.PartName.Contains(filterValue)),
            "partcategory" => query.Where(x => x.PartCategory != null && x.PartCategory.Contains(filterValue)),
            "unitofmeasure" => query.Where(x => x.UnitOfMeasure != null && x.UnitOfMeasure.Contains(filterValue)),
            "standardcost" => decimal.TryParse(filterValue, out var StandardCostValue) ? query.Where(x => x.StandardCost == StandardCostValue) : query,
            "reorderpoint" => decimal.TryParse(filterValue, out var ReorderPointValue) ? query.Where(x => x.ReorderPoint == ReorderPointValue) : query,
            "onhandquantity" => decimal.TryParse(filterValue, out var OnHandQuantityValue) ? query.Where(x => x.OnHandQuantity == OnHandQuantityValue) : query,
            _ => query
        };
    }

    private static IQueryable<Part> ApplySort(IQueryable<Part> query, string? sortBy, string? sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) || string.Equals(sortDirection, "descending", StringComparison.OrdinalIgnoreCase);
        var field = string.IsNullOrWhiteSpace(sortBy) ? "PartId" : sortBy.Trim();
        return field.ToLowerInvariant() switch
        {
            "partid" => descending ? query.OrderByDescending(x => x.PartId) : query.OrderBy(x => x.PartId),
            "partnumber" => descending ? query.OrderByDescending(x => x.PartNumber) : query.OrderBy(x => x.PartNumber),
            "partname" => descending ? query.OrderByDescending(x => x.PartName) : query.OrderBy(x => x.PartName),
            "partcategory" => descending ? query.OrderByDescending(x => x.PartCategory) : query.OrderBy(x => x.PartCategory),
            "unitofmeasure" => descending ? query.OrderByDescending(x => x.UnitOfMeasure) : query.OrderBy(x => x.UnitOfMeasure),
            "standardcost" => descending ? query.OrderByDescending(x => x.StandardCost) : query.OrderBy(x => x.StandardCost),
            "reorderpoint" => descending ? query.OrderByDescending(x => x.ReorderPoint) : query.OrderBy(x => x.ReorderPoint),
            "onhandquantity" => descending ? query.OrderByDescending(x => x.OnHandQuantity) : query.OrderBy(x => x.OnHandQuantity),
            _ => descending ? query.OrderByDescending(x => x.PartId) : query.OrderBy(x => x.PartId)
        };
    }
    private static PartDto ToDto(Part item) => new(
        item.PartId,
        item.PartNumber,
        item.PartName,
        item.PartCategory,
        item.UnitOfMeasure,
        item.StandardCost,
        item.ReorderPoint,
        item.OnHandQuantity
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
            Resource = "Part",
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
        _changes.Clients.All.SendAsync(DataChangeHub.DataChangedMethod, new DataChangeNotification("Part", action, resourceKey, DateTimeOffset.UtcNow), ct);

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
