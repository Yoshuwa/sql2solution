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
[Route("api/fluidTypes")]
public sealed partial class FluidTypeController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<DataChangeHub> _changes;

    public FluidTypeController(AppDbContext db, IHubContext<DataChangeHub> changes)
    {
        _db = db;
        _changes = changes;
    }

    partial void OnBeforeCreate(CreateFluidTypeRequest request, FluidType item);
    partial void OnAfterCreate(FluidType item);
    partial void OnBeforeUpdate(FluidType item, UpdateFluidTypeRequest request);
    partial void OnBeforeDelete(FluidType item);

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<FluidTypeDto>>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null, [FromQuery] string? filterField = null, [FromQuery] string? filterValue = null, [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        IQueryable<FluidType> query = _db.Set<FluidType>().AsNoTracking();
        query = ApplySearch(query, search);
        query = ApplyFilter(query, filterField, filterValue);
        query = ApplySort(query, sortBy, sortDirection);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<FluidTypeDto>>.Success("records loaded", new PagedResult<FluidTypeDto>(items, page, pageSize, total)));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<FluidTypeDto>>> GetById(int id, CancellationToken ct)
    {
        IQueryable<FluidType> query = _db.Set<FluidType>().AsNoTracking();
        var item = await query.FirstOrDefaultAsync(x => x.FluidTypeId!.Equals(id), ct);
        return item is null ? NotFound(ApiResponse<object>.Warning("record not found")) : Ok(ApiResponse<FluidTypeDto>.Success("record loaded", ToDto(item)));
    }

    [HttpGet("{id}/history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuditTrailDto>>>> GetHistory(int id, CancellationToken ct)
    {
        var canReadRecord = await _db.Set<FluidType>().AsNoTracking().AnyAsync(x => x.FluidTypeId!.Equals(id), ct);
        if (!canReadRecord) return NotFound(ApiResponse<object>.Warning("record not found"));
        await EnsureAuditTrailTableAsync(ct);
        var resourceKey = Convert.ToString(id) ?? string.Empty;
        var history = await _db.AuditTrailEntries
            .AsNoTracking()
            .Where(entry => entry.Resource == "FluidType" && entry.ResourceKey == resourceKey)
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .Take(100)
            .Select(entry => ToAuditTrailDto(entry))
            .ToListAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<AuditTrailDto>>.Success("activity loaded", history));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<FluidTypeDto>>> Create(CreateFluidTypeRequest request, CancellationToken ct)
    {
        var item = new FluidType
        {
            FluidCode = request.FluidCode,
            FluidName = request.FluidName,
            FluidCategory = request.FluidCategory,
            DefaultIntervalHours = request.DefaultIntervalHours,
            IsActive = request.IsActive,
        };
        OnBeforeCreate(request, item);
        _db.Set<FluidType>().Add(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Created", Convert.ToString(item.FluidTypeId) ?? string.Empty, $"Created FluidType record {item.FluidTypeId}.", ToDto(item), ct);
        OnAfterCreate(item);
        await NotifyResourceChangedAsync("Created", Convert.ToString(item.FluidTypeId), ct);
        return CreatedAtAction(nameof(GetById), new { id = item.FluidTypeId }, ApiResponse<FluidTypeDto>.Success("record created", ToDto(item)));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateFluidTypeRequest request, CancellationToken ct)
    {
        var item = await _db.Set<FluidType>().FirstOrDefaultAsync(x => x.FluidTypeId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeUpdate(item, request);
        item.FluidCode = request.FluidCode;
        item.FluidName = request.FluidName;
        item.FluidCategory = request.FluidCategory;
        item.DefaultIntervalHours = request.DefaultIntervalHours;
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
        await LogAuditTrailAsync("Updated", Convert.ToString(item.FluidTypeId) ?? string.Empty, $"Updated FluidType record {item.FluidTypeId}.", auditChanges, ct);
        await NotifyResourceChangedAsync("Updated", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record updated", new { updated = 1 }));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch(int id, UpdateFluidTypeRequest request, CancellationToken ct)
    {
        return await Update(id, request, ct);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var item = await _db.Set<FluidType>().FirstOrDefaultAsync(x => x.FluidTypeId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeDelete(item);
        _db.Set<FluidType>().Remove(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Deleted", Convert.ToString(id) ?? string.Empty, $"Hard deleted FluidType record {id}.", ToDto(item), ct);
        await NotifyResourceChangedAsync("Deleted", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record deleted", new { deleted = 1, mode = "Hard" }));
    }

    [HttpPost("bulk/export")]
    public async Task<ActionResult<ApiResponse<PagedResult<FluidTypeDto>>>> ExportBulk(BulkIdsRequest request, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return Ok(ApiResponse<PagedResult<FluidTypeDto>>.Warning("no records selected", new PagedResult<FluidTypeDto>(Array.Empty<FluidTypeDto>(), page, pageSize, 0)));
        IQueryable<FluidType> query = _db.Set<FluidType>().AsNoTracking().Where(x => ids.Contains(x.FluidTypeId));
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<FluidTypeDto>>.Success("records exported", new PagedResult<FluidTypeDto>(items, page, pageSize, total)));
    }

    [HttpPatch("bulk")]
    public async Task<IActionResult> UpdateBulk(BulkUpdateRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        if (string.IsNullOrWhiteSpace(request.Field)) return BadRequest(ApiResponse<object>.Error("error", new { error = "Choose a field to update." }));
        IQueryable<FluidType> query = _db.Set<FluidType>().Where(x => ids.Contains(x.FluidTypeId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return NotFound(ApiResponse<object>.Warning("records not found"));
        if (!ApplyBulkUpdate(items, request, out var error)) return BadRequest(ApiResponse<object>.Error("error", new { error }));
        var auditChanges = items.ToDictionary(item => Convert.ToString(item.FluidTypeId) ?? string.Empty, item => GetEntityChanges(_db.Entry(item)));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Updated", Convert.ToString(item.FluidTypeId) ?? string.Empty, $"Updated FluidType record {item.FluidTypeId} in bulk update.", auditChanges[Convert.ToString(item.FluidTypeId) ?? string.Empty], ct);
        await NotifyResourceChangedAsync("Updated", null, ct);
        return Ok(ApiResponse<object>.Success("records updated", new { updated = items.Count }));
    }

    [HttpPost("bulk/delete")]
    public async Task<IActionResult> DeleteBulk(BulkIdsRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        IQueryable<FluidType> query = _db.Set<FluidType>().Where(x => ids.Contains(x.FluidTypeId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return Ok(ApiResponse<object>.Warning("records not found", new { deleted = 0 }));
        foreach (var item in items)
        {
            OnBeforeDelete(item);
        }
        _db.Set<FluidType>().RemoveRange(items);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Deleted", Convert.ToString(item.FluidTypeId) ?? string.Empty, $"Hard deleted FluidType record {item.FluidTypeId} in bulk delete.", ToDto(item), ct);
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

    private static bool ApplyBulkUpdate(IReadOnlyList<FluidType> items, BulkUpdateRequest request, out string error)
    {
        error = string.Empty;
        return request.Field.Trim().ToLowerInvariant() switch
        {
            "fluidcode" => ApplyBulkFluidCode(items, request.Value, out error),
            "fluidname" => ApplyBulkFluidName(items, request.Value, out error),
            "fluidcategory" => ApplyBulkFluidCategory(items, request.Value, out error),
            "defaultintervalhours" => ApplyBulkDefaultIntervalHours(items, request.Value, out error),
            "isactive" => ApplyBulkIsActive(items, request.Value, out error),
            _ => FailBulkUpdate("Field is not bulk editable.", out error)
        };
    }

    private static bool ApplyBulkFluidCode(IReadOnlyList<FluidType> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.FluidCode = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkFluidName(IReadOnlyList<FluidType> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.FluidName = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkFluidCategory(IReadOnlyList<FluidType> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.FluidCategory = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkDefaultIntervalHours(IReadOnlyList<FluidType> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.DefaultIntervalHours = null;
            return true;
        }
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("DefaultIntervalHours requires a decimal value.", out error);
        foreach (var item in items) item.DefaultIntervalHours = value;
        return true;
    }

    private static bool ApplyBulkIsActive(IReadOnlyList<FluidType> items, string? raw, out string error)
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


    private static IQueryable<FluidType> ApplySearch(IQueryable<FluidType> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        search = search.Trim();
        return query.Where(x => (x.FluidCode != null && x.FluidCode.Contains(search)) || (x.FluidName != null && x.FluidName.Contains(search)) || (x.FluidCategory != null && x.FluidCategory.Contains(search)));
    }

    private static IQueryable<FluidType> ApplyFilter(IQueryable<FluidType> query, string? filterField, string? filterValue)
    {
        if (string.IsNullOrWhiteSpace(filterField) || string.IsNullOrWhiteSpace(filterValue)) return query;
        filterField = filterField.Trim();
        filterValue = filterValue.Trim();
        return filterField.ToLowerInvariant() switch
        {
            "fluidtypeid" => int.TryParse(filterValue, out var FluidTypeIdValue) ? query.Where(x => x.FluidTypeId == FluidTypeIdValue) : query,
            "fluidcode" => query.Where(x => x.FluidCode != null && x.FluidCode.Contains(filterValue)),
            "fluidname" => query.Where(x => x.FluidName != null && x.FluidName.Contains(filterValue)),
            "fluidcategory" => query.Where(x => x.FluidCategory != null && x.FluidCategory.Contains(filterValue)),
            "defaultintervalhours" => decimal.TryParse(filterValue, out var DefaultIntervalHoursValue) ? query.Where(x => x.DefaultIntervalHours == DefaultIntervalHoursValue) : query,
            "isactive" => bool.TryParse(filterValue, out var IsActiveValue) ? query.Where(x => x.IsActive == IsActiveValue) : query,
            _ => query
        };
    }

    private static IQueryable<FluidType> ApplySort(IQueryable<FluidType> query, string? sortBy, string? sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) || string.Equals(sortDirection, "descending", StringComparison.OrdinalIgnoreCase);
        var field = string.IsNullOrWhiteSpace(sortBy) ? "FluidTypeId" : sortBy.Trim();
        return field.ToLowerInvariant() switch
        {
            "fluidtypeid" => descending ? query.OrderByDescending(x => x.FluidTypeId) : query.OrderBy(x => x.FluidTypeId),
            "fluidcode" => descending ? query.OrderByDescending(x => x.FluidCode) : query.OrderBy(x => x.FluidCode),
            "fluidname" => descending ? query.OrderByDescending(x => x.FluidName) : query.OrderBy(x => x.FluidName),
            "fluidcategory" => descending ? query.OrderByDescending(x => x.FluidCategory) : query.OrderBy(x => x.FluidCategory),
            "defaultintervalhours" => descending ? query.OrderByDescending(x => x.DefaultIntervalHours) : query.OrderBy(x => x.DefaultIntervalHours),
            "isactive" => descending ? query.OrderByDescending(x => x.IsActive) : query.OrderBy(x => x.IsActive),
            _ => descending ? query.OrderByDescending(x => x.FluidTypeId) : query.OrderBy(x => x.FluidTypeId)
        };
    }
    private static FluidTypeDto ToDto(FluidType item) => new(
        item.FluidTypeId,
        item.FluidCode,
        item.FluidName,
        item.FluidCategory,
        item.DefaultIntervalHours,
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
            Resource = "FluidType",
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
        _changes.Clients.All.SendAsync(DataChangeHub.DataChangedMethod, new DataChangeNotification("FluidType", action, resourceKey, DateTimeOffset.UtcNow), ct);

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
