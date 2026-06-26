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
[Route("api/tireInstallations")]
public sealed partial class TireInstallationController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<DataChangeHub> _changes;

    public TireInstallationController(AppDbContext db, IHubContext<DataChangeHub> changes)
    {
        _db = db;
        _changes = changes;
    }

    partial void OnBeforeCreate(CreateTireInstallationRequest request, TireInstallation item);
    partial void OnAfterCreate(TireInstallation item);
    partial void OnBeforeUpdate(TireInstallation item, UpdateTireInstallationRequest request);
    partial void OnBeforeDelete(TireInstallation item);

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<TireInstallationDto>>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null, [FromQuery] string? filterField = null, [FromQuery] string? filterValue = null, [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        IQueryable<TireInstallation> query = _db.Set<TireInstallation>().AsNoTracking();
        query = ApplySearch(query, search);
        query = ApplyFilter(query, filterField, filterValue);
        query = ApplySort(query, sortBy, sortDirection);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<TireInstallationDto>>.Success("records loaded", new PagedResult<TireInstallationDto>(items, page, pageSize, total)));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<TireInstallationDto>>> GetById(long id, CancellationToken ct)
    {
        IQueryable<TireInstallation> query = _db.Set<TireInstallation>().AsNoTracking();
        var item = await query.FirstOrDefaultAsync(x => x.TireInstallationId!.Equals(id), ct);
        return item is null ? NotFound(ApiResponse<object>.Warning("record not found")) : Ok(ApiResponse<TireInstallationDto>.Success("record loaded", ToDto(item)));
    }

    [HttpGet("{id}/history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuditTrailDto>>>> GetHistory(long id, CancellationToken ct)
    {
        var canReadRecord = await _db.Set<TireInstallation>().AsNoTracking().AnyAsync(x => x.TireInstallationId!.Equals(id), ct);
        if (!canReadRecord) return NotFound(ApiResponse<object>.Warning("record not found"));
        await EnsureAuditTrailTableAsync(ct);
        var resourceKey = Convert.ToString(id) ?? string.Empty;
        var history = await _db.AuditTrailEntries
            .AsNoTracking()
            .Where(entry => entry.Resource == "TireInstallation" && entry.ResourceKey == resourceKey)
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .Take(100)
            .Select(entry => ToAuditTrailDto(entry))
            .ToListAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<AuditTrailDto>>.Success("activity loaded", history));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<TireInstallationDto>>> Create(CreateTireInstallationRequest request, CancellationToken ct)
    {
        var item = new TireInstallation
        {
            TireId = request.TireId,
            EquipmentId = request.EquipmentId,
            PositionCode = request.PositionCode,
            InstalledAt = request.InstalledAt,
            RemovedAt = request.RemovedAt,
            InstallHourMeter = request.InstallHourMeter,
            RemoveHourMeter = request.RemoveHourMeter,
            RemovalReason = request.RemovalReason,
        };
        OnBeforeCreate(request, item);
        _db.Set<TireInstallation>().Add(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Created", Convert.ToString(item.TireInstallationId) ?? string.Empty, $"Created TireInstallation record {item.TireInstallationId}.", ToDto(item), ct);
        OnAfterCreate(item);
        await NotifyResourceChangedAsync("Created", Convert.ToString(item.TireInstallationId), ct);
        return CreatedAtAction(nameof(GetById), new { id = item.TireInstallationId }, ApiResponse<TireInstallationDto>.Success("record created", ToDto(item)));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, UpdateTireInstallationRequest request, CancellationToken ct)
    {
        var item = await _db.Set<TireInstallation>().FirstOrDefaultAsync(x => x.TireInstallationId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeUpdate(item, request);
        item.TireId = request.TireId;
        item.EquipmentId = request.EquipmentId;
        item.PositionCode = request.PositionCode;
        item.InstalledAt = request.InstalledAt;
        item.RemovedAt = request.RemovedAt;
        item.InstallHourMeter = request.InstallHourMeter;
        item.RemoveHourMeter = request.RemoveHourMeter;
        item.RemovalReason = request.RemovalReason;
        var auditChanges = GetEntityChanges(_db.Entry(item));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Updated", Convert.ToString(item.TireInstallationId) ?? string.Empty, $"Updated TireInstallation record {item.TireInstallationId}.", auditChanges, ct);
        await NotifyResourceChangedAsync("Updated", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record updated", new { updated = 1 }));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch(long id, UpdateTireInstallationRequest request, CancellationToken ct)
    {
        return await Update(id, request, ct);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var item = await _db.Set<TireInstallation>().FirstOrDefaultAsync(x => x.TireInstallationId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeDelete(item);
        _db.Set<TireInstallation>().Remove(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Deleted", Convert.ToString(id) ?? string.Empty, $"Hard deleted TireInstallation record {id}.", ToDto(item), ct);
        await NotifyResourceChangedAsync("Deleted", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record deleted", new { deleted = 1, mode = "Hard" }));
    }

    [HttpPost("bulk/export")]
    public async Task<ActionResult<ApiResponse<PagedResult<TireInstallationDto>>>> ExportBulk(BulkIdsRequest request, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return Ok(ApiResponse<PagedResult<TireInstallationDto>>.Warning("no records selected", new PagedResult<TireInstallationDto>(Array.Empty<TireInstallationDto>(), page, pageSize, 0)));
        IQueryable<TireInstallation> query = _db.Set<TireInstallation>().AsNoTracking().Where(x => ids.Contains(x.TireInstallationId));
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<TireInstallationDto>>.Success("records exported", new PagedResult<TireInstallationDto>(items, page, pageSize, total)));
    }

    [HttpPatch("bulk")]
    public async Task<IActionResult> UpdateBulk(BulkUpdateRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        if (string.IsNullOrWhiteSpace(request.Field)) return BadRequest(ApiResponse<object>.Error("error", new { error = "Choose a field to update." }));
        IQueryable<TireInstallation> query = _db.Set<TireInstallation>().Where(x => ids.Contains(x.TireInstallationId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return NotFound(ApiResponse<object>.Warning("records not found"));
        if (!ApplyBulkUpdate(items, request, out var error)) return BadRequest(ApiResponse<object>.Error("error", new { error }));
        var auditChanges = items.ToDictionary(item => Convert.ToString(item.TireInstallationId) ?? string.Empty, item => GetEntityChanges(_db.Entry(item)));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Updated", Convert.ToString(item.TireInstallationId) ?? string.Empty, $"Updated TireInstallation record {item.TireInstallationId} in bulk update.", auditChanges[Convert.ToString(item.TireInstallationId) ?? string.Empty], ct);
        await NotifyResourceChangedAsync("Updated", null, ct);
        return Ok(ApiResponse<object>.Success("records updated", new { updated = items.Count }));
    }

    [HttpPost("bulk/delete")]
    public async Task<IActionResult> DeleteBulk(BulkIdsRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        IQueryable<TireInstallation> query = _db.Set<TireInstallation>().Where(x => ids.Contains(x.TireInstallationId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return Ok(ApiResponse<object>.Warning("records not found", new { deleted = 0 }));
        foreach (var item in items)
        {
            OnBeforeDelete(item);
        }
        _db.Set<TireInstallation>().RemoveRange(items);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Deleted", Convert.ToString(item.TireInstallationId) ?? string.Empty, $"Hard deleted TireInstallation record {item.TireInstallationId} in bulk delete.", ToDto(item), ct);
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

    private static bool ApplyBulkUpdate(IReadOnlyList<TireInstallation> items, BulkUpdateRequest request, out string error)
    {
        error = string.Empty;
        return request.Field.Trim().ToLowerInvariant() switch
        {
            "tireid" => ApplyBulkTireId(items, request.Value, out error),
            "equipmentid" => ApplyBulkEquipmentId(items, request.Value, out error),
            "positioncode" => ApplyBulkPositionCode(items, request.Value, out error),
            "installedat" => ApplyBulkInstalledAt(items, request.Value, out error),
            "removedat" => ApplyBulkRemovedAt(items, request.Value, out error),
            "installhourmeter" => ApplyBulkInstallHourMeter(items, request.Value, out error),
            "removehourmeter" => ApplyBulkRemoveHourMeter(items, request.Value, out error),
            "removalreason" => ApplyBulkRemovalReason(items, request.Value, out error),
            _ => FailBulkUpdate("Field is not bulk editable.", out error)
        };
    }

    private static bool ApplyBulkTireId(IReadOnlyList<TireInstallation> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("TireId requires a int value.", out error);
        foreach (var item in items) item.TireId = value;
        return true;
    }

    private static bool ApplyBulkEquipmentId(IReadOnlyList<TireInstallation> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("EquipmentId requires a int value.", out error);
        foreach (var item in items) item.EquipmentId = value;
        return true;
    }

    private static bool ApplyBulkPositionCode(IReadOnlyList<TireInstallation> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.PositionCode = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkInstalledAt(IReadOnlyList<TireInstallation> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!DateTime.TryParse(raw, out var value)) return FailBulkUpdate("InstalledAt requires a DateTime value.", out error);
        foreach (var item in items) item.InstalledAt = value;
        return true;
    }

    private static bool ApplyBulkRemovedAt(IReadOnlyList<TireInstallation> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.RemovedAt = null;
            return true;
        }
        if (!DateTime.TryParse(raw, out var value)) return FailBulkUpdate("RemovedAt requires a DateTime value.", out error);
        foreach (var item in items) item.RemovedAt = value;
        return true;
    }

    private static bool ApplyBulkInstallHourMeter(IReadOnlyList<TireInstallation> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("InstallHourMeter requires a decimal value.", out error);
        foreach (var item in items) item.InstallHourMeter = value;
        return true;
    }

    private static bool ApplyBulkRemoveHourMeter(IReadOnlyList<TireInstallation> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.RemoveHourMeter = null;
            return true;
        }
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("RemoveHourMeter requires a decimal value.", out error);
        foreach (var item in items) item.RemoveHourMeter = value;
        return true;
    }

    private static bool ApplyBulkRemovalReason(IReadOnlyList<TireInstallation> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.RemovalReason = null;
            return true;
        }
        foreach (var item in items) item.RemovalReason = raw;
        return true;
    }

    private static bool FailBulkUpdate(string message, out string error)
    {
        error = message;
        return false;
    }


    private static IQueryable<TireInstallation> ApplySearch(IQueryable<TireInstallation> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        search = search.Trim();
        return query.Where(x => (x.PositionCode != null && x.PositionCode.Contains(search)) || (x.RemovalReason != null && x.RemovalReason.Contains(search)));
    }

    private static IQueryable<TireInstallation> ApplyFilter(IQueryable<TireInstallation> query, string? filterField, string? filterValue)
    {
        if (string.IsNullOrWhiteSpace(filterField) || string.IsNullOrWhiteSpace(filterValue)) return query;
        filterField = filterField.Trim();
        filterValue = filterValue.Trim();
        return filterField.ToLowerInvariant() switch
        {
            "tireinstallationid" => long.TryParse(filterValue, out var TireInstallationIdValue) ? query.Where(x => x.TireInstallationId == TireInstallationIdValue) : query,
            "tireid" => int.TryParse(filterValue, out var TireIdValue) ? query.Where(x => x.TireId == TireIdValue) : query,
            "equipmentid" => int.TryParse(filterValue, out var EquipmentIdValue) ? query.Where(x => x.EquipmentId == EquipmentIdValue) : query,
            "positioncode" => query.Where(x => x.PositionCode != null && x.PositionCode.Contains(filterValue)),
            "installedat" => DateTime.TryParse(filterValue, out var InstalledAtValue) ? query.Where(x => x.InstalledAt == InstalledAtValue) : query,
            "removedat" => DateTime.TryParse(filterValue, out var RemovedAtValue) ? query.Where(x => x.RemovedAt == RemovedAtValue) : query,
            "installhourmeter" => decimal.TryParse(filterValue, out var InstallHourMeterValue) ? query.Where(x => x.InstallHourMeter == InstallHourMeterValue) : query,
            "removehourmeter" => decimal.TryParse(filterValue, out var RemoveHourMeterValue) ? query.Where(x => x.RemoveHourMeter == RemoveHourMeterValue) : query,
            "removalreason" => query.Where(x => x.RemovalReason != null && x.RemovalReason.Contains(filterValue)),
            _ => query
        };
    }

    private static IQueryable<TireInstallation> ApplySort(IQueryable<TireInstallation> query, string? sortBy, string? sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) || string.Equals(sortDirection, "descending", StringComparison.OrdinalIgnoreCase);
        var field = string.IsNullOrWhiteSpace(sortBy) ? "TireInstallationId" : sortBy.Trim();
        return field.ToLowerInvariant() switch
        {
            "tireinstallationid" => descending ? query.OrderByDescending(x => x.TireInstallationId) : query.OrderBy(x => x.TireInstallationId),
            "tireid" => descending ? query.OrderByDescending(x => x.TireId) : query.OrderBy(x => x.TireId),
            "equipmentid" => descending ? query.OrderByDescending(x => x.EquipmentId) : query.OrderBy(x => x.EquipmentId),
            "positioncode" => descending ? query.OrderByDescending(x => x.PositionCode) : query.OrderBy(x => x.PositionCode),
            "installedat" => descending ? query.OrderByDescending(x => x.InstalledAt) : query.OrderBy(x => x.InstalledAt),
            "removedat" => descending ? query.OrderByDescending(x => x.RemovedAt) : query.OrderBy(x => x.RemovedAt),
            "installhourmeter" => descending ? query.OrderByDescending(x => x.InstallHourMeter) : query.OrderBy(x => x.InstallHourMeter),
            "removehourmeter" => descending ? query.OrderByDescending(x => x.RemoveHourMeter) : query.OrderBy(x => x.RemoveHourMeter),
            "removalreason" => descending ? query.OrderByDescending(x => x.RemovalReason) : query.OrderBy(x => x.RemovalReason),
            _ => descending ? query.OrderByDescending(x => x.TireInstallationId) : query.OrderBy(x => x.TireInstallationId)
        };
    }
    private static TireInstallationDto ToDto(TireInstallation item) => new(
        item.TireInstallationId,
        item.TireId,
        item.EquipmentId,
        item.PositionCode,
        item.InstalledAt,
        item.RemovedAt,
        item.InstallHourMeter,
        item.RemoveHourMeter,
        item.RemovalReason
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
            Resource = "TireInstallation",
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
        _changes.Clients.All.SendAsync(DataChangeHub.DataChangedMethod, new DataChangeNotification("TireInstallation", action, resourceKey, DateTimeOffset.UtcNow), ct);

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
