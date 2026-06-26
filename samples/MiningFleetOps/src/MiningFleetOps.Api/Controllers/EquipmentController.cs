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
[Route("api/equipments")]
public sealed partial class EquipmentController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<DataChangeHub> _changes;

    public EquipmentController(AppDbContext db, IHubContext<DataChangeHub> changes)
    {
        _db = db;
        _changes = changes;
    }

    partial void OnBeforeCreate(CreateEquipmentRequest request, Equipment item);
    partial void OnAfterCreate(Equipment item);
    partial void OnBeforeUpdate(Equipment item, UpdateEquipmentRequest request);
    partial void OnBeforeDelete(Equipment item);

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<EquipmentDto>>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null, [FromQuery] string? filterField = null, [FromQuery] string? filterValue = null, [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        IQueryable<Equipment> query = _db.Set<Equipment>().AsNoTracking();
        query = ApplySearch(query, search);
        query = ApplyFilter(query, filterField, filterValue);
        query = ApplySort(query, sortBy, sortDirection);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<EquipmentDto>>.Success("records loaded", new PagedResult<EquipmentDto>(items, page, pageSize, total)));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<EquipmentDto>>> GetById(int id, CancellationToken ct)
    {
        IQueryable<Equipment> query = _db.Set<Equipment>().AsNoTracking();
        var item = await query.FirstOrDefaultAsync(x => x.EquipmentId!.Equals(id), ct);
        return item is null ? NotFound(ApiResponse<object>.Warning("record not found")) : Ok(ApiResponse<EquipmentDto>.Success("record loaded", ToDto(item)));
    }

    [HttpGet("{id}/history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuditTrailDto>>>> GetHistory(int id, CancellationToken ct)
    {
        var canReadRecord = await _db.Set<Equipment>().AsNoTracking().AnyAsync(x => x.EquipmentId!.Equals(id), ct);
        if (!canReadRecord) return NotFound(ApiResponse<object>.Warning("record not found"));
        await EnsureAuditTrailTableAsync(ct);
        var resourceKey = Convert.ToString(id) ?? string.Empty;
        var history = await _db.AuditTrailEntries
            .AsNoTracking()
            .Where(entry => entry.Resource == "Equipment" && entry.ResourceKey == resourceKey)
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .Take(100)
            .Select(entry => ToAuditTrailDto(entry))
            .ToListAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<AuditTrailDto>>.Success("activity loaded", history));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<EquipmentDto>>> Create(CreateEquipmentRequest request, CancellationToken ct)
    {
        var item = new Equipment
        {
            SiteId = request.SiteId,
            EquipmentClassId = request.EquipmentClassId,
            AssetTag = request.AssetTag,
            SerialNumber = request.SerialNumber,
            Manufacturer = request.Manufacturer,
            Model = request.Model,
            CommissionDate = request.CommissionDate,
            FuelTypeId = request.FuelTypeId,
            TankCapacityL = request.TankCapacityL,
            CurrentHourMeter = request.CurrentHourMeter,
            CurrentOdometerKm = request.CurrentOdometerKm,
            Status = request.Status,
            IsActive = request.IsActive,
            CreatedAt = request.CreatedAt,
        };
        OnBeforeCreate(request, item);
        _db.Set<Equipment>().Add(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Created", Convert.ToString(item.EquipmentId) ?? string.Empty, $"Created Equipment record {item.EquipmentId}.", ToDto(item), ct);
        OnAfterCreate(item);
        await NotifyResourceChangedAsync("Created", Convert.ToString(item.EquipmentId), ct);
        return CreatedAtAction(nameof(GetById), new { id = item.EquipmentId }, ApiResponse<EquipmentDto>.Success("record created", ToDto(item)));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateEquipmentRequest request, CancellationToken ct)
    {
        var item = await _db.Set<Equipment>().FirstOrDefaultAsync(x => x.EquipmentId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeUpdate(item, request);
        item.SiteId = request.SiteId;
        item.EquipmentClassId = request.EquipmentClassId;
        item.AssetTag = request.AssetTag;
        item.SerialNumber = request.SerialNumber;
        item.Manufacturer = request.Manufacturer;
        item.Model = request.Model;
        item.CommissionDate = request.CommissionDate;
        item.FuelTypeId = request.FuelTypeId;
        item.TankCapacityL = request.TankCapacityL;
        item.CurrentHourMeter = request.CurrentHourMeter;
        item.CurrentOdometerKm = request.CurrentOdometerKm;
        item.Status = request.Status;
        item.IsActive = request.IsActive;
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
        await LogAuditTrailAsync("Updated", Convert.ToString(item.EquipmentId) ?? string.Empty, $"Updated Equipment record {item.EquipmentId}.", auditChanges, ct);
        await NotifyResourceChangedAsync("Updated", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record updated", new { updated = 1 }));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch(int id, UpdateEquipmentRequest request, CancellationToken ct)
    {
        return await Update(id, request, ct);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var item = await _db.Set<Equipment>().FirstOrDefaultAsync(x => x.EquipmentId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeDelete(item);
        _db.Set<Equipment>().Remove(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Deleted", Convert.ToString(id) ?? string.Empty, $"Hard deleted Equipment record {id}.", ToDto(item), ct);
        await NotifyResourceChangedAsync("Deleted", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record deleted", new { deleted = 1, mode = "Hard" }));
    }

    [HttpPost("bulk/export")]
    public async Task<ActionResult<ApiResponse<PagedResult<EquipmentDto>>>> ExportBulk(BulkIdsRequest request, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return Ok(ApiResponse<PagedResult<EquipmentDto>>.Warning("no records selected", new PagedResult<EquipmentDto>(Array.Empty<EquipmentDto>(), page, pageSize, 0)));
        IQueryable<Equipment> query = _db.Set<Equipment>().AsNoTracking().Where(x => ids.Contains(x.EquipmentId));
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<EquipmentDto>>.Success("records exported", new PagedResult<EquipmentDto>(items, page, pageSize, total)));
    }

    [HttpPatch("bulk")]
    public async Task<IActionResult> UpdateBulk(BulkUpdateRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        if (string.IsNullOrWhiteSpace(request.Field)) return BadRequest(ApiResponse<object>.Error("error", new { error = "Choose a field to update." }));
        IQueryable<Equipment> query = _db.Set<Equipment>().Where(x => ids.Contains(x.EquipmentId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return NotFound(ApiResponse<object>.Warning("records not found"));
        if (!ApplyBulkUpdate(items, request, out var error)) return BadRequest(ApiResponse<object>.Error("error", new { error }));
        var auditChanges = items.ToDictionary(item => Convert.ToString(item.EquipmentId) ?? string.Empty, item => GetEntityChanges(_db.Entry(item)));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Updated", Convert.ToString(item.EquipmentId) ?? string.Empty, $"Updated Equipment record {item.EquipmentId} in bulk update.", auditChanges[Convert.ToString(item.EquipmentId) ?? string.Empty], ct);
        await NotifyResourceChangedAsync("Updated", null, ct);
        return Ok(ApiResponse<object>.Success("records updated", new { updated = items.Count }));
    }

    [HttpPost("bulk/delete")]
    public async Task<IActionResult> DeleteBulk(BulkIdsRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        IQueryable<Equipment> query = _db.Set<Equipment>().Where(x => ids.Contains(x.EquipmentId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return Ok(ApiResponse<object>.Warning("records not found", new { deleted = 0 }));
        foreach (var item in items)
        {
            OnBeforeDelete(item);
        }
        _db.Set<Equipment>().RemoveRange(items);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Deleted", Convert.ToString(item.EquipmentId) ?? string.Empty, $"Hard deleted Equipment record {item.EquipmentId} in bulk delete.", ToDto(item), ct);
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

    private static bool ApplyBulkUpdate(IReadOnlyList<Equipment> items, BulkUpdateRequest request, out string error)
    {
        error = string.Empty;
        return request.Field.Trim().ToLowerInvariant() switch
        {
            "siteid" => ApplyBulkSiteId(items, request.Value, out error),
            "equipmentclassid" => ApplyBulkEquipmentClassId(items, request.Value, out error),
            "assettag" => ApplyBulkAssetTag(items, request.Value, out error),
            "serialnumber" => ApplyBulkSerialNumber(items, request.Value, out error),
            "manufacturer" => ApplyBulkManufacturer(items, request.Value, out error),
            "model" => ApplyBulkModel(items, request.Value, out error),
            "commissiondate" => ApplyBulkCommissionDate(items, request.Value, out error),
            "fueltypeid" => ApplyBulkFuelTypeId(items, request.Value, out error),
            "tankcapacityl" => ApplyBulkTankCapacityL(items, request.Value, out error),
            "currenthourmeter" => ApplyBulkCurrentHourMeter(items, request.Value, out error),
            "currentodometerkm" => ApplyBulkCurrentOdometerKm(items, request.Value, out error),
            "status" => ApplyBulkStatus(items, request.Value, out error),
            "isactive" => ApplyBulkIsActive(items, request.Value, out error),
            "createdat" => ApplyBulkCreatedAt(items, request.Value, out error),
            _ => FailBulkUpdate("Field is not bulk editable.", out error)
        };
    }

    private static bool ApplyBulkSiteId(IReadOnlyList<Equipment> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("SiteId requires a int value.", out error);
        foreach (var item in items) item.SiteId = value;
        return true;
    }

    private static bool ApplyBulkEquipmentClassId(IReadOnlyList<Equipment> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("EquipmentClassId requires a int value.", out error);
        foreach (var item in items) item.EquipmentClassId = value;
        return true;
    }

    private static bool ApplyBulkAssetTag(IReadOnlyList<Equipment> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.AssetTag = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkSerialNumber(IReadOnlyList<Equipment> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.SerialNumber = null;
            return true;
        }
        foreach (var item in items) item.SerialNumber = raw;
        return true;
    }

    private static bool ApplyBulkManufacturer(IReadOnlyList<Equipment> items, string? raw, out string error)
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

    private static bool ApplyBulkModel(IReadOnlyList<Equipment> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.Model = null;
            return true;
        }
        foreach (var item in items) item.Model = raw;
        return true;
    }

    private static bool ApplyBulkCommissionDate(IReadOnlyList<Equipment> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.CommissionDate = null;
            return true;
        }
        if (!DateTime.TryParse(raw, out var value)) return FailBulkUpdate("CommissionDate requires a DateTime value.", out error);
        foreach (var item in items) item.CommissionDate = value;
        return true;
    }

    private static bool ApplyBulkFuelTypeId(IReadOnlyList<Equipment> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("FuelTypeId requires a int value.", out error);
        foreach (var item in items) item.FuelTypeId = value;
        return true;
    }

    private static bool ApplyBulkTankCapacityL(IReadOnlyList<Equipment> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.TankCapacityL = null;
            return true;
        }
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("TankCapacityL requires a decimal value.", out error);
        foreach (var item in items) item.TankCapacityL = value;
        return true;
    }

    private static bool ApplyBulkCurrentHourMeter(IReadOnlyList<Equipment> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("CurrentHourMeter requires a decimal value.", out error);
        foreach (var item in items) item.CurrentHourMeter = value;
        return true;
    }

    private static bool ApplyBulkCurrentOdometerKm(IReadOnlyList<Equipment> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.CurrentOdometerKm = null;
            return true;
        }
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("CurrentOdometerKm requires a decimal value.", out error);
        foreach (var item in items) item.CurrentOdometerKm = value;
        return true;
    }

    private static bool ApplyBulkStatus(IReadOnlyList<Equipment> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.Status = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkIsActive(IReadOnlyList<Equipment> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!bool.TryParse(raw, out var value)) return FailBulkUpdate("IsActive requires a boolean value.", out error);
        foreach (var item in items) item.IsActive = value;
        return true;
    }

    private static bool ApplyBulkCreatedAt(IReadOnlyList<Equipment> items, string? raw, out string error)
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


    private static IQueryable<Equipment> ApplySearch(IQueryable<Equipment> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        search = search.Trim();
        return query.Where(x => (x.AssetTag != null && x.AssetTag.Contains(search)) || (x.SerialNumber != null && x.SerialNumber.Contains(search)) || (x.Manufacturer != null && x.Manufacturer.Contains(search)) || (x.Model != null && x.Model.Contains(search)) || (x.Status != null && x.Status.Contains(search)));
    }

    private static IQueryable<Equipment> ApplyFilter(IQueryable<Equipment> query, string? filterField, string? filterValue)
    {
        if (string.IsNullOrWhiteSpace(filterField) || string.IsNullOrWhiteSpace(filterValue)) return query;
        filterField = filterField.Trim();
        filterValue = filterValue.Trim();
        return filterField.ToLowerInvariant() switch
        {
            "equipmentid" => int.TryParse(filterValue, out var EquipmentIdValue) ? query.Where(x => x.EquipmentId == EquipmentIdValue) : query,
            "siteid" => int.TryParse(filterValue, out var SiteIdValue) ? query.Where(x => x.SiteId == SiteIdValue) : query,
            "equipmentclassid" => int.TryParse(filterValue, out var EquipmentClassIdValue) ? query.Where(x => x.EquipmentClassId == EquipmentClassIdValue) : query,
            "assettag" => query.Where(x => x.AssetTag != null && x.AssetTag.Contains(filterValue)),
            "serialnumber" => query.Where(x => x.SerialNumber != null && x.SerialNumber.Contains(filterValue)),
            "manufacturer" => query.Where(x => x.Manufacturer != null && x.Manufacturer.Contains(filterValue)),
            "model" => query.Where(x => x.Model != null && x.Model.Contains(filterValue)),
            "commissiondate" => DateTime.TryParse(filterValue, out var CommissionDateValue) ? query.Where(x => x.CommissionDate == CommissionDateValue) : query,
            "fueltypeid" => int.TryParse(filterValue, out var FuelTypeIdValue) ? query.Where(x => x.FuelTypeId == FuelTypeIdValue) : query,
            "tankcapacityl" => decimal.TryParse(filterValue, out var TankCapacityLValue) ? query.Where(x => x.TankCapacityL == TankCapacityLValue) : query,
            "currenthourmeter" => decimal.TryParse(filterValue, out var CurrentHourMeterValue) ? query.Where(x => x.CurrentHourMeter == CurrentHourMeterValue) : query,
            "currentodometerkm" => decimal.TryParse(filterValue, out var CurrentOdometerKmValue) ? query.Where(x => x.CurrentOdometerKm == CurrentOdometerKmValue) : query,
            "status" => query.Where(x => x.Status != null && x.Status.Contains(filterValue)),
            "isactive" => bool.TryParse(filterValue, out var IsActiveValue) ? query.Where(x => x.IsActive == IsActiveValue) : query,
            "createdat" => DateTime.TryParse(filterValue, out var CreatedAtValue) ? query.Where(x => x.CreatedAt == CreatedAtValue) : query,
            _ => query
        };
    }

    private static IQueryable<Equipment> ApplySort(IQueryable<Equipment> query, string? sortBy, string? sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) || string.Equals(sortDirection, "descending", StringComparison.OrdinalIgnoreCase);
        var field = string.IsNullOrWhiteSpace(sortBy) ? "EquipmentId" : sortBy.Trim();
        return field.ToLowerInvariant() switch
        {
            "equipmentid" => descending ? query.OrderByDescending(x => x.EquipmentId) : query.OrderBy(x => x.EquipmentId),
            "siteid" => descending ? query.OrderByDescending(x => x.SiteId) : query.OrderBy(x => x.SiteId),
            "equipmentclassid" => descending ? query.OrderByDescending(x => x.EquipmentClassId) : query.OrderBy(x => x.EquipmentClassId),
            "assettag" => descending ? query.OrderByDescending(x => x.AssetTag) : query.OrderBy(x => x.AssetTag),
            "serialnumber" => descending ? query.OrderByDescending(x => x.SerialNumber) : query.OrderBy(x => x.SerialNumber),
            "manufacturer" => descending ? query.OrderByDescending(x => x.Manufacturer) : query.OrderBy(x => x.Manufacturer),
            "model" => descending ? query.OrderByDescending(x => x.Model) : query.OrderBy(x => x.Model),
            "commissiondate" => descending ? query.OrderByDescending(x => x.CommissionDate) : query.OrderBy(x => x.CommissionDate),
            "fueltypeid" => descending ? query.OrderByDescending(x => x.FuelTypeId) : query.OrderBy(x => x.FuelTypeId),
            "tankcapacityl" => descending ? query.OrderByDescending(x => x.TankCapacityL) : query.OrderBy(x => x.TankCapacityL),
            "currenthourmeter" => descending ? query.OrderByDescending(x => x.CurrentHourMeter) : query.OrderBy(x => x.CurrentHourMeter),
            "currentodometerkm" => descending ? query.OrderByDescending(x => x.CurrentOdometerKm) : query.OrderBy(x => x.CurrentOdometerKm),
            "status" => descending ? query.OrderByDescending(x => x.Status) : query.OrderBy(x => x.Status),
            "isactive" => descending ? query.OrderByDescending(x => x.IsActive) : query.OrderBy(x => x.IsActive),
            "createdat" => descending ? query.OrderByDescending(x => x.CreatedAt) : query.OrderBy(x => x.CreatedAt),
            _ => descending ? query.OrderByDescending(x => x.EquipmentId) : query.OrderBy(x => x.EquipmentId)
        };
    }
    private static EquipmentDto ToDto(Equipment item) => new(
        item.EquipmentId,
        item.SiteId,
        item.EquipmentClassId,
        item.AssetTag,
        item.SerialNumber,
        item.Manufacturer,
        item.Model,
        item.CommissionDate,
        item.FuelTypeId,
        item.TankCapacityL,
        item.CurrentHourMeter,
        item.CurrentOdometerKm,
        item.Status,
        item.IsActive,
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
            Resource = "Equipment",
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
        _changes.Clients.All.SendAsync(DataChangeHub.DataChangedMethod, new DataChangeNotification("Equipment", action, resourceKey, DateTimeOffset.UtcNow), ct);

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
