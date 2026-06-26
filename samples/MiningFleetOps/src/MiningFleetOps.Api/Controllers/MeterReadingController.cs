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
[Route("api/meterReadings")]
public sealed partial class MeterReadingController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<DataChangeHub> _changes;

    public MeterReadingController(AppDbContext db, IHubContext<DataChangeHub> changes)
    {
        _db = db;
        _changes = changes;
    }

    partial void OnBeforeCreate(CreateMeterReadingRequest request, MeterReading item);
    partial void OnAfterCreate(MeterReading item);
    partial void OnBeforeUpdate(MeterReading item, UpdateMeterReadingRequest request);
    partial void OnBeforeDelete(MeterReading item);

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<MeterReadingDto>>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null, [FromQuery] string? filterField = null, [FromQuery] string? filterValue = null, [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        IQueryable<MeterReading> query = _db.Set<MeterReading>().AsNoTracking();
        query = ApplySearch(query, search);
        query = ApplyFilter(query, filterField, filterValue);
        query = ApplySort(query, sortBy, sortDirection);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<MeterReadingDto>>.Success("records loaded", new PagedResult<MeterReadingDto>(items, page, pageSize, total)));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<MeterReadingDto>>> GetById(long id, CancellationToken ct)
    {
        IQueryable<MeterReading> query = _db.Set<MeterReading>().AsNoTracking();
        var item = await query.FirstOrDefaultAsync(x => x.MeterReadingId!.Equals(id), ct);
        return item is null ? NotFound(ApiResponse<object>.Warning("record not found")) : Ok(ApiResponse<MeterReadingDto>.Success("record loaded", ToDto(item)));
    }

    [HttpGet("{id}/history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuditTrailDto>>>> GetHistory(long id, CancellationToken ct)
    {
        var canReadRecord = await _db.Set<MeterReading>().AsNoTracking().AnyAsync(x => x.MeterReadingId!.Equals(id), ct);
        if (!canReadRecord) return NotFound(ApiResponse<object>.Warning("record not found"));
        await EnsureAuditTrailTableAsync(ct);
        var resourceKey = Convert.ToString(id) ?? string.Empty;
        var history = await _db.AuditTrailEntries
            .AsNoTracking()
            .Where(entry => entry.Resource == "MeterReading" && entry.ResourceKey == resourceKey)
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .Take(100)
            .Select(entry => ToAuditTrailDto(entry))
            .ToListAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<AuditTrailDto>>.Success("activity loaded", history));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<MeterReadingDto>>> Create(CreateMeterReadingRequest request, CancellationToken ct)
    {
        var item = new MeterReading
        {
            EquipmentId = request.EquipmentId,
            ReadingAt = request.ReadingAt,
            HourMeter = request.HourMeter,
            OdometerKm = request.OdometerKm,
            SourceName = request.SourceName,
            RecordedByEmployeeId = request.RecordedByEmployeeId,
            Notes = request.Notes,
            CreatedAt = request.CreatedAt,
        };
        OnBeforeCreate(request, item);
        _db.Set<MeterReading>().Add(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Created", Convert.ToString(item.MeterReadingId) ?? string.Empty, $"Created MeterReading record {item.MeterReadingId}.", ToDto(item), ct);
        OnAfterCreate(item);
        await NotifyResourceChangedAsync("Created", Convert.ToString(item.MeterReadingId), ct);
        return CreatedAtAction(nameof(GetById), new { id = item.MeterReadingId }, ApiResponse<MeterReadingDto>.Success("record created", ToDto(item)));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, UpdateMeterReadingRequest request, CancellationToken ct)
    {
        var item = await _db.Set<MeterReading>().FirstOrDefaultAsync(x => x.MeterReadingId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeUpdate(item, request);
        item.EquipmentId = request.EquipmentId;
        item.ReadingAt = request.ReadingAt;
        item.HourMeter = request.HourMeter;
        item.OdometerKm = request.OdometerKm;
        item.SourceName = request.SourceName;
        item.RecordedByEmployeeId = request.RecordedByEmployeeId;
        item.Notes = request.Notes;
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
        await LogAuditTrailAsync("Updated", Convert.ToString(item.MeterReadingId) ?? string.Empty, $"Updated MeterReading record {item.MeterReadingId}.", auditChanges, ct);
        await NotifyResourceChangedAsync("Updated", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record updated", new { updated = 1 }));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch(long id, UpdateMeterReadingRequest request, CancellationToken ct)
    {
        return await Update(id, request, ct);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var item = await _db.Set<MeterReading>().FirstOrDefaultAsync(x => x.MeterReadingId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeDelete(item);
        _db.Set<MeterReading>().Remove(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Deleted", Convert.ToString(id) ?? string.Empty, $"Hard deleted MeterReading record {id}.", ToDto(item), ct);
        await NotifyResourceChangedAsync("Deleted", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record deleted", new { deleted = 1, mode = "Hard" }));
    }

    [HttpPost("bulk/export")]
    public async Task<ActionResult<ApiResponse<PagedResult<MeterReadingDto>>>> ExportBulk(BulkIdsRequest request, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return Ok(ApiResponse<PagedResult<MeterReadingDto>>.Warning("no records selected", new PagedResult<MeterReadingDto>(Array.Empty<MeterReadingDto>(), page, pageSize, 0)));
        IQueryable<MeterReading> query = _db.Set<MeterReading>().AsNoTracking().Where(x => ids.Contains(x.MeterReadingId));
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<MeterReadingDto>>.Success("records exported", new PagedResult<MeterReadingDto>(items, page, pageSize, total)));
    }

    [HttpPatch("bulk")]
    public async Task<IActionResult> UpdateBulk(BulkUpdateRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        if (string.IsNullOrWhiteSpace(request.Field)) return BadRequest(ApiResponse<object>.Error("error", new { error = "Choose a field to update." }));
        IQueryable<MeterReading> query = _db.Set<MeterReading>().Where(x => ids.Contains(x.MeterReadingId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return NotFound(ApiResponse<object>.Warning("records not found"));
        if (!ApplyBulkUpdate(items, request, out var error)) return BadRequest(ApiResponse<object>.Error("error", new { error }));
        var auditChanges = items.ToDictionary(item => Convert.ToString(item.MeterReadingId) ?? string.Empty, item => GetEntityChanges(_db.Entry(item)));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Updated", Convert.ToString(item.MeterReadingId) ?? string.Empty, $"Updated MeterReading record {item.MeterReadingId} in bulk update.", auditChanges[Convert.ToString(item.MeterReadingId) ?? string.Empty], ct);
        await NotifyResourceChangedAsync("Updated", null, ct);
        return Ok(ApiResponse<object>.Success("records updated", new { updated = items.Count }));
    }

    [HttpPost("bulk/delete")]
    public async Task<IActionResult> DeleteBulk(BulkIdsRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        IQueryable<MeterReading> query = _db.Set<MeterReading>().Where(x => ids.Contains(x.MeterReadingId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return Ok(ApiResponse<object>.Warning("records not found", new { deleted = 0 }));
        foreach (var item in items)
        {
            OnBeforeDelete(item);
        }
        _db.Set<MeterReading>().RemoveRange(items);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Deleted", Convert.ToString(item.MeterReadingId) ?? string.Empty, $"Hard deleted MeterReading record {item.MeterReadingId} in bulk delete.", ToDto(item), ct);
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

    private static bool ApplyBulkUpdate(IReadOnlyList<MeterReading> items, BulkUpdateRequest request, out string error)
    {
        error = string.Empty;
        return request.Field.Trim().ToLowerInvariant() switch
        {
            "equipmentid" => ApplyBulkEquipmentId(items, request.Value, out error),
            "readingat" => ApplyBulkReadingAt(items, request.Value, out error),
            "hourmeter" => ApplyBulkHourMeter(items, request.Value, out error),
            "odometerkm" => ApplyBulkOdometerKm(items, request.Value, out error),
            "sourcename" => ApplyBulkSourceName(items, request.Value, out error),
            "recordedbyemployeeid" => ApplyBulkRecordedByEmployeeId(items, request.Value, out error),
            "notes" => ApplyBulkNotes(items, request.Value, out error),
            "createdat" => ApplyBulkCreatedAt(items, request.Value, out error),
            _ => FailBulkUpdate("Field is not bulk editable.", out error)
        };
    }

    private static bool ApplyBulkEquipmentId(IReadOnlyList<MeterReading> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("EquipmentId requires a int value.", out error);
        foreach (var item in items) item.EquipmentId = value;
        return true;
    }

    private static bool ApplyBulkReadingAt(IReadOnlyList<MeterReading> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!DateTime.TryParse(raw, out var value)) return FailBulkUpdate("ReadingAt requires a DateTime value.", out error);
        foreach (var item in items) item.ReadingAt = value;
        return true;
    }

    private static bool ApplyBulkHourMeter(IReadOnlyList<MeterReading> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("HourMeter requires a decimal value.", out error);
        foreach (var item in items) item.HourMeter = value;
        return true;
    }

    private static bool ApplyBulkOdometerKm(IReadOnlyList<MeterReading> items, string? raw, out string error)
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

    private static bool ApplyBulkSourceName(IReadOnlyList<MeterReading> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.SourceName = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkRecordedByEmployeeId(IReadOnlyList<MeterReading> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.RecordedByEmployeeId = null;
            return true;
        }
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("RecordedByEmployeeId requires a int value.", out error);
        foreach (var item in items) item.RecordedByEmployeeId = value;
        return true;
    }

    private static bool ApplyBulkNotes(IReadOnlyList<MeterReading> items, string? raw, out string error)
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

    private static bool ApplyBulkCreatedAt(IReadOnlyList<MeterReading> items, string? raw, out string error)
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


    private static IQueryable<MeterReading> ApplySearch(IQueryable<MeterReading> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        search = search.Trim();
        return query.Where(x => (x.SourceName != null && x.SourceName.Contains(search)) || (x.Notes != null && x.Notes.Contains(search)));
    }

    private static IQueryable<MeterReading> ApplyFilter(IQueryable<MeterReading> query, string? filterField, string? filterValue)
    {
        if (string.IsNullOrWhiteSpace(filterField) || string.IsNullOrWhiteSpace(filterValue)) return query;
        filterField = filterField.Trim();
        filterValue = filterValue.Trim();
        return filterField.ToLowerInvariant() switch
        {
            "meterreadingid" => long.TryParse(filterValue, out var MeterReadingIdValue) ? query.Where(x => x.MeterReadingId == MeterReadingIdValue) : query,
            "equipmentid" => int.TryParse(filterValue, out var EquipmentIdValue) ? query.Where(x => x.EquipmentId == EquipmentIdValue) : query,
            "readingat" => DateTime.TryParse(filterValue, out var ReadingAtValue) ? query.Where(x => x.ReadingAt == ReadingAtValue) : query,
            "hourmeter" => decimal.TryParse(filterValue, out var HourMeterValue) ? query.Where(x => x.HourMeter == HourMeterValue) : query,
            "odometerkm" => decimal.TryParse(filterValue, out var OdometerKmValue) ? query.Where(x => x.OdometerKm == OdometerKmValue) : query,
            "sourcename" => query.Where(x => x.SourceName != null && x.SourceName.Contains(filterValue)),
            "recordedbyemployeeid" => int.TryParse(filterValue, out var RecordedByEmployeeIdValue) ? query.Where(x => x.RecordedByEmployeeId == RecordedByEmployeeIdValue) : query,
            "notes" => query.Where(x => x.Notes != null && x.Notes.Contains(filterValue)),
            "createdat" => DateTime.TryParse(filterValue, out var CreatedAtValue) ? query.Where(x => x.CreatedAt == CreatedAtValue) : query,
            _ => query
        };
    }

    private static IQueryable<MeterReading> ApplySort(IQueryable<MeterReading> query, string? sortBy, string? sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) || string.Equals(sortDirection, "descending", StringComparison.OrdinalIgnoreCase);
        var field = string.IsNullOrWhiteSpace(sortBy) ? "MeterReadingId" : sortBy.Trim();
        return field.ToLowerInvariant() switch
        {
            "meterreadingid" => descending ? query.OrderByDescending(x => x.MeterReadingId) : query.OrderBy(x => x.MeterReadingId),
            "equipmentid" => descending ? query.OrderByDescending(x => x.EquipmentId) : query.OrderBy(x => x.EquipmentId),
            "readingat" => descending ? query.OrderByDescending(x => x.ReadingAt) : query.OrderBy(x => x.ReadingAt),
            "hourmeter" => descending ? query.OrderByDescending(x => x.HourMeter) : query.OrderBy(x => x.HourMeter),
            "odometerkm" => descending ? query.OrderByDescending(x => x.OdometerKm) : query.OrderBy(x => x.OdometerKm),
            "sourcename" => descending ? query.OrderByDescending(x => x.SourceName) : query.OrderBy(x => x.SourceName),
            "recordedbyemployeeid" => descending ? query.OrderByDescending(x => x.RecordedByEmployeeId) : query.OrderBy(x => x.RecordedByEmployeeId),
            "notes" => descending ? query.OrderByDescending(x => x.Notes) : query.OrderBy(x => x.Notes),
            "createdat" => descending ? query.OrderByDescending(x => x.CreatedAt) : query.OrderBy(x => x.CreatedAt),
            _ => descending ? query.OrderByDescending(x => x.MeterReadingId) : query.OrderBy(x => x.MeterReadingId)
        };
    }
    private static MeterReadingDto ToDto(MeterReading item) => new(
        item.MeterReadingId,
        item.EquipmentId,
        item.ReadingAt,
        item.HourMeter,
        item.OdometerKm,
        item.SourceName,
        item.RecordedByEmployeeId,
        item.Notes,
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
            Resource = "MeterReading",
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
        _changes.Clients.All.SendAsync(DataChangeHub.DataChangedMethod, new DataChangeNotification("MeterReading", action, resourceKey, DateTimeOffset.UtcNow), ct);

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
