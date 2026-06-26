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
[Route("api/fuelTypes")]
public sealed partial class FuelTypeController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<DataChangeHub> _changes;

    public FuelTypeController(AppDbContext db, IHubContext<DataChangeHub> changes)
    {
        _db = db;
        _changes = changes;
    }

    partial void OnBeforeCreate(CreateFuelTypeRequest request, FuelType item);
    partial void OnAfterCreate(FuelType item);
    partial void OnBeforeUpdate(FuelType item, UpdateFuelTypeRequest request);
    partial void OnBeforeDelete(FuelType item);

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<FuelTypeDto>>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null, [FromQuery] string? filterField = null, [FromQuery] string? filterValue = null, [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        IQueryable<FuelType> query = _db.Set<FuelType>().AsNoTracking();
        query = ApplySearch(query, search);
        query = ApplyFilter(query, filterField, filterValue);
        query = ApplySort(query, sortBy, sortDirection);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<FuelTypeDto>>.Success("records loaded", new PagedResult<FuelTypeDto>(items, page, pageSize, total)));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<FuelTypeDto>>> GetById(int id, CancellationToken ct)
    {
        IQueryable<FuelType> query = _db.Set<FuelType>().AsNoTracking();
        var item = await query.FirstOrDefaultAsync(x => x.FuelTypeId!.Equals(id), ct);
        return item is null ? NotFound(ApiResponse<object>.Warning("record not found")) : Ok(ApiResponse<FuelTypeDto>.Success("record loaded", ToDto(item)));
    }

    [HttpGet("{id}/history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuditTrailDto>>>> GetHistory(int id, CancellationToken ct)
    {
        var canReadRecord = await _db.Set<FuelType>().AsNoTracking().AnyAsync(x => x.FuelTypeId!.Equals(id), ct);
        if (!canReadRecord) return NotFound(ApiResponse<object>.Warning("record not found"));
        await EnsureAuditTrailTableAsync(ct);
        var resourceKey = Convert.ToString(id) ?? string.Empty;
        var history = await _db.AuditTrailEntries
            .AsNoTracking()
            .Where(entry => entry.Resource == "FuelType" && entry.ResourceKey == resourceKey)
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .Take(100)
            .Select(entry => ToAuditTrailDto(entry))
            .ToListAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<AuditTrailDto>>.Success("activity loaded", history));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<FuelTypeDto>>> Create(CreateFuelTypeRequest request, CancellationToken ct)
    {
        var item = new FuelType
        {
            FuelCode = request.FuelCode,
            FuelName = request.FuelName,
            EnergyDensityMjPerL = request.EnergyDensityMjPerL,
            Co2KgPerL = request.Co2KgPerL,
            IsActive = request.IsActive,
        };
        OnBeforeCreate(request, item);
        _db.Set<FuelType>().Add(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Created", Convert.ToString(item.FuelTypeId) ?? string.Empty, $"Created FuelType record {item.FuelTypeId}.", ToDto(item), ct);
        OnAfterCreate(item);
        await NotifyResourceChangedAsync("Created", Convert.ToString(item.FuelTypeId), ct);
        return CreatedAtAction(nameof(GetById), new { id = item.FuelTypeId }, ApiResponse<FuelTypeDto>.Success("record created", ToDto(item)));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateFuelTypeRequest request, CancellationToken ct)
    {
        var item = await _db.Set<FuelType>().FirstOrDefaultAsync(x => x.FuelTypeId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeUpdate(item, request);
        item.FuelCode = request.FuelCode;
        item.FuelName = request.FuelName;
        item.EnergyDensityMjPerL = request.EnergyDensityMjPerL;
        item.Co2KgPerL = request.Co2KgPerL;
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
        await LogAuditTrailAsync("Updated", Convert.ToString(item.FuelTypeId) ?? string.Empty, $"Updated FuelType record {item.FuelTypeId}.", auditChanges, ct);
        await NotifyResourceChangedAsync("Updated", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record updated", new { updated = 1 }));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch(int id, UpdateFuelTypeRequest request, CancellationToken ct)
    {
        return await Update(id, request, ct);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var item = await _db.Set<FuelType>().FirstOrDefaultAsync(x => x.FuelTypeId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeDelete(item);
        _db.Set<FuelType>().Remove(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Deleted", Convert.ToString(id) ?? string.Empty, $"Hard deleted FuelType record {id}.", ToDto(item), ct);
        await NotifyResourceChangedAsync("Deleted", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record deleted", new { deleted = 1, mode = "Hard" }));
    }

    [HttpPost("bulk/export")]
    public async Task<ActionResult<ApiResponse<PagedResult<FuelTypeDto>>>> ExportBulk(BulkIdsRequest request, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return Ok(ApiResponse<PagedResult<FuelTypeDto>>.Warning("no records selected", new PagedResult<FuelTypeDto>(Array.Empty<FuelTypeDto>(), page, pageSize, 0)));
        IQueryable<FuelType> query = _db.Set<FuelType>().AsNoTracking().Where(x => ids.Contains(x.FuelTypeId));
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<FuelTypeDto>>.Success("records exported", new PagedResult<FuelTypeDto>(items, page, pageSize, total)));
    }

    [HttpPatch("bulk")]
    public async Task<IActionResult> UpdateBulk(BulkUpdateRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        if (string.IsNullOrWhiteSpace(request.Field)) return BadRequest(ApiResponse<object>.Error("error", new { error = "Choose a field to update." }));
        IQueryable<FuelType> query = _db.Set<FuelType>().Where(x => ids.Contains(x.FuelTypeId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return NotFound(ApiResponse<object>.Warning("records not found"));
        if (!ApplyBulkUpdate(items, request, out var error)) return BadRequest(ApiResponse<object>.Error("error", new { error }));
        var auditChanges = items.ToDictionary(item => Convert.ToString(item.FuelTypeId) ?? string.Empty, item => GetEntityChanges(_db.Entry(item)));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Updated", Convert.ToString(item.FuelTypeId) ?? string.Empty, $"Updated FuelType record {item.FuelTypeId} in bulk update.", auditChanges[Convert.ToString(item.FuelTypeId) ?? string.Empty], ct);
        await NotifyResourceChangedAsync("Updated", null, ct);
        return Ok(ApiResponse<object>.Success("records updated", new { updated = items.Count }));
    }

    [HttpPost("bulk/delete")]
    public async Task<IActionResult> DeleteBulk(BulkIdsRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        IQueryable<FuelType> query = _db.Set<FuelType>().Where(x => ids.Contains(x.FuelTypeId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return Ok(ApiResponse<object>.Warning("records not found", new { deleted = 0 }));
        foreach (var item in items)
        {
            OnBeforeDelete(item);
        }
        _db.Set<FuelType>().RemoveRange(items);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Deleted", Convert.ToString(item.FuelTypeId) ?? string.Empty, $"Hard deleted FuelType record {item.FuelTypeId} in bulk delete.", ToDto(item), ct);
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

    private static bool ApplyBulkUpdate(IReadOnlyList<FuelType> items, BulkUpdateRequest request, out string error)
    {
        error = string.Empty;
        return request.Field.Trim().ToLowerInvariant() switch
        {
            "fuelcode" => ApplyBulkFuelCode(items, request.Value, out error),
            "fuelname" => ApplyBulkFuelName(items, request.Value, out error),
            "energydensitymjperl" => ApplyBulkEnergyDensityMjPerL(items, request.Value, out error),
            "co2kgperl" => ApplyBulkCo2KgPerL(items, request.Value, out error),
            "isactive" => ApplyBulkIsActive(items, request.Value, out error),
            _ => FailBulkUpdate("Field is not bulk editable.", out error)
        };
    }

    private static bool ApplyBulkFuelCode(IReadOnlyList<FuelType> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.FuelCode = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkFuelName(IReadOnlyList<FuelType> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.FuelName = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkEnergyDensityMjPerL(IReadOnlyList<FuelType> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.EnergyDensityMjPerL = null;
            return true;
        }
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("EnergyDensityMjPerL requires a decimal value.", out error);
        foreach (var item in items) item.EnergyDensityMjPerL = value;
        return true;
    }

    private static bool ApplyBulkCo2KgPerL(IReadOnlyList<FuelType> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.Co2KgPerL = null;
            return true;
        }
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("Co2KgPerL requires a decimal value.", out error);
        foreach (var item in items) item.Co2KgPerL = value;
        return true;
    }

    private static bool ApplyBulkIsActive(IReadOnlyList<FuelType> items, string? raw, out string error)
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


    private static IQueryable<FuelType> ApplySearch(IQueryable<FuelType> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        search = search.Trim();
        return query.Where(x => (x.FuelCode != null && x.FuelCode.Contains(search)) || (x.FuelName != null && x.FuelName.Contains(search)));
    }

    private static IQueryable<FuelType> ApplyFilter(IQueryable<FuelType> query, string? filterField, string? filterValue)
    {
        if (string.IsNullOrWhiteSpace(filterField) || string.IsNullOrWhiteSpace(filterValue)) return query;
        filterField = filterField.Trim();
        filterValue = filterValue.Trim();
        return filterField.ToLowerInvariant() switch
        {
            "fueltypeid" => int.TryParse(filterValue, out var FuelTypeIdValue) ? query.Where(x => x.FuelTypeId == FuelTypeIdValue) : query,
            "fuelcode" => query.Where(x => x.FuelCode != null && x.FuelCode.Contains(filterValue)),
            "fuelname" => query.Where(x => x.FuelName != null && x.FuelName.Contains(filterValue)),
            "energydensitymjperl" => decimal.TryParse(filterValue, out var EnergyDensityMjPerLValue) ? query.Where(x => x.EnergyDensityMjPerL == EnergyDensityMjPerLValue) : query,
            "co2kgperl" => decimal.TryParse(filterValue, out var Co2KgPerLValue) ? query.Where(x => x.Co2KgPerL == Co2KgPerLValue) : query,
            "isactive" => bool.TryParse(filterValue, out var IsActiveValue) ? query.Where(x => x.IsActive == IsActiveValue) : query,
            _ => query
        };
    }

    private static IQueryable<FuelType> ApplySort(IQueryable<FuelType> query, string? sortBy, string? sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) || string.Equals(sortDirection, "descending", StringComparison.OrdinalIgnoreCase);
        var field = string.IsNullOrWhiteSpace(sortBy) ? "FuelTypeId" : sortBy.Trim();
        return field.ToLowerInvariant() switch
        {
            "fueltypeid" => descending ? query.OrderByDescending(x => x.FuelTypeId) : query.OrderBy(x => x.FuelTypeId),
            "fuelcode" => descending ? query.OrderByDescending(x => x.FuelCode) : query.OrderBy(x => x.FuelCode),
            "fuelname" => descending ? query.OrderByDescending(x => x.FuelName) : query.OrderBy(x => x.FuelName),
            "energydensitymjperl" => descending ? query.OrderByDescending(x => x.EnergyDensityMjPerL) : query.OrderBy(x => x.EnergyDensityMjPerL),
            "co2kgperl" => descending ? query.OrderByDescending(x => x.Co2KgPerL) : query.OrderBy(x => x.Co2KgPerL),
            "isactive" => descending ? query.OrderByDescending(x => x.IsActive) : query.OrderBy(x => x.IsActive),
            _ => descending ? query.OrderByDescending(x => x.FuelTypeId) : query.OrderBy(x => x.FuelTypeId)
        };
    }
    private static FuelTypeDto ToDto(FuelType item) => new(
        item.FuelTypeId,
        item.FuelCode,
        item.FuelName,
        item.EnergyDensityMjPerL,
        item.Co2KgPerL,
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
            Resource = "FuelType",
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
        _changes.Clients.All.SendAsync(DataChangeHub.DataChangedMethod, new DataChangeNotification("FuelType", action, resourceKey, DateTimeOffset.UtcNow), ct);

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
