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
[Route("api/fuelLogs")]
public sealed partial class FuelLogController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<DataChangeHub> _changes;

    public FuelLogController(AppDbContext db, IHubContext<DataChangeHub> changes)
    {
        _db = db;
        _changes = changes;
    }

    partial void OnBeforeCreate(CreateFuelLogRequest request, FuelLog item);
    partial void OnAfterCreate(FuelLog item);
    partial void OnBeforeUpdate(FuelLog item, UpdateFuelLogRequest request);
    partial void OnBeforeDelete(FuelLog item);

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<FuelLogDto>>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null, [FromQuery] string? filterField = null, [FromQuery] string? filterValue = null, [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        IQueryable<FuelLog> query = _db.Set<FuelLog>().AsNoTracking();
        query = ApplySearch(query, search);
        query = ApplyFilter(query, filterField, filterValue);
        query = ApplySort(query, sortBy, sortDirection);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<FuelLogDto>>.Success("records loaded", new PagedResult<FuelLogDto>(items, page, pageSize, total)));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<FuelLogDto>>> GetById(long id, CancellationToken ct)
    {
        IQueryable<FuelLog> query = _db.Set<FuelLog>().AsNoTracking();
        var item = await query.FirstOrDefaultAsync(x => x.FuelLogId!.Equals(id), ct);
        return item is null ? NotFound(ApiResponse<object>.Warning("record not found")) : Ok(ApiResponse<FuelLogDto>.Success("record loaded", ToDto(item)));
    }

    [HttpGet("{id}/history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuditTrailDto>>>> GetHistory(long id, CancellationToken ct)
    {
        var canReadRecord = await _db.Set<FuelLog>().AsNoTracking().AnyAsync(x => x.FuelLogId!.Equals(id), ct);
        if (!canReadRecord) return NotFound(ApiResponse<object>.Warning("record not found"));
        await EnsureAuditTrailTableAsync(ct);
        var resourceKey = Convert.ToString(id) ?? string.Empty;
        var history = await _db.AuditTrailEntries
            .AsNoTracking()
            .Where(entry => entry.Resource == "FuelLog" && entry.ResourceKey == resourceKey)
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .Take(100)
            .Select(entry => ToAuditTrailDto(entry))
            .ToListAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<AuditTrailDto>>.Success("activity loaded", history));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<FuelLogDto>>> Create(CreateFuelLogRequest request, CancellationToken ct)
    {
        var item = new FuelLog
        {
            EquipmentId = request.EquipmentId,
            FuelTypeId = request.FuelTypeId,
            FueledAt = request.FueledAt,
            ShiftId = request.ShiftId,
            EmployeeId = request.EmployeeId,
            PitId = request.PitId,
            HourMeter = request.HourMeter,
            OdometerKm = request.OdometerKm,
            Liters = request.Liters,
            UnitCost = request.UnitCost,
            HoursSinceLastFuel = request.HoursSinceLastFuel,
            FuelBurnLph = request.FuelBurnLph,
            Co2KgPerL = request.Co2KgPerL,
            SourceName = request.SourceName,
            Notes = request.Notes,
            CreatedAt = request.CreatedAt,
            CostAmount = request.CostAmount,
            Co2Kg = request.Co2Kg,
        };
        OnBeforeCreate(request, item);
        _db.Set<FuelLog>().Add(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Created", Convert.ToString(item.FuelLogId) ?? string.Empty, $"Created FuelLog record {item.FuelLogId}.", ToDto(item), ct);
        OnAfterCreate(item);
        await NotifyResourceChangedAsync("Created", Convert.ToString(item.FuelLogId), ct);
        return CreatedAtAction(nameof(GetById), new { id = item.FuelLogId }, ApiResponse<FuelLogDto>.Success("record created", ToDto(item)));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, UpdateFuelLogRequest request, CancellationToken ct)
    {
        var item = await _db.Set<FuelLog>().FirstOrDefaultAsync(x => x.FuelLogId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeUpdate(item, request);
        item.EquipmentId = request.EquipmentId;
        item.FuelTypeId = request.FuelTypeId;
        item.FueledAt = request.FueledAt;
        item.ShiftId = request.ShiftId;
        item.EmployeeId = request.EmployeeId;
        item.PitId = request.PitId;
        item.HourMeter = request.HourMeter;
        item.OdometerKm = request.OdometerKm;
        item.Liters = request.Liters;
        item.UnitCost = request.UnitCost;
        item.HoursSinceLastFuel = request.HoursSinceLastFuel;
        item.FuelBurnLph = request.FuelBurnLph;
        item.Co2KgPerL = request.Co2KgPerL;
        item.SourceName = request.SourceName;
        item.Notes = request.Notes;
        item.CreatedAt = request.CreatedAt;
        item.CostAmount = request.CostAmount;
        item.Co2Kg = request.Co2Kg;
        var auditChanges = GetEntityChanges(_db.Entry(item));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Updated", Convert.ToString(item.FuelLogId) ?? string.Empty, $"Updated FuelLog record {item.FuelLogId}.", auditChanges, ct);
        await NotifyResourceChangedAsync("Updated", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record updated", new { updated = 1 }));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch(long id, UpdateFuelLogRequest request, CancellationToken ct)
    {
        return await Update(id, request, ct);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var item = await _db.Set<FuelLog>().FirstOrDefaultAsync(x => x.FuelLogId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeDelete(item);
        _db.Set<FuelLog>().Remove(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Deleted", Convert.ToString(id) ?? string.Empty, $"Hard deleted FuelLog record {id}.", ToDto(item), ct);
        await NotifyResourceChangedAsync("Deleted", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record deleted", new { deleted = 1, mode = "Hard" }));
    }

    [HttpPost("bulk/export")]
    public async Task<ActionResult<ApiResponse<PagedResult<FuelLogDto>>>> ExportBulk(BulkIdsRequest request, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return Ok(ApiResponse<PagedResult<FuelLogDto>>.Warning("no records selected", new PagedResult<FuelLogDto>(Array.Empty<FuelLogDto>(), page, pageSize, 0)));
        IQueryable<FuelLog> query = _db.Set<FuelLog>().AsNoTracking().Where(x => ids.Contains(x.FuelLogId));
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<FuelLogDto>>.Success("records exported", new PagedResult<FuelLogDto>(items, page, pageSize, total)));
    }

    [HttpPatch("bulk")]
    public async Task<IActionResult> UpdateBulk(BulkUpdateRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        if (string.IsNullOrWhiteSpace(request.Field)) return BadRequest(ApiResponse<object>.Error("error", new { error = "Choose a field to update." }));
        IQueryable<FuelLog> query = _db.Set<FuelLog>().Where(x => ids.Contains(x.FuelLogId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return NotFound(ApiResponse<object>.Warning("records not found"));
        if (!ApplyBulkUpdate(items, request, out var error)) return BadRequest(ApiResponse<object>.Error("error", new { error }));
        var auditChanges = items.ToDictionary(item => Convert.ToString(item.FuelLogId) ?? string.Empty, item => GetEntityChanges(_db.Entry(item)));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Updated", Convert.ToString(item.FuelLogId) ?? string.Empty, $"Updated FuelLog record {item.FuelLogId} in bulk update.", auditChanges[Convert.ToString(item.FuelLogId) ?? string.Empty], ct);
        await NotifyResourceChangedAsync("Updated", null, ct);
        return Ok(ApiResponse<object>.Success("records updated", new { updated = items.Count }));
    }

    [HttpPost("bulk/delete")]
    public async Task<IActionResult> DeleteBulk(BulkIdsRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        IQueryable<FuelLog> query = _db.Set<FuelLog>().Where(x => ids.Contains(x.FuelLogId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return Ok(ApiResponse<object>.Warning("records not found", new { deleted = 0 }));
        foreach (var item in items)
        {
            OnBeforeDelete(item);
        }
        _db.Set<FuelLog>().RemoveRange(items);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Deleted", Convert.ToString(item.FuelLogId) ?? string.Empty, $"Hard deleted FuelLog record {item.FuelLogId} in bulk delete.", ToDto(item), ct);
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

    private static bool ApplyBulkUpdate(IReadOnlyList<FuelLog> items, BulkUpdateRequest request, out string error)
    {
        error = string.Empty;
        return request.Field.Trim().ToLowerInvariant() switch
        {
            "equipmentid" => ApplyBulkEquipmentId(items, request.Value, out error),
            "fueltypeid" => ApplyBulkFuelTypeId(items, request.Value, out error),
            "fueledat" => ApplyBulkFueledAt(items, request.Value, out error),
            "shiftid" => ApplyBulkShiftId(items, request.Value, out error),
            "employeeid" => ApplyBulkEmployeeId(items, request.Value, out error),
            "pitid" => ApplyBulkPitId(items, request.Value, out error),
            "hourmeter" => ApplyBulkHourMeter(items, request.Value, out error),
            "odometerkm" => ApplyBulkOdometerKm(items, request.Value, out error),
            "liters" => ApplyBulkLiters(items, request.Value, out error),
            "unitcost" => ApplyBulkUnitCost(items, request.Value, out error),
            "hourssincelastfuel" => ApplyBulkHoursSinceLastFuel(items, request.Value, out error),
            "fuelburnlph" => ApplyBulkFuelBurnLph(items, request.Value, out error),
            "co2kgperl" => ApplyBulkCo2KgPerL(items, request.Value, out error),
            "sourcename" => ApplyBulkSourceName(items, request.Value, out error),
            "notes" => ApplyBulkNotes(items, request.Value, out error),
            "createdat" => ApplyBulkCreatedAt(items, request.Value, out error),
            "costamount" => ApplyBulkCostAmount(items, request.Value, out error),
            "co2kg" => ApplyBulkCo2Kg(items, request.Value, out error),
            _ => FailBulkUpdate("Field is not bulk editable.", out error)
        };
    }

    private static bool ApplyBulkEquipmentId(IReadOnlyList<FuelLog> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("EquipmentId requires a int value.", out error);
        foreach (var item in items) item.EquipmentId = value;
        return true;
    }

    private static bool ApplyBulkFuelTypeId(IReadOnlyList<FuelLog> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("FuelTypeId requires a int value.", out error);
        foreach (var item in items) item.FuelTypeId = value;
        return true;
    }

    private static bool ApplyBulkFueledAt(IReadOnlyList<FuelLog> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!DateTime.TryParse(raw, out var value)) return FailBulkUpdate("FueledAt requires a DateTime value.", out error);
        foreach (var item in items) item.FueledAt = value;
        return true;
    }

    private static bool ApplyBulkShiftId(IReadOnlyList<FuelLog> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.ShiftId = null;
            return true;
        }
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("ShiftId requires a int value.", out error);
        foreach (var item in items) item.ShiftId = value;
        return true;
    }

    private static bool ApplyBulkEmployeeId(IReadOnlyList<FuelLog> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.EmployeeId = null;
            return true;
        }
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("EmployeeId requires a int value.", out error);
        foreach (var item in items) item.EmployeeId = value;
        return true;
    }

    private static bool ApplyBulkPitId(IReadOnlyList<FuelLog> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.PitId = null;
            return true;
        }
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("PitId requires a int value.", out error);
        foreach (var item in items) item.PitId = value;
        return true;
    }

    private static bool ApplyBulkHourMeter(IReadOnlyList<FuelLog> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("HourMeter requires a decimal value.", out error);
        foreach (var item in items) item.HourMeter = value;
        return true;
    }

    private static bool ApplyBulkOdometerKm(IReadOnlyList<FuelLog> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.OdometerKm = null;
            return true;
        }
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("OdometerKm requires a decimal value.", out error);
        foreach (var item in items) item.OdometerKm = value;
        return true;
    }

    private static bool ApplyBulkLiters(IReadOnlyList<FuelLog> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("Liters requires a decimal value.", out error);
        foreach (var item in items) item.Liters = value;
        return true;
    }

    private static bool ApplyBulkUnitCost(IReadOnlyList<FuelLog> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.UnitCost = null;
            return true;
        }
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("UnitCost requires a decimal value.", out error);
        foreach (var item in items) item.UnitCost = value;
        return true;
    }

    private static bool ApplyBulkHoursSinceLastFuel(IReadOnlyList<FuelLog> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.HoursSinceLastFuel = null;
            return true;
        }
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("HoursSinceLastFuel requires a decimal value.", out error);
        foreach (var item in items) item.HoursSinceLastFuel = value;
        return true;
    }

    private static bool ApplyBulkFuelBurnLph(IReadOnlyList<FuelLog> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.FuelBurnLph = null;
            return true;
        }
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("FuelBurnLph requires a decimal value.", out error);
        foreach (var item in items) item.FuelBurnLph = value;
        return true;
    }

    private static bool ApplyBulkCo2KgPerL(IReadOnlyList<FuelLog> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("Co2KgPerL requires a decimal value.", out error);
        foreach (var item in items) item.Co2KgPerL = value;
        return true;
    }

    private static bool ApplyBulkSourceName(IReadOnlyList<FuelLog> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.SourceName = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkNotes(IReadOnlyList<FuelLog> items, string? raw, out string error)
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

    private static bool ApplyBulkCreatedAt(IReadOnlyList<FuelLog> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!DateTime.TryParse(raw, out var value)) return FailBulkUpdate("CreatedAt requires a DateTime value.", out error);
        foreach (var item in items) item.CreatedAt = value;
        return true;
    }

    private static bool ApplyBulkCostAmount(IReadOnlyList<FuelLog> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.CostAmount = null;
            return true;
        }
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("CostAmount requires a decimal value.", out error);
        foreach (var item in items) item.CostAmount = value;
        return true;
    }

    private static bool ApplyBulkCo2Kg(IReadOnlyList<FuelLog> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.Co2Kg = null;
            return true;
        }
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("Co2Kg requires a decimal value.", out error);
        foreach (var item in items) item.Co2Kg = value;
        return true;
    }

    private static bool FailBulkUpdate(string message, out string error)
    {
        error = message;
        return false;
    }


    private static IQueryable<FuelLog> ApplySearch(IQueryable<FuelLog> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        search = search.Trim();
        return query.Where(x => (x.SourceName != null && x.SourceName.Contains(search)) || (x.Notes != null && x.Notes.Contains(search)));
    }

    private static IQueryable<FuelLog> ApplyFilter(IQueryable<FuelLog> query, string? filterField, string? filterValue)
    {
        if (string.IsNullOrWhiteSpace(filterField) || string.IsNullOrWhiteSpace(filterValue)) return query;
        filterField = filterField.Trim();
        filterValue = filterValue.Trim();
        return filterField.ToLowerInvariant() switch
        {
            "fuellogid" => long.TryParse(filterValue, out var FuelLogIdValue) ? query.Where(x => x.FuelLogId == FuelLogIdValue) : query,
            "equipmentid" => int.TryParse(filterValue, out var EquipmentIdValue) ? query.Where(x => x.EquipmentId == EquipmentIdValue) : query,
            "fueltypeid" => int.TryParse(filterValue, out var FuelTypeIdValue) ? query.Where(x => x.FuelTypeId == FuelTypeIdValue) : query,
            "fueledat" => DateTime.TryParse(filterValue, out var FueledAtValue) ? query.Where(x => x.FueledAt == FueledAtValue) : query,
            "shiftid" => int.TryParse(filterValue, out var ShiftIdValue) ? query.Where(x => x.ShiftId == ShiftIdValue) : query,
            "employeeid" => int.TryParse(filterValue, out var EmployeeIdValue) ? query.Where(x => x.EmployeeId == EmployeeIdValue) : query,
            "pitid" => int.TryParse(filterValue, out var PitIdValue) ? query.Where(x => x.PitId == PitIdValue) : query,
            "hourmeter" => decimal.TryParse(filterValue, out var HourMeterValue) ? query.Where(x => x.HourMeter == HourMeterValue) : query,
            "odometerkm" => decimal.TryParse(filterValue, out var OdometerKmValue) ? query.Where(x => x.OdometerKm == OdometerKmValue) : query,
            "liters" => decimal.TryParse(filterValue, out var LitersValue) ? query.Where(x => x.Liters == LitersValue) : query,
            "unitcost" => decimal.TryParse(filterValue, out var UnitCostValue) ? query.Where(x => x.UnitCost == UnitCostValue) : query,
            "hourssincelastfuel" => decimal.TryParse(filterValue, out var HoursSinceLastFuelValue) ? query.Where(x => x.HoursSinceLastFuel == HoursSinceLastFuelValue) : query,
            "fuelburnlph" => decimal.TryParse(filterValue, out var FuelBurnLphValue) ? query.Where(x => x.FuelBurnLph == FuelBurnLphValue) : query,
            "co2kgperl" => decimal.TryParse(filterValue, out var Co2KgPerLValue) ? query.Where(x => x.Co2KgPerL == Co2KgPerLValue) : query,
            "sourcename" => query.Where(x => x.SourceName != null && x.SourceName.Contains(filterValue)),
            "notes" => query.Where(x => x.Notes != null && x.Notes.Contains(filterValue)),
            "createdat" => DateTime.TryParse(filterValue, out var CreatedAtValue) ? query.Where(x => x.CreatedAt == CreatedAtValue) : query,
            "costamount" => decimal.TryParse(filterValue, out var CostAmountValue) ? query.Where(x => x.CostAmount == CostAmountValue) : query,
            "co2kg" => decimal.TryParse(filterValue, out var Co2KgValue) ? query.Where(x => x.Co2Kg == Co2KgValue) : query,
            _ => query
        };
    }

    private static IQueryable<FuelLog> ApplySort(IQueryable<FuelLog> query, string? sortBy, string? sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) || string.Equals(sortDirection, "descending", StringComparison.OrdinalIgnoreCase);
        var field = string.IsNullOrWhiteSpace(sortBy) ? "FuelLogId" : sortBy.Trim();
        return field.ToLowerInvariant() switch
        {
            "fuellogid" => descending ? query.OrderByDescending(x => x.FuelLogId) : query.OrderBy(x => x.FuelLogId),
            "equipmentid" => descending ? query.OrderByDescending(x => x.EquipmentId) : query.OrderBy(x => x.EquipmentId),
            "fueltypeid" => descending ? query.OrderByDescending(x => x.FuelTypeId) : query.OrderBy(x => x.FuelTypeId),
            "fueledat" => descending ? query.OrderByDescending(x => x.FueledAt) : query.OrderBy(x => x.FueledAt),
            "shiftid" => descending ? query.OrderByDescending(x => x.ShiftId) : query.OrderBy(x => x.ShiftId),
            "employeeid" => descending ? query.OrderByDescending(x => x.EmployeeId) : query.OrderBy(x => x.EmployeeId),
            "pitid" => descending ? query.OrderByDescending(x => x.PitId) : query.OrderBy(x => x.PitId),
            "hourmeter" => descending ? query.OrderByDescending(x => x.HourMeter) : query.OrderBy(x => x.HourMeter),
            "odometerkm" => descending ? query.OrderByDescending(x => x.OdometerKm) : query.OrderBy(x => x.OdometerKm),
            "liters" => descending ? query.OrderByDescending(x => x.Liters) : query.OrderBy(x => x.Liters),
            "unitcost" => descending ? query.OrderByDescending(x => x.UnitCost) : query.OrderBy(x => x.UnitCost),
            "hourssincelastfuel" => descending ? query.OrderByDescending(x => x.HoursSinceLastFuel) : query.OrderBy(x => x.HoursSinceLastFuel),
            "fuelburnlph" => descending ? query.OrderByDescending(x => x.FuelBurnLph) : query.OrderBy(x => x.FuelBurnLph),
            "co2kgperl" => descending ? query.OrderByDescending(x => x.Co2KgPerL) : query.OrderBy(x => x.Co2KgPerL),
            "sourcename" => descending ? query.OrderByDescending(x => x.SourceName) : query.OrderBy(x => x.SourceName),
            "notes" => descending ? query.OrderByDescending(x => x.Notes) : query.OrderBy(x => x.Notes),
            "createdat" => descending ? query.OrderByDescending(x => x.CreatedAt) : query.OrderBy(x => x.CreatedAt),
            "costamount" => descending ? query.OrderByDescending(x => x.CostAmount) : query.OrderBy(x => x.CostAmount),
            "co2kg" => descending ? query.OrderByDescending(x => x.Co2Kg) : query.OrderBy(x => x.Co2Kg),
            _ => descending ? query.OrderByDescending(x => x.FuelLogId) : query.OrderBy(x => x.FuelLogId)
        };
    }
    private static FuelLogDto ToDto(FuelLog item) => new(
        item.FuelLogId,
        item.EquipmentId,
        item.FuelTypeId,
        item.FueledAt,
        item.ShiftId,
        item.EmployeeId,
        item.PitId,
        item.HourMeter,
        item.OdometerKm,
        item.Liters,
        item.UnitCost,
        item.HoursSinceLastFuel,
        item.FuelBurnLph,
        item.Co2KgPerL,
        item.SourceName,
        item.Notes,
        item.CreatedAt,
        item.CostAmount,
        item.Co2Kg
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
            Resource = "FuelLog",
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
        _changes.Clients.All.SendAsync(DataChangeHub.DataChangedMethod, new DataChangeNotification("FuelLog", action, resourceKey, DateTimeOffset.UtcNow), ct);

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
