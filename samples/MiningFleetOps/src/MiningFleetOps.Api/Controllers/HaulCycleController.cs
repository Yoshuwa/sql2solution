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
[Route("api/haulCycles")]
public sealed partial class HaulCycleController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<DataChangeHub> _changes;

    public HaulCycleController(AppDbContext db, IHubContext<DataChangeHub> changes)
    {
        _db = db;
        _changes = changes;
    }

    partial void OnBeforeCreate(CreateHaulCycleRequest request, HaulCycle item);
    partial void OnAfterCreate(HaulCycle item);
    partial void OnBeforeUpdate(HaulCycle item, UpdateHaulCycleRequest request);
    partial void OnBeforeDelete(HaulCycle item);

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<HaulCycleDto>>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null, [FromQuery] string? filterField = null, [FromQuery] string? filterValue = null, [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        IQueryable<HaulCycle> query = _db.Set<HaulCycle>().AsNoTracking();
        query = ApplySearch(query, search);
        query = ApplyFilter(query, filterField, filterValue);
        query = ApplySort(query, sortBy, sortDirection);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<HaulCycleDto>>.Success("records loaded", new PagedResult<HaulCycleDto>(items, page, pageSize, total)));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<HaulCycleDto>>> GetById(long id, CancellationToken ct)
    {
        IQueryable<HaulCycle> query = _db.Set<HaulCycle>().AsNoTracking();
        var item = await query.FirstOrDefaultAsync(x => x.HaulCycleId!.Equals(id), ct);
        return item is null ? NotFound(ApiResponse<object>.Warning("record not found")) : Ok(ApiResponse<HaulCycleDto>.Success("record loaded", ToDto(item)));
    }

    [HttpGet("{id}/history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuditTrailDto>>>> GetHistory(long id, CancellationToken ct)
    {
        var canReadRecord = await _db.Set<HaulCycle>().AsNoTracking().AnyAsync(x => x.HaulCycleId!.Equals(id), ct);
        if (!canReadRecord) return NotFound(ApiResponse<object>.Warning("record not found"));
        await EnsureAuditTrailTableAsync(ct);
        var resourceKey = Convert.ToString(id) ?? string.Empty;
        var history = await _db.AuditTrailEntries
            .AsNoTracking()
            .Where(entry => entry.Resource == "HaulCycle" && entry.ResourceKey == resourceKey)
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .Take(100)
            .Select(entry => ToAuditTrailDto(entry))
            .ToListAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<AuditTrailDto>>.Success("activity loaded", history));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<HaulCycleDto>>> Create(CreateHaulCycleRequest request, CancellationToken ct)
    {
        var item = new HaulCycle
        {
            EquipmentId = request.EquipmentId,
            OperatorEmployeeId = request.OperatorEmployeeId,
            ShiftId = request.ShiftId,
            PitId = request.PitId,
            MaterialId = request.MaterialId,
            CycleStartedAt = request.CycleStartedAt,
            CycleEndedAt = request.CycleEndedAt,
            LoadedTonnes = request.LoadedTonnes,
            DistanceKm = request.DistanceKm,
            FuelLitersEstimated = request.FuelLitersEstimated,
            TonnesPerHour = request.TonnesPerHour,
            CycleMinutes = request.CycleMinutes,
            TonnesKm = request.TonnesKm,
        };
        OnBeforeCreate(request, item);
        _db.Set<HaulCycle>().Add(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Created", Convert.ToString(item.HaulCycleId) ?? string.Empty, $"Created HaulCycle record {item.HaulCycleId}.", ToDto(item), ct);
        OnAfterCreate(item);
        await NotifyResourceChangedAsync("Created", Convert.ToString(item.HaulCycleId), ct);
        return CreatedAtAction(nameof(GetById), new { id = item.HaulCycleId }, ApiResponse<HaulCycleDto>.Success("record created", ToDto(item)));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, UpdateHaulCycleRequest request, CancellationToken ct)
    {
        var item = await _db.Set<HaulCycle>().FirstOrDefaultAsync(x => x.HaulCycleId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeUpdate(item, request);
        item.EquipmentId = request.EquipmentId;
        item.OperatorEmployeeId = request.OperatorEmployeeId;
        item.ShiftId = request.ShiftId;
        item.PitId = request.PitId;
        item.MaterialId = request.MaterialId;
        item.CycleStartedAt = request.CycleStartedAt;
        item.CycleEndedAt = request.CycleEndedAt;
        item.LoadedTonnes = request.LoadedTonnes;
        item.DistanceKm = request.DistanceKm;
        item.FuelLitersEstimated = request.FuelLitersEstimated;
        item.TonnesPerHour = request.TonnesPerHour;
        item.CycleMinutes = request.CycleMinutes;
        item.TonnesKm = request.TonnesKm;
        var auditChanges = GetEntityChanges(_db.Entry(item));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Updated", Convert.ToString(item.HaulCycleId) ?? string.Empty, $"Updated HaulCycle record {item.HaulCycleId}.", auditChanges, ct);
        await NotifyResourceChangedAsync("Updated", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record updated", new { updated = 1 }));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch(long id, UpdateHaulCycleRequest request, CancellationToken ct)
    {
        return await Update(id, request, ct);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var item = await _db.Set<HaulCycle>().FirstOrDefaultAsync(x => x.HaulCycleId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeDelete(item);
        _db.Set<HaulCycle>().Remove(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Deleted", Convert.ToString(id) ?? string.Empty, $"Hard deleted HaulCycle record {id}.", ToDto(item), ct);
        await NotifyResourceChangedAsync("Deleted", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record deleted", new { deleted = 1, mode = "Hard" }));
    }

    [HttpPost("bulk/export")]
    public async Task<ActionResult<ApiResponse<PagedResult<HaulCycleDto>>>> ExportBulk(BulkIdsRequest request, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return Ok(ApiResponse<PagedResult<HaulCycleDto>>.Warning("no records selected", new PagedResult<HaulCycleDto>(Array.Empty<HaulCycleDto>(), page, pageSize, 0)));
        IQueryable<HaulCycle> query = _db.Set<HaulCycle>().AsNoTracking().Where(x => ids.Contains(x.HaulCycleId));
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<HaulCycleDto>>.Success("records exported", new PagedResult<HaulCycleDto>(items, page, pageSize, total)));
    }

    [HttpPatch("bulk")]
    public async Task<IActionResult> UpdateBulk(BulkUpdateRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        if (string.IsNullOrWhiteSpace(request.Field)) return BadRequest(ApiResponse<object>.Error("error", new { error = "Choose a field to update." }));
        IQueryable<HaulCycle> query = _db.Set<HaulCycle>().Where(x => ids.Contains(x.HaulCycleId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return NotFound(ApiResponse<object>.Warning("records not found"));
        if (!ApplyBulkUpdate(items, request, out var error)) return BadRequest(ApiResponse<object>.Error("error", new { error }));
        var auditChanges = items.ToDictionary(item => Convert.ToString(item.HaulCycleId) ?? string.Empty, item => GetEntityChanges(_db.Entry(item)));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Updated", Convert.ToString(item.HaulCycleId) ?? string.Empty, $"Updated HaulCycle record {item.HaulCycleId} in bulk update.", auditChanges[Convert.ToString(item.HaulCycleId) ?? string.Empty], ct);
        await NotifyResourceChangedAsync("Updated", null, ct);
        return Ok(ApiResponse<object>.Success("records updated", new { updated = items.Count }));
    }

    [HttpPost("bulk/delete")]
    public async Task<IActionResult> DeleteBulk(BulkIdsRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        IQueryable<HaulCycle> query = _db.Set<HaulCycle>().Where(x => ids.Contains(x.HaulCycleId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return Ok(ApiResponse<object>.Warning("records not found", new { deleted = 0 }));
        foreach (var item in items)
        {
            OnBeforeDelete(item);
        }
        _db.Set<HaulCycle>().RemoveRange(items);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Deleted", Convert.ToString(item.HaulCycleId) ?? string.Empty, $"Hard deleted HaulCycle record {item.HaulCycleId} in bulk delete.", ToDto(item), ct);
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

    private static bool ApplyBulkUpdate(IReadOnlyList<HaulCycle> items, BulkUpdateRequest request, out string error)
    {
        error = string.Empty;
        return request.Field.Trim().ToLowerInvariant() switch
        {
            "equipmentid" => ApplyBulkEquipmentId(items, request.Value, out error),
            "operatoremployeeid" => ApplyBulkOperatorEmployeeId(items, request.Value, out error),
            "shiftid" => ApplyBulkShiftId(items, request.Value, out error),
            "pitid" => ApplyBulkPitId(items, request.Value, out error),
            "materialid" => ApplyBulkMaterialId(items, request.Value, out error),
            "cyclestartedat" => ApplyBulkCycleStartedAt(items, request.Value, out error),
            "cycleendedat" => ApplyBulkCycleEndedAt(items, request.Value, out error),
            "loadedtonnes" => ApplyBulkLoadedTonnes(items, request.Value, out error),
            "distancekm" => ApplyBulkDistanceKm(items, request.Value, out error),
            "fuellitersestimated" => ApplyBulkFuelLitersEstimated(items, request.Value, out error),
            "tonnesperhour" => ApplyBulkTonnesPerHour(items, request.Value, out error),
            "cycleminutes" => ApplyBulkCycleMinutes(items, request.Value, out error),
            "tonneskm" => ApplyBulkTonnesKm(items, request.Value, out error),
            _ => FailBulkUpdate("Field is not bulk editable.", out error)
        };
    }

    private static bool ApplyBulkEquipmentId(IReadOnlyList<HaulCycle> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("EquipmentId requires a int value.", out error);
        foreach (var item in items) item.EquipmentId = value;
        return true;
    }

    private static bool ApplyBulkOperatorEmployeeId(IReadOnlyList<HaulCycle> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.OperatorEmployeeId = null;
            return true;
        }
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("OperatorEmployeeId requires a int value.", out error);
        foreach (var item in items) item.OperatorEmployeeId = value;
        return true;
    }

    private static bool ApplyBulkShiftId(IReadOnlyList<HaulCycle> items, string? raw, out string error)
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

    private static bool ApplyBulkPitId(IReadOnlyList<HaulCycle> items, string? raw, out string error)
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

    private static bool ApplyBulkMaterialId(IReadOnlyList<HaulCycle> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("MaterialId requires a int value.", out error);
        foreach (var item in items) item.MaterialId = value;
        return true;
    }

    private static bool ApplyBulkCycleStartedAt(IReadOnlyList<HaulCycle> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!DateTime.TryParse(raw, out var value)) return FailBulkUpdate("CycleStartedAt requires a DateTime value.", out error);
        foreach (var item in items) item.CycleStartedAt = value;
        return true;
    }

    private static bool ApplyBulkCycleEndedAt(IReadOnlyList<HaulCycle> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!DateTime.TryParse(raw, out var value)) return FailBulkUpdate("CycleEndedAt requires a DateTime value.", out error);
        foreach (var item in items) item.CycleEndedAt = value;
        return true;
    }

    private static bool ApplyBulkLoadedTonnes(IReadOnlyList<HaulCycle> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("LoadedTonnes requires a decimal value.", out error);
        foreach (var item in items) item.LoadedTonnes = value;
        return true;
    }

    private static bool ApplyBulkDistanceKm(IReadOnlyList<HaulCycle> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.DistanceKm = null;
            return true;
        }
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("DistanceKm requires a decimal value.", out error);
        foreach (var item in items) item.DistanceKm = value;
        return true;
    }

    private static bool ApplyBulkFuelLitersEstimated(IReadOnlyList<HaulCycle> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.FuelLitersEstimated = null;
            return true;
        }
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("FuelLitersEstimated requires a decimal value.", out error);
        foreach (var item in items) item.FuelLitersEstimated = value;
        return true;
    }

    private static bool ApplyBulkTonnesPerHour(IReadOnlyList<HaulCycle> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.TonnesPerHour = null;
            return true;
        }
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("TonnesPerHour requires a decimal value.", out error);
        foreach (var item in items) item.TonnesPerHour = value;
        return true;
    }

    private static bool ApplyBulkCycleMinutes(IReadOnlyList<HaulCycle> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.CycleMinutes = null;
            return true;
        }
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("CycleMinutes requires a decimal value.", out error);
        foreach (var item in items) item.CycleMinutes = value;
        return true;
    }

    private static bool ApplyBulkTonnesKm(IReadOnlyList<HaulCycle> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.TonnesKm = null;
            return true;
        }
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("TonnesKm requires a decimal value.", out error);
        foreach (var item in items) item.TonnesKm = value;
        return true;
    }

    private static bool FailBulkUpdate(string message, out string error)
    {
        error = message;
        return false;
    }


    private static IQueryable<HaulCycle> ApplySearch(IQueryable<HaulCycle> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        search = search.Trim();
        return query;
    }

    private static IQueryable<HaulCycle> ApplyFilter(IQueryable<HaulCycle> query, string? filterField, string? filterValue)
    {
        if (string.IsNullOrWhiteSpace(filterField) || string.IsNullOrWhiteSpace(filterValue)) return query;
        filterField = filterField.Trim();
        filterValue = filterValue.Trim();
        return filterField.ToLowerInvariant() switch
        {
            "haulcycleid" => long.TryParse(filterValue, out var HaulCycleIdValue) ? query.Where(x => x.HaulCycleId == HaulCycleIdValue) : query,
            "equipmentid" => int.TryParse(filterValue, out var EquipmentIdValue) ? query.Where(x => x.EquipmentId == EquipmentIdValue) : query,
            "operatoremployeeid" => int.TryParse(filterValue, out var OperatorEmployeeIdValue) ? query.Where(x => x.OperatorEmployeeId == OperatorEmployeeIdValue) : query,
            "shiftid" => int.TryParse(filterValue, out var ShiftIdValue) ? query.Where(x => x.ShiftId == ShiftIdValue) : query,
            "pitid" => int.TryParse(filterValue, out var PitIdValue) ? query.Where(x => x.PitId == PitIdValue) : query,
            "materialid" => int.TryParse(filterValue, out var MaterialIdValue) ? query.Where(x => x.MaterialId == MaterialIdValue) : query,
            "cyclestartedat" => DateTime.TryParse(filterValue, out var CycleStartedAtValue) ? query.Where(x => x.CycleStartedAt == CycleStartedAtValue) : query,
            "cycleendedat" => DateTime.TryParse(filterValue, out var CycleEndedAtValue) ? query.Where(x => x.CycleEndedAt == CycleEndedAtValue) : query,
            "loadedtonnes" => decimal.TryParse(filterValue, out var LoadedTonnesValue) ? query.Where(x => x.LoadedTonnes == LoadedTonnesValue) : query,
            "distancekm" => decimal.TryParse(filterValue, out var DistanceKmValue) ? query.Where(x => x.DistanceKm == DistanceKmValue) : query,
            "fuellitersestimated" => decimal.TryParse(filterValue, out var FuelLitersEstimatedValue) ? query.Where(x => x.FuelLitersEstimated == FuelLitersEstimatedValue) : query,
            "tonnesperhour" => decimal.TryParse(filterValue, out var TonnesPerHourValue) ? query.Where(x => x.TonnesPerHour == TonnesPerHourValue) : query,
            "cycleminutes" => decimal.TryParse(filterValue, out var CycleMinutesValue) ? query.Where(x => x.CycleMinutes == CycleMinutesValue) : query,
            "tonneskm" => decimal.TryParse(filterValue, out var TonnesKmValue) ? query.Where(x => x.TonnesKm == TonnesKmValue) : query,
            _ => query
        };
    }

    private static IQueryable<HaulCycle> ApplySort(IQueryable<HaulCycle> query, string? sortBy, string? sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) || string.Equals(sortDirection, "descending", StringComparison.OrdinalIgnoreCase);
        var field = string.IsNullOrWhiteSpace(sortBy) ? "HaulCycleId" : sortBy.Trim();
        return field.ToLowerInvariant() switch
        {
            "haulcycleid" => descending ? query.OrderByDescending(x => x.HaulCycleId) : query.OrderBy(x => x.HaulCycleId),
            "equipmentid" => descending ? query.OrderByDescending(x => x.EquipmentId) : query.OrderBy(x => x.EquipmentId),
            "operatoremployeeid" => descending ? query.OrderByDescending(x => x.OperatorEmployeeId) : query.OrderBy(x => x.OperatorEmployeeId),
            "shiftid" => descending ? query.OrderByDescending(x => x.ShiftId) : query.OrderBy(x => x.ShiftId),
            "pitid" => descending ? query.OrderByDescending(x => x.PitId) : query.OrderBy(x => x.PitId),
            "materialid" => descending ? query.OrderByDescending(x => x.MaterialId) : query.OrderBy(x => x.MaterialId),
            "cyclestartedat" => descending ? query.OrderByDescending(x => x.CycleStartedAt) : query.OrderBy(x => x.CycleStartedAt),
            "cycleendedat" => descending ? query.OrderByDescending(x => x.CycleEndedAt) : query.OrderBy(x => x.CycleEndedAt),
            "loadedtonnes" => descending ? query.OrderByDescending(x => x.LoadedTonnes) : query.OrderBy(x => x.LoadedTonnes),
            "distancekm" => descending ? query.OrderByDescending(x => x.DistanceKm) : query.OrderBy(x => x.DistanceKm),
            "fuellitersestimated" => descending ? query.OrderByDescending(x => x.FuelLitersEstimated) : query.OrderBy(x => x.FuelLitersEstimated),
            "tonnesperhour" => descending ? query.OrderByDescending(x => x.TonnesPerHour) : query.OrderBy(x => x.TonnesPerHour),
            "cycleminutes" => descending ? query.OrderByDescending(x => x.CycleMinutes) : query.OrderBy(x => x.CycleMinutes),
            "tonneskm" => descending ? query.OrderByDescending(x => x.TonnesKm) : query.OrderBy(x => x.TonnesKm),
            _ => descending ? query.OrderByDescending(x => x.HaulCycleId) : query.OrderBy(x => x.HaulCycleId)
        };
    }
    private static HaulCycleDto ToDto(HaulCycle item) => new(
        item.HaulCycleId,
        item.EquipmentId,
        item.OperatorEmployeeId,
        item.ShiftId,
        item.PitId,
        item.MaterialId,
        item.CycleStartedAt,
        item.CycleEndedAt,
        item.LoadedTonnes,
        item.DistanceKm,
        item.FuelLitersEstimated,
        item.TonnesPerHour,
        item.CycleMinutes,
        item.TonnesKm
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
            Resource = "HaulCycle",
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
        _changes.Clients.All.SendAsync(DataChangeHub.DataChangedMethod, new DataChangeNotification("HaulCycle", action, resourceKey, DateTimeOffset.UtcNow), ct);

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
