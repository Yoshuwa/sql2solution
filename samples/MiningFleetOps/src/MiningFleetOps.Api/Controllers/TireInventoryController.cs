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
[Route("api/tireInventories")]
public sealed partial class TireInventoryController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<DataChangeHub> _changes;

    public TireInventoryController(AppDbContext db, IHubContext<DataChangeHub> changes)
    {
        _db = db;
        _changes = changes;
    }

    partial void OnBeforeCreate(CreateTireInventoryRequest request, TireInventory item);
    partial void OnAfterCreate(TireInventory item);
    partial void OnBeforeUpdate(TireInventory item, UpdateTireInventoryRequest request);
    partial void OnBeforeDelete(TireInventory item);

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<TireInventoryDto>>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null, [FromQuery] string? filterField = null, [FromQuery] string? filterValue = null, [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        IQueryable<TireInventory> query = _db.Set<TireInventory>().AsNoTracking();
        query = ApplySearch(query, search);
        query = ApplyFilter(query, filterField, filterValue);
        query = ApplySort(query, sortBy, sortDirection);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<TireInventoryDto>>.Success("records loaded", new PagedResult<TireInventoryDto>(items, page, pageSize, total)));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<TireInventoryDto>>> GetById(int id, CancellationToken ct)
    {
        IQueryable<TireInventory> query = _db.Set<TireInventory>().AsNoTracking();
        var item = await query.FirstOrDefaultAsync(x => x.TireId!.Equals(id), ct);
        return item is null ? NotFound(ApiResponse<object>.Warning("record not found")) : Ok(ApiResponse<TireInventoryDto>.Success("record loaded", ToDto(item)));
    }

    [HttpGet("{id}/history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuditTrailDto>>>> GetHistory(int id, CancellationToken ct)
    {
        var canReadRecord = await _db.Set<TireInventory>().AsNoTracking().AnyAsync(x => x.TireId!.Equals(id), ct);
        if (!canReadRecord) return NotFound(ApiResponse<object>.Warning("record not found"));
        await EnsureAuditTrailTableAsync(ct);
        var resourceKey = Convert.ToString(id) ?? string.Empty;
        var history = await _db.AuditTrailEntries
            .AsNoTracking()
            .Where(entry => entry.Resource == "TireInventory" && entry.ResourceKey == resourceKey)
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .Take(100)
            .Select(entry => ToAuditTrailDto(entry))
            .ToListAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<AuditTrailDto>>.Success("activity loaded", history));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<TireInventoryDto>>> Create(CreateTireInventoryRequest request, CancellationToken ct)
    {
        var item = new TireInventory
        {
            TireSerialNumber = request.TireSerialNumber,
            Manufacturer = request.Manufacturer,
            TireSize = request.TireSize,
            TireType = request.TireType,
            PurchaseDate = request.PurchaseDate,
            PurchaseCost = request.PurchaseCost,
            OriginalTreadDepthMm = request.OriginalTreadDepthMm,
            Status = request.Status,
            CreatedAt = request.CreatedAt,
        };
        OnBeforeCreate(request, item);
        _db.Set<TireInventory>().Add(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Created", Convert.ToString(item.TireId) ?? string.Empty, $"Created TireInventory record {item.TireId}.", ToDto(item), ct);
        OnAfterCreate(item);
        await NotifyResourceChangedAsync("Created", Convert.ToString(item.TireId), ct);
        return CreatedAtAction(nameof(GetById), new { id = item.TireId }, ApiResponse<TireInventoryDto>.Success("record created", ToDto(item)));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateTireInventoryRequest request, CancellationToken ct)
    {
        var item = await _db.Set<TireInventory>().FirstOrDefaultAsync(x => x.TireId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeUpdate(item, request);
        item.TireSerialNumber = request.TireSerialNumber;
        item.Manufacturer = request.Manufacturer;
        item.TireSize = request.TireSize;
        item.TireType = request.TireType;
        item.PurchaseDate = request.PurchaseDate;
        item.PurchaseCost = request.PurchaseCost;
        item.OriginalTreadDepthMm = request.OriginalTreadDepthMm;
        item.Status = request.Status;
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
        await LogAuditTrailAsync("Updated", Convert.ToString(item.TireId) ?? string.Empty, $"Updated TireInventory record {item.TireId}.", auditChanges, ct);
        await NotifyResourceChangedAsync("Updated", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record updated", new { updated = 1 }));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch(int id, UpdateTireInventoryRequest request, CancellationToken ct)
    {
        return await Update(id, request, ct);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var item = await _db.Set<TireInventory>().FirstOrDefaultAsync(x => x.TireId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeDelete(item);
        _db.Set<TireInventory>().Remove(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Deleted", Convert.ToString(id) ?? string.Empty, $"Hard deleted TireInventory record {id}.", ToDto(item), ct);
        await NotifyResourceChangedAsync("Deleted", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record deleted", new { deleted = 1, mode = "Hard" }));
    }

    [HttpPost("bulk/export")]
    public async Task<ActionResult<ApiResponse<PagedResult<TireInventoryDto>>>> ExportBulk(BulkIdsRequest request, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return Ok(ApiResponse<PagedResult<TireInventoryDto>>.Warning("no records selected", new PagedResult<TireInventoryDto>(Array.Empty<TireInventoryDto>(), page, pageSize, 0)));
        IQueryable<TireInventory> query = _db.Set<TireInventory>().AsNoTracking().Where(x => ids.Contains(x.TireId));
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<TireInventoryDto>>.Success("records exported", new PagedResult<TireInventoryDto>(items, page, pageSize, total)));
    }

    [HttpPatch("bulk")]
    public async Task<IActionResult> UpdateBulk(BulkUpdateRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        if (string.IsNullOrWhiteSpace(request.Field)) return BadRequest(ApiResponse<object>.Error("error", new { error = "Choose a field to update." }));
        IQueryable<TireInventory> query = _db.Set<TireInventory>().Where(x => ids.Contains(x.TireId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return NotFound(ApiResponse<object>.Warning("records not found"));
        if (!ApplyBulkUpdate(items, request, out var error)) return BadRequest(ApiResponse<object>.Error("error", new { error }));
        var auditChanges = items.ToDictionary(item => Convert.ToString(item.TireId) ?? string.Empty, item => GetEntityChanges(_db.Entry(item)));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Updated", Convert.ToString(item.TireId) ?? string.Empty, $"Updated TireInventory record {item.TireId} in bulk update.", auditChanges[Convert.ToString(item.TireId) ?? string.Empty], ct);
        await NotifyResourceChangedAsync("Updated", null, ct);
        return Ok(ApiResponse<object>.Success("records updated", new { updated = items.Count }));
    }

    [HttpPost("bulk/delete")]
    public async Task<IActionResult> DeleteBulk(BulkIdsRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        IQueryable<TireInventory> query = _db.Set<TireInventory>().Where(x => ids.Contains(x.TireId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return Ok(ApiResponse<object>.Warning("records not found", new { deleted = 0 }));
        foreach (var item in items)
        {
            OnBeforeDelete(item);
        }
        _db.Set<TireInventory>().RemoveRange(items);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Deleted", Convert.ToString(item.TireId) ?? string.Empty, $"Hard deleted TireInventory record {item.TireId} in bulk delete.", ToDto(item), ct);
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

    private static bool ApplyBulkUpdate(IReadOnlyList<TireInventory> items, BulkUpdateRequest request, out string error)
    {
        error = string.Empty;
        return request.Field.Trim().ToLowerInvariant() switch
        {
            "tireserialnumber" => ApplyBulkTireSerialNumber(items, request.Value, out error),
            "manufacturer" => ApplyBulkManufacturer(items, request.Value, out error),
            "tiresize" => ApplyBulkTireSize(items, request.Value, out error),
            "tiretype" => ApplyBulkTireType(items, request.Value, out error),
            "purchasedate" => ApplyBulkPurchaseDate(items, request.Value, out error),
            "purchasecost" => ApplyBulkPurchaseCost(items, request.Value, out error),
            "originaltreaddepthmm" => ApplyBulkOriginalTreadDepthMm(items, request.Value, out error),
            "status" => ApplyBulkStatus(items, request.Value, out error),
            "createdat" => ApplyBulkCreatedAt(items, request.Value, out error),
            _ => FailBulkUpdate("Field is not bulk editable.", out error)
        };
    }

    private static bool ApplyBulkTireSerialNumber(IReadOnlyList<TireInventory> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.TireSerialNumber = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkManufacturer(IReadOnlyList<TireInventory> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.Manufacturer = null;
            return true;
        }
        foreach (var item in items) item.Manufacturer = raw;
        return true;
    }

    private static bool ApplyBulkTireSize(IReadOnlyList<TireInventory> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.TireSize = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkTireType(IReadOnlyList<TireInventory> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.TireType = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkPurchaseDate(IReadOnlyList<TireInventory> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.PurchaseDate = null;
            return true;
        }
        if (!DateTime.TryParse(raw, out var value)) return FailBulkUpdate("PurchaseDate requires a DateTime value.", out error);
        foreach (var item in items) item.PurchaseDate = value;
        return true;
    }

    private static bool ApplyBulkPurchaseCost(IReadOnlyList<TireInventory> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.PurchaseCost = null;
            return true;
        }
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("PurchaseCost requires a decimal value.", out error);
        foreach (var item in items) item.PurchaseCost = value;
        return true;
    }

    private static bool ApplyBulkOriginalTreadDepthMm(IReadOnlyList<TireInventory> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("OriginalTreadDepthMm requires a decimal value.", out error);
        foreach (var item in items) item.OriginalTreadDepthMm = value;
        return true;
    }

    private static bool ApplyBulkStatus(IReadOnlyList<TireInventory> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.Status = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkCreatedAt(IReadOnlyList<TireInventory> items, string? raw, out string error)
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


    private static IQueryable<TireInventory> ApplySearch(IQueryable<TireInventory> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        search = search.Trim();
        return query.Where(x => (x.TireSerialNumber != null && x.TireSerialNumber.Contains(search)) || (x.Manufacturer != null && x.Manufacturer.Contains(search)) || (x.TireSize != null && x.TireSize.Contains(search)) || (x.TireType != null && x.TireType.Contains(search)) || (x.Status != null && x.Status.Contains(search)));
    }

    private static IQueryable<TireInventory> ApplyFilter(IQueryable<TireInventory> query, string? filterField, string? filterValue)
    {
        if (string.IsNullOrWhiteSpace(filterField) || string.IsNullOrWhiteSpace(filterValue)) return query;
        filterField = filterField.Trim();
        filterValue = filterValue.Trim();
        return filterField.ToLowerInvariant() switch
        {
            "tireid" => int.TryParse(filterValue, out var TireIdValue) ? query.Where(x => x.TireId == TireIdValue) : query,
            "tireserialnumber" => query.Where(x => x.TireSerialNumber != null && x.TireSerialNumber.Contains(filterValue)),
            "manufacturer" => query.Where(x => x.Manufacturer != null && x.Manufacturer.Contains(filterValue)),
            "tiresize" => query.Where(x => x.TireSize != null && x.TireSize.Contains(filterValue)),
            "tiretype" => query.Where(x => x.TireType != null && x.TireType.Contains(filterValue)),
            "purchasedate" => DateTime.TryParse(filterValue, out var PurchaseDateValue) ? query.Where(x => x.PurchaseDate == PurchaseDateValue) : query,
            "purchasecost" => decimal.TryParse(filterValue, out var PurchaseCostValue) ? query.Where(x => x.PurchaseCost == PurchaseCostValue) : query,
            "originaltreaddepthmm" => decimal.TryParse(filterValue, out var OriginalTreadDepthMmValue) ? query.Where(x => x.OriginalTreadDepthMm == OriginalTreadDepthMmValue) : query,
            "status" => query.Where(x => x.Status != null && x.Status.Contains(filterValue)),
            "createdat" => DateTime.TryParse(filterValue, out var CreatedAtValue) ? query.Where(x => x.CreatedAt == CreatedAtValue) : query,
            _ => query
        };
    }

    private static IQueryable<TireInventory> ApplySort(IQueryable<TireInventory> query, string? sortBy, string? sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) || string.Equals(sortDirection, "descending", StringComparison.OrdinalIgnoreCase);
        var field = string.IsNullOrWhiteSpace(sortBy) ? "TireId" : sortBy.Trim();
        return field.ToLowerInvariant() switch
        {
            "tireid" => descending ? query.OrderByDescending(x => x.TireId) : query.OrderBy(x => x.TireId),
            "tireserialnumber" => descending ? query.OrderByDescending(x => x.TireSerialNumber) : query.OrderBy(x => x.TireSerialNumber),
            "manufacturer" => descending ? query.OrderByDescending(x => x.Manufacturer) : query.OrderBy(x => x.Manufacturer),
            "tiresize" => descending ? query.OrderByDescending(x => x.TireSize) : query.OrderBy(x => x.TireSize),
            "tiretype" => descending ? query.OrderByDescending(x => x.TireType) : query.OrderBy(x => x.TireType),
            "purchasedate" => descending ? query.OrderByDescending(x => x.PurchaseDate) : query.OrderBy(x => x.PurchaseDate),
            "purchasecost" => descending ? query.OrderByDescending(x => x.PurchaseCost) : query.OrderBy(x => x.PurchaseCost),
            "originaltreaddepthmm" => descending ? query.OrderByDescending(x => x.OriginalTreadDepthMm) : query.OrderBy(x => x.OriginalTreadDepthMm),
            "status" => descending ? query.OrderByDescending(x => x.Status) : query.OrderBy(x => x.Status),
            "createdat" => descending ? query.OrderByDescending(x => x.CreatedAt) : query.OrderBy(x => x.CreatedAt),
            _ => descending ? query.OrderByDescending(x => x.TireId) : query.OrderBy(x => x.TireId)
        };
    }
    private static TireInventoryDto ToDto(TireInventory item) => new(
        item.TireId,
        item.TireSerialNumber,
        item.Manufacturer,
        item.TireSize,
        item.TireType,
        item.PurchaseDate,
        item.PurchaseCost,
        item.OriginalTreadDepthMm,
        item.Status,
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
            Resource = "TireInventory",
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
        _changes.Clients.All.SendAsync(DataChangeHub.DataChangedMethod, new DataChangeNotification("TireInventory", action, resourceKey, DateTimeOffset.UtcNow), ct);

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
