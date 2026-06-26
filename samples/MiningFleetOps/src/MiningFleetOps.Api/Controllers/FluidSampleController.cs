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
[Route("api/fluidSamples")]
public sealed partial class FluidSampleController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<DataChangeHub> _changes;

    public FluidSampleController(AppDbContext db, IHubContext<DataChangeHub> changes)
    {
        _db = db;
        _changes = changes;
    }

    partial void OnBeforeCreate(CreateFluidSampleRequest request, FluidSample item);
    partial void OnAfterCreate(FluidSample item);
    partial void OnBeforeUpdate(FluidSample item, UpdateFluidSampleRequest request);
    partial void OnBeforeDelete(FluidSample item);

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<FluidSampleDto>>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null, [FromQuery] string? filterField = null, [FromQuery] string? filterValue = null, [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        IQueryable<FluidSample> query = _db.Set<FluidSample>().AsNoTracking();
        query = ApplySearch(query, search);
        query = ApplyFilter(query, filterField, filterValue);
        query = ApplySort(query, sortBy, sortDirection);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<FluidSampleDto>>.Success("records loaded", new PagedResult<FluidSampleDto>(items, page, pageSize, total)));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<FluidSampleDto>>> GetById(long id, CancellationToken ct)
    {
        IQueryable<FluidSample> query = _db.Set<FluidSample>().AsNoTracking();
        var item = await query.FirstOrDefaultAsync(x => x.FluidSampleId!.Equals(id), ct);
        return item is null ? NotFound(ApiResponse<object>.Warning("record not found")) : Ok(ApiResponse<FluidSampleDto>.Success("record loaded", ToDto(item)));
    }

    [HttpGet("{id}/history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuditTrailDto>>>> GetHistory(long id, CancellationToken ct)
    {
        var canReadRecord = await _db.Set<FluidSample>().AsNoTracking().AnyAsync(x => x.FluidSampleId!.Equals(id), ct);
        if (!canReadRecord) return NotFound(ApiResponse<object>.Warning("record not found"));
        await EnsureAuditTrailTableAsync(ct);
        var resourceKey = Convert.ToString(id) ?? string.Empty;
        var history = await _db.AuditTrailEntries
            .AsNoTracking()
            .Where(entry => entry.Resource == "FluidSample" && entry.ResourceKey == resourceKey)
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .Take(100)
            .Select(entry => ToAuditTrailDto(entry))
            .ToListAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<AuditTrailDto>>.Success("activity loaded", history));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<FluidSampleDto>>> Create(CreateFluidSampleRequest request, CancellationToken ct)
    {
        var item = new FluidSample
        {
            EquipmentId = request.EquipmentId,
            FluidTypeId = request.FluidTypeId,
            SampledAt = request.SampledAt,
            HourMeter = request.HourMeter,
            LabReference = request.LabReference,
            IronPpm = request.IronPpm,
            CopperPpm = request.CopperPpm,
            SiliconPpm = request.SiliconPpm,
            ViscosityCst = request.ViscosityCst,
            WaterPercent = request.WaterPercent,
            Severity = request.Severity,
            Recommendation = request.Recommendation,
        };
        OnBeforeCreate(request, item);
        _db.Set<FluidSample>().Add(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Created", Convert.ToString(item.FluidSampleId) ?? string.Empty, $"Created FluidSample record {item.FluidSampleId}.", ToDto(item), ct);
        OnAfterCreate(item);
        await NotifyResourceChangedAsync("Created", Convert.ToString(item.FluidSampleId), ct);
        return CreatedAtAction(nameof(GetById), new { id = item.FluidSampleId }, ApiResponse<FluidSampleDto>.Success("record created", ToDto(item)));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, UpdateFluidSampleRequest request, CancellationToken ct)
    {
        var item = await _db.Set<FluidSample>().FirstOrDefaultAsync(x => x.FluidSampleId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeUpdate(item, request);
        item.EquipmentId = request.EquipmentId;
        item.FluidTypeId = request.FluidTypeId;
        item.SampledAt = request.SampledAt;
        item.HourMeter = request.HourMeter;
        item.LabReference = request.LabReference;
        item.IronPpm = request.IronPpm;
        item.CopperPpm = request.CopperPpm;
        item.SiliconPpm = request.SiliconPpm;
        item.ViscosityCst = request.ViscosityCst;
        item.WaterPercent = request.WaterPercent;
        item.Severity = request.Severity;
        item.Recommendation = request.Recommendation;
        var auditChanges = GetEntityChanges(_db.Entry(item));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Updated", Convert.ToString(item.FluidSampleId) ?? string.Empty, $"Updated FluidSample record {item.FluidSampleId}.", auditChanges, ct);
        await NotifyResourceChangedAsync("Updated", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record updated", new { updated = 1 }));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch(long id, UpdateFluidSampleRequest request, CancellationToken ct)
    {
        return await Update(id, request, ct);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var item = await _db.Set<FluidSample>().FirstOrDefaultAsync(x => x.FluidSampleId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeDelete(item);
        _db.Set<FluidSample>().Remove(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Deleted", Convert.ToString(id) ?? string.Empty, $"Hard deleted FluidSample record {id}.", ToDto(item), ct);
        await NotifyResourceChangedAsync("Deleted", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record deleted", new { deleted = 1, mode = "Hard" }));
    }

    [HttpPost("bulk/export")]
    public async Task<ActionResult<ApiResponse<PagedResult<FluidSampleDto>>>> ExportBulk(BulkIdsRequest request, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return Ok(ApiResponse<PagedResult<FluidSampleDto>>.Warning("no records selected", new PagedResult<FluidSampleDto>(Array.Empty<FluidSampleDto>(), page, pageSize, 0)));
        IQueryable<FluidSample> query = _db.Set<FluidSample>().AsNoTracking().Where(x => ids.Contains(x.FluidSampleId));
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<FluidSampleDto>>.Success("records exported", new PagedResult<FluidSampleDto>(items, page, pageSize, total)));
    }

    [HttpPatch("bulk")]
    public async Task<IActionResult> UpdateBulk(BulkUpdateRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        if (string.IsNullOrWhiteSpace(request.Field)) return BadRequest(ApiResponse<object>.Error("error", new { error = "Choose a field to update." }));
        IQueryable<FluidSample> query = _db.Set<FluidSample>().Where(x => ids.Contains(x.FluidSampleId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return NotFound(ApiResponse<object>.Warning("records not found"));
        if (!ApplyBulkUpdate(items, request, out var error)) return BadRequest(ApiResponse<object>.Error("error", new { error }));
        var auditChanges = items.ToDictionary(item => Convert.ToString(item.FluidSampleId) ?? string.Empty, item => GetEntityChanges(_db.Entry(item)));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Updated", Convert.ToString(item.FluidSampleId) ?? string.Empty, $"Updated FluidSample record {item.FluidSampleId} in bulk update.", auditChanges[Convert.ToString(item.FluidSampleId) ?? string.Empty], ct);
        await NotifyResourceChangedAsync("Updated", null, ct);
        return Ok(ApiResponse<object>.Success("records updated", new { updated = items.Count }));
    }

    [HttpPost("bulk/delete")]
    public async Task<IActionResult> DeleteBulk(BulkIdsRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        IQueryable<FluidSample> query = _db.Set<FluidSample>().Where(x => ids.Contains(x.FluidSampleId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return Ok(ApiResponse<object>.Warning("records not found", new { deleted = 0 }));
        foreach (var item in items)
        {
            OnBeforeDelete(item);
        }
        _db.Set<FluidSample>().RemoveRange(items);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Deleted", Convert.ToString(item.FluidSampleId) ?? string.Empty, $"Hard deleted FluidSample record {item.FluidSampleId} in bulk delete.", ToDto(item), ct);
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

    private static bool ApplyBulkUpdate(IReadOnlyList<FluidSample> items, BulkUpdateRequest request, out string error)
    {
        error = string.Empty;
        return request.Field.Trim().ToLowerInvariant() switch
        {
            "equipmentid" => ApplyBulkEquipmentId(items, request.Value, out error),
            "fluidtypeid" => ApplyBulkFluidTypeId(items, request.Value, out error),
            "sampledat" => ApplyBulkSampledAt(items, request.Value, out error),
            "hourmeter" => ApplyBulkHourMeter(items, request.Value, out error),
            "labreference" => ApplyBulkLabReference(items, request.Value, out error),
            "ironppm" => ApplyBulkIronPpm(items, request.Value, out error),
            "copperppm" => ApplyBulkCopperPpm(items, request.Value, out error),
            "siliconppm" => ApplyBulkSiliconPpm(items, request.Value, out error),
            "viscositycst" => ApplyBulkViscosityCst(items, request.Value, out error),
            "waterpercent" => ApplyBulkWaterPercent(items, request.Value, out error),
            "severity" => ApplyBulkSeverity(items, request.Value, out error),
            "recommendation" => ApplyBulkRecommendation(items, request.Value, out error),
            _ => FailBulkUpdate("Field is not bulk editable.", out error)
        };
    }

    private static bool ApplyBulkEquipmentId(IReadOnlyList<FluidSample> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("EquipmentId requires a int value.", out error);
        foreach (var item in items) item.EquipmentId = value;
        return true;
    }

    private static bool ApplyBulkFluidTypeId(IReadOnlyList<FluidSample> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("FluidTypeId requires a int value.", out error);
        foreach (var item in items) item.FluidTypeId = value;
        return true;
    }

    private static bool ApplyBulkSampledAt(IReadOnlyList<FluidSample> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!DateTime.TryParse(raw, out var value)) return FailBulkUpdate("SampledAt requires a DateTime value.", out error);
        foreach (var item in items) item.SampledAt = value;
        return true;
    }

    private static bool ApplyBulkHourMeter(IReadOnlyList<FluidSample> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("HourMeter requires a decimal value.", out error);
        foreach (var item in items) item.HourMeter = value;
        return true;
    }

    private static bool ApplyBulkLabReference(IReadOnlyList<FluidSample> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.LabReference = null;
            return true;
        }
        foreach (var item in items) item.LabReference = raw;
        return true;
    }

    private static bool ApplyBulkIronPpm(IReadOnlyList<FluidSample> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.IronPpm = null;
            return true;
        }
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("IronPpm requires a decimal value.", out error);
        foreach (var item in items) item.IronPpm = value;
        return true;
    }

    private static bool ApplyBulkCopperPpm(IReadOnlyList<FluidSample> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.CopperPpm = null;
            return true;
        }
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("CopperPpm requires a decimal value.", out error);
        foreach (var item in items) item.CopperPpm = value;
        return true;
    }

    private static bool ApplyBulkSiliconPpm(IReadOnlyList<FluidSample> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.SiliconPpm = null;
            return true;
        }
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("SiliconPpm requires a decimal value.", out error);
        foreach (var item in items) item.SiliconPpm = value;
        return true;
    }

    private static bool ApplyBulkViscosityCst(IReadOnlyList<FluidSample> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.ViscosityCst = null;
            return true;
        }
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("ViscosityCst requires a decimal value.", out error);
        foreach (var item in items) item.ViscosityCst = value;
        return true;
    }

    private static bool ApplyBulkWaterPercent(IReadOnlyList<FluidSample> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.WaterPercent = null;
            return true;
        }
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("WaterPercent requires a decimal value.", out error);
        foreach (var item in items) item.WaterPercent = value;
        return true;
    }

    private static bool ApplyBulkSeverity(IReadOnlyList<FluidSample> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.Severity = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkRecommendation(IReadOnlyList<FluidSample> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.Recommendation = null;
            return true;
        }
        foreach (var item in items) item.Recommendation = raw;
        return true;
    }

    private static bool FailBulkUpdate(string message, out string error)
    {
        error = message;
        return false;
    }


    private static IQueryable<FluidSample> ApplySearch(IQueryable<FluidSample> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        search = search.Trim();
        return query.Where(x => (x.LabReference != null && x.LabReference.Contains(search)) || (x.Severity != null && x.Severity.Contains(search)) || (x.Recommendation != null && x.Recommendation.Contains(search)));
    }

    private static IQueryable<FluidSample> ApplyFilter(IQueryable<FluidSample> query, string? filterField, string? filterValue)
    {
        if (string.IsNullOrWhiteSpace(filterField) || string.IsNullOrWhiteSpace(filterValue)) return query;
        filterField = filterField.Trim();
        filterValue = filterValue.Trim();
        return filterField.ToLowerInvariant() switch
        {
            "fluidsampleid" => long.TryParse(filterValue, out var FluidSampleIdValue) ? query.Where(x => x.FluidSampleId == FluidSampleIdValue) : query,
            "equipmentid" => int.TryParse(filterValue, out var EquipmentIdValue) ? query.Where(x => x.EquipmentId == EquipmentIdValue) : query,
            "fluidtypeid" => int.TryParse(filterValue, out var FluidTypeIdValue) ? query.Where(x => x.FluidTypeId == FluidTypeIdValue) : query,
            "sampledat" => DateTime.TryParse(filterValue, out var SampledAtValue) ? query.Where(x => x.SampledAt == SampledAtValue) : query,
            "hourmeter" => decimal.TryParse(filterValue, out var HourMeterValue) ? query.Where(x => x.HourMeter == HourMeterValue) : query,
            "labreference" => query.Where(x => x.LabReference != null && x.LabReference.Contains(filterValue)),
            "ironppm" => decimal.TryParse(filterValue, out var IronPpmValue) ? query.Where(x => x.IronPpm == IronPpmValue) : query,
            "copperppm" => decimal.TryParse(filterValue, out var CopperPpmValue) ? query.Where(x => x.CopperPpm == CopperPpmValue) : query,
            "siliconppm" => decimal.TryParse(filterValue, out var SiliconPpmValue) ? query.Where(x => x.SiliconPpm == SiliconPpmValue) : query,
            "viscositycst" => decimal.TryParse(filterValue, out var ViscosityCstValue) ? query.Where(x => x.ViscosityCst == ViscosityCstValue) : query,
            "waterpercent" => decimal.TryParse(filterValue, out var WaterPercentValue) ? query.Where(x => x.WaterPercent == WaterPercentValue) : query,
            "severity" => query.Where(x => x.Severity != null && x.Severity.Contains(filterValue)),
            "recommendation" => query.Where(x => x.Recommendation != null && x.Recommendation.Contains(filterValue)),
            _ => query
        };
    }

    private static IQueryable<FluidSample> ApplySort(IQueryable<FluidSample> query, string? sortBy, string? sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) || string.Equals(sortDirection, "descending", StringComparison.OrdinalIgnoreCase);
        var field = string.IsNullOrWhiteSpace(sortBy) ? "FluidSampleId" : sortBy.Trim();
        return field.ToLowerInvariant() switch
        {
            "fluidsampleid" => descending ? query.OrderByDescending(x => x.FluidSampleId) : query.OrderBy(x => x.FluidSampleId),
            "equipmentid" => descending ? query.OrderByDescending(x => x.EquipmentId) : query.OrderBy(x => x.EquipmentId),
            "fluidtypeid" => descending ? query.OrderByDescending(x => x.FluidTypeId) : query.OrderBy(x => x.FluidTypeId),
            "sampledat" => descending ? query.OrderByDescending(x => x.SampledAt) : query.OrderBy(x => x.SampledAt),
            "hourmeter" => descending ? query.OrderByDescending(x => x.HourMeter) : query.OrderBy(x => x.HourMeter),
            "labreference" => descending ? query.OrderByDescending(x => x.LabReference) : query.OrderBy(x => x.LabReference),
            "ironppm" => descending ? query.OrderByDescending(x => x.IronPpm) : query.OrderBy(x => x.IronPpm),
            "copperppm" => descending ? query.OrderByDescending(x => x.CopperPpm) : query.OrderBy(x => x.CopperPpm),
            "siliconppm" => descending ? query.OrderByDescending(x => x.SiliconPpm) : query.OrderBy(x => x.SiliconPpm),
            "viscositycst" => descending ? query.OrderByDescending(x => x.ViscosityCst) : query.OrderBy(x => x.ViscosityCst),
            "waterpercent" => descending ? query.OrderByDescending(x => x.WaterPercent) : query.OrderBy(x => x.WaterPercent),
            "severity" => descending ? query.OrderByDescending(x => x.Severity) : query.OrderBy(x => x.Severity),
            "recommendation" => descending ? query.OrderByDescending(x => x.Recommendation) : query.OrderBy(x => x.Recommendation),
            _ => descending ? query.OrderByDescending(x => x.FluidSampleId) : query.OrderBy(x => x.FluidSampleId)
        };
    }
    private static FluidSampleDto ToDto(FluidSample item) => new(
        item.FluidSampleId,
        item.EquipmentId,
        item.FluidTypeId,
        item.SampledAt,
        item.HourMeter,
        item.LabReference,
        item.IronPpm,
        item.CopperPpm,
        item.SiliconPpm,
        item.ViscosityCst,
        item.WaterPercent,
        item.Severity,
        item.Recommendation
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
            Resource = "FluidSample",
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
        _changes.Clients.All.SendAsync(DataChangeHub.DataChangedMethod, new DataChangeNotification("FluidSample", action, resourceKey, DateTimeOffset.UtcNow), ct);

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
