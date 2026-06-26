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
[Route("api/equipmentClass")]
public sealed partial class EquipmentClassController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<DataChangeHub> _changes;

    public EquipmentClassController(AppDbContext db, IHubContext<DataChangeHub> changes)
    {
        _db = db;
        _changes = changes;
    }

    partial void OnBeforeCreate(CreateEquipmentClassRequest request, EquipmentClass item);
    partial void OnAfterCreate(EquipmentClass item);
    partial void OnBeforeUpdate(EquipmentClass item, UpdateEquipmentClassRequest request);
    partial void OnBeforeDelete(EquipmentClass item);

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<EquipmentClassDto>>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null, [FromQuery] string? filterField = null, [FromQuery] string? filterValue = null, [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        IQueryable<EquipmentClass> query = _db.Set<EquipmentClass>().AsNoTracking();
        query = ApplySearch(query, search);
        query = ApplyFilter(query, filterField, filterValue);
        query = ApplySort(query, sortBy, sortDirection);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<EquipmentClassDto>>.Success("records loaded", new PagedResult<EquipmentClassDto>(items, page, pageSize, total)));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<EquipmentClassDto>>> GetById(int id, CancellationToken ct)
    {
        IQueryable<EquipmentClass> query = _db.Set<EquipmentClass>().AsNoTracking();
        var item = await query.FirstOrDefaultAsync(x => x.EquipmentClassId!.Equals(id), ct);
        return item is null ? NotFound(ApiResponse<object>.Warning("record not found")) : Ok(ApiResponse<EquipmentClassDto>.Success("record loaded", ToDto(item)));
    }

    [HttpGet("{id}/history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuditTrailDto>>>> GetHistory(int id, CancellationToken ct)
    {
        var canReadRecord = await _db.Set<EquipmentClass>().AsNoTracking().AnyAsync(x => x.EquipmentClassId!.Equals(id), ct);
        if (!canReadRecord) return NotFound(ApiResponse<object>.Warning("record not found"));
        await EnsureAuditTrailTableAsync(ct);
        var resourceKey = Convert.ToString(id) ?? string.Empty;
        var history = await _db.AuditTrailEntries
            .AsNoTracking()
            .Where(entry => entry.Resource == "EquipmentClass" && entry.ResourceKey == resourceKey)
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .Take(100)
            .Select(entry => ToAuditTrailDto(entry))
            .ToListAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<AuditTrailDto>>.Success("activity loaded", history));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<EquipmentClassDto>>> Create(CreateEquipmentClassRequest request, CancellationToken ct)
    {
        var item = new EquipmentClass
        {
            ClassCode = request.ClassCode,
            ClassName = request.ClassName,
            CategoryName = request.CategoryName,
            TypicalPayloadTonnes = request.TypicalPayloadTonnes,
            DefaultFuelBurnLph = request.DefaultFuelBurnLph,
            MaintenanceIntervalHours = request.MaintenanceIntervalHours,
            OilIntervalHours = request.OilIntervalHours,
        };
        OnBeforeCreate(request, item);
        _db.Set<EquipmentClass>().Add(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Created", Convert.ToString(item.EquipmentClassId) ?? string.Empty, $"Created EquipmentClass record {item.EquipmentClassId}.", ToDto(item), ct);
        OnAfterCreate(item);
        await NotifyResourceChangedAsync("Created", Convert.ToString(item.EquipmentClassId), ct);
        return CreatedAtAction(nameof(GetById), new { id = item.EquipmentClassId }, ApiResponse<EquipmentClassDto>.Success("record created", ToDto(item)));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateEquipmentClassRequest request, CancellationToken ct)
    {
        var item = await _db.Set<EquipmentClass>().FirstOrDefaultAsync(x => x.EquipmentClassId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeUpdate(item, request);
        item.ClassCode = request.ClassCode;
        item.ClassName = request.ClassName;
        item.CategoryName = request.CategoryName;
        item.TypicalPayloadTonnes = request.TypicalPayloadTonnes;
        item.DefaultFuelBurnLph = request.DefaultFuelBurnLph;
        item.MaintenanceIntervalHours = request.MaintenanceIntervalHours;
        item.OilIntervalHours = request.OilIntervalHours;
        var auditChanges = GetEntityChanges(_db.Entry(item));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Updated", Convert.ToString(item.EquipmentClassId) ?? string.Empty, $"Updated EquipmentClass record {item.EquipmentClassId}.", auditChanges, ct);
        await NotifyResourceChangedAsync("Updated", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record updated", new { updated = 1 }));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch(int id, UpdateEquipmentClassRequest request, CancellationToken ct)
    {
        return await Update(id, request, ct);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var item = await _db.Set<EquipmentClass>().FirstOrDefaultAsync(x => x.EquipmentClassId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeDelete(item);
        _db.Set<EquipmentClass>().Remove(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Deleted", Convert.ToString(id) ?? string.Empty, $"Hard deleted EquipmentClass record {id}.", ToDto(item), ct);
        await NotifyResourceChangedAsync("Deleted", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record deleted", new { deleted = 1, mode = "Hard" }));
    }

    [HttpPost("bulk/export")]
    public async Task<ActionResult<ApiResponse<PagedResult<EquipmentClassDto>>>> ExportBulk(BulkIdsRequest request, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return Ok(ApiResponse<PagedResult<EquipmentClassDto>>.Warning("no records selected", new PagedResult<EquipmentClassDto>(Array.Empty<EquipmentClassDto>(), page, pageSize, 0)));
        IQueryable<EquipmentClass> query = _db.Set<EquipmentClass>().AsNoTracking().Where(x => ids.Contains(x.EquipmentClassId));
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<EquipmentClassDto>>.Success("records exported", new PagedResult<EquipmentClassDto>(items, page, pageSize, total)));
    }

    [HttpPatch("bulk")]
    public async Task<IActionResult> UpdateBulk(BulkUpdateRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        if (string.IsNullOrWhiteSpace(request.Field)) return BadRequest(ApiResponse<object>.Error("error", new { error = "Choose a field to update." }));
        IQueryable<EquipmentClass> query = _db.Set<EquipmentClass>().Where(x => ids.Contains(x.EquipmentClassId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return NotFound(ApiResponse<object>.Warning("records not found"));
        if (!ApplyBulkUpdate(items, request, out var error)) return BadRequest(ApiResponse<object>.Error("error", new { error }));
        var auditChanges = items.ToDictionary(item => Convert.ToString(item.EquipmentClassId) ?? string.Empty, item => GetEntityChanges(_db.Entry(item)));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Updated", Convert.ToString(item.EquipmentClassId) ?? string.Empty, $"Updated EquipmentClass record {item.EquipmentClassId} in bulk update.", auditChanges[Convert.ToString(item.EquipmentClassId) ?? string.Empty], ct);
        await NotifyResourceChangedAsync("Updated", null, ct);
        return Ok(ApiResponse<object>.Success("records updated", new { updated = items.Count }));
    }

    [HttpPost("bulk/delete")]
    public async Task<IActionResult> DeleteBulk(BulkIdsRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        IQueryable<EquipmentClass> query = _db.Set<EquipmentClass>().Where(x => ids.Contains(x.EquipmentClassId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return Ok(ApiResponse<object>.Warning("records not found", new { deleted = 0 }));
        foreach (var item in items)
        {
            OnBeforeDelete(item);
        }
        _db.Set<EquipmentClass>().RemoveRange(items);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Deleted", Convert.ToString(item.EquipmentClassId) ?? string.Empty, $"Hard deleted EquipmentClass record {item.EquipmentClassId} in bulk delete.", ToDto(item), ct);
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

    private static bool ApplyBulkUpdate(IReadOnlyList<EquipmentClass> items, BulkUpdateRequest request, out string error)
    {
        error = string.Empty;
        return request.Field.Trim().ToLowerInvariant() switch
        {
            "classcode" => ApplyBulkClassCode(items, request.Value, out error),
            "classname" => ApplyBulkClassName(items, request.Value, out error),
            "categoryname" => ApplyBulkCategoryName(items, request.Value, out error),
            "typicalpayloadtonnes" => ApplyBulkTypicalPayloadTonnes(items, request.Value, out error),
            "defaultfuelburnlph" => ApplyBulkDefaultFuelBurnLph(items, request.Value, out error),
            "maintenanceintervalhours" => ApplyBulkMaintenanceIntervalHours(items, request.Value, out error),
            "oilintervalhours" => ApplyBulkOilIntervalHours(items, request.Value, out error),
            _ => FailBulkUpdate("Field is not bulk editable.", out error)
        };
    }

    private static bool ApplyBulkClassCode(IReadOnlyList<EquipmentClass> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.ClassCode = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkClassName(IReadOnlyList<EquipmentClass> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.ClassName = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkCategoryName(IReadOnlyList<EquipmentClass> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.CategoryName = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkTypicalPayloadTonnes(IReadOnlyList<EquipmentClass> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.TypicalPayloadTonnes = null;
            return true;
        }
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("TypicalPayloadTonnes requires a decimal value.", out error);
        foreach (var item in items) item.TypicalPayloadTonnes = value;
        return true;
    }

    private static bool ApplyBulkDefaultFuelBurnLph(IReadOnlyList<EquipmentClass> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.DefaultFuelBurnLph = null;
            return true;
        }
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("DefaultFuelBurnLph requires a decimal value.", out error);
        foreach (var item in items) item.DefaultFuelBurnLph = value;
        return true;
    }

    private static bool ApplyBulkMaintenanceIntervalHours(IReadOnlyList<EquipmentClass> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("MaintenanceIntervalHours requires a decimal value.", out error);
        foreach (var item in items) item.MaintenanceIntervalHours = value;
        return true;
    }

    private static bool ApplyBulkOilIntervalHours(IReadOnlyList<EquipmentClass> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("OilIntervalHours requires a decimal value.", out error);
        foreach (var item in items) item.OilIntervalHours = value;
        return true;
    }

    private static bool FailBulkUpdate(string message, out string error)
    {
        error = message;
        return false;
    }


    private static IQueryable<EquipmentClass> ApplySearch(IQueryable<EquipmentClass> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        search = search.Trim();
        return query.Where(x => (x.ClassCode != null && x.ClassCode.Contains(search)) || (x.ClassName != null && x.ClassName.Contains(search)) || (x.CategoryName != null && x.CategoryName.Contains(search)));
    }

    private static IQueryable<EquipmentClass> ApplyFilter(IQueryable<EquipmentClass> query, string? filterField, string? filterValue)
    {
        if (string.IsNullOrWhiteSpace(filterField) || string.IsNullOrWhiteSpace(filterValue)) return query;
        filterField = filterField.Trim();
        filterValue = filterValue.Trim();
        return filterField.ToLowerInvariant() switch
        {
            "equipmentclassid" => int.TryParse(filterValue, out var EquipmentClassIdValue) ? query.Where(x => x.EquipmentClassId == EquipmentClassIdValue) : query,
            "classcode" => query.Where(x => x.ClassCode != null && x.ClassCode.Contains(filterValue)),
            "classname" => query.Where(x => x.ClassName != null && x.ClassName.Contains(filterValue)),
            "categoryname" => query.Where(x => x.CategoryName != null && x.CategoryName.Contains(filterValue)),
            "typicalpayloadtonnes" => decimal.TryParse(filterValue, out var TypicalPayloadTonnesValue) ? query.Where(x => x.TypicalPayloadTonnes == TypicalPayloadTonnesValue) : query,
            "defaultfuelburnlph" => decimal.TryParse(filterValue, out var DefaultFuelBurnLphValue) ? query.Where(x => x.DefaultFuelBurnLph == DefaultFuelBurnLphValue) : query,
            "maintenanceintervalhours" => decimal.TryParse(filterValue, out var MaintenanceIntervalHoursValue) ? query.Where(x => x.MaintenanceIntervalHours == MaintenanceIntervalHoursValue) : query,
            "oilintervalhours" => decimal.TryParse(filterValue, out var OilIntervalHoursValue) ? query.Where(x => x.OilIntervalHours == OilIntervalHoursValue) : query,
            _ => query
        };
    }

    private static IQueryable<EquipmentClass> ApplySort(IQueryable<EquipmentClass> query, string? sortBy, string? sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) || string.Equals(sortDirection, "descending", StringComparison.OrdinalIgnoreCase);
        var field = string.IsNullOrWhiteSpace(sortBy) ? "EquipmentClassId" : sortBy.Trim();
        return field.ToLowerInvariant() switch
        {
            "equipmentclassid" => descending ? query.OrderByDescending(x => x.EquipmentClassId) : query.OrderBy(x => x.EquipmentClassId),
            "classcode" => descending ? query.OrderByDescending(x => x.ClassCode) : query.OrderBy(x => x.ClassCode),
            "classname" => descending ? query.OrderByDescending(x => x.ClassName) : query.OrderBy(x => x.ClassName),
            "categoryname" => descending ? query.OrderByDescending(x => x.CategoryName) : query.OrderBy(x => x.CategoryName),
            "typicalpayloadtonnes" => descending ? query.OrderByDescending(x => x.TypicalPayloadTonnes) : query.OrderBy(x => x.TypicalPayloadTonnes),
            "defaultfuelburnlph" => descending ? query.OrderByDescending(x => x.DefaultFuelBurnLph) : query.OrderBy(x => x.DefaultFuelBurnLph),
            "maintenanceintervalhours" => descending ? query.OrderByDescending(x => x.MaintenanceIntervalHours) : query.OrderBy(x => x.MaintenanceIntervalHours),
            "oilintervalhours" => descending ? query.OrderByDescending(x => x.OilIntervalHours) : query.OrderBy(x => x.OilIntervalHours),
            _ => descending ? query.OrderByDescending(x => x.EquipmentClassId) : query.OrderBy(x => x.EquipmentClassId)
        };
    }
    private static EquipmentClassDto ToDto(EquipmentClass item) => new(
        item.EquipmentClassId,
        item.ClassCode,
        item.ClassName,
        item.CategoryName,
        item.TypicalPayloadTonnes,
        item.DefaultFuelBurnLph,
        item.MaintenanceIntervalHours,
        item.OilIntervalHours
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
            Resource = "EquipmentClass",
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
        _changes.Clients.All.SendAsync(DataChangeHub.DataChangedMethod, new DataChangeNotification("EquipmentClass", action, resourceKey, DateTimeOffset.UtcNow), ct);

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
