using AdventureWorksLT2017Api.Application.Common;
using AdventureWorksLT2017Api.Application.Dtos;
using AdventureWorksLT2017Api.Domain.Auditing;
using AdventureWorksLT2017Api.Domain.Entities;
using AdventureWorksLT2017Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace AdventureWorksLT2017Api.Api.Controllers;

[ApiController]
[Route("api/errorLogs")]
public sealed partial class ErrorLogController : ControllerBase
{
    private readonly AppDbContext _db;

    public ErrorLogController(AppDbContext db)
    {
        _db = db;
    }

    partial void OnBeforeCreate(CreateErrorLogRequest request, ErrorLog item);
    partial void OnAfterCreate(ErrorLog item);
    partial void OnBeforeUpdate(ErrorLog item, UpdateErrorLogRequest request);
    partial void OnBeforeDelete(ErrorLog item);

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<ErrorLogDto>>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null, [FromQuery] string? filterField = null, [FromQuery] string? filterValue = null, [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        IQueryable<ErrorLog> query = _db.Set<ErrorLog>().AsNoTracking();
        query = ApplySearch(query, search);
        query = ApplyFilter(query, filterField, filterValue);
        query = ApplySort(query, sortBy, sortDirection);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<ErrorLogDto>>.Success("records loaded", new PagedResult<ErrorLogDto>(items, page, pageSize, total)));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<ErrorLogDto>>> GetById(int id, CancellationToken ct)
    {
        IQueryable<ErrorLog> query = _db.Set<ErrorLog>().AsNoTracking();
        var item = await query.FirstOrDefaultAsync(x => x.ErrorLogID!.Equals(id), ct);
        return item is null ? NotFound(ApiResponse<object>.Warning("record not found")) : Ok(ApiResponse<ErrorLogDto>.Success("record loaded", ToDto(item)));
    }

    [HttpGet("{id}/history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuditTrailDto>>>> GetHistory(int id, CancellationToken ct)
    {
        var canReadRecord = await _db.Set<ErrorLog>().AsNoTracking().AnyAsync(x => x.ErrorLogID!.Equals(id), ct);
        if (!canReadRecord) return NotFound(ApiResponse<object>.Warning("record not found"));
        await EnsureAuditTrailTableAsync(ct);
        var resourceKey = Convert.ToString(id) ?? string.Empty;
        var history = await _db.AuditTrailEntries
            .AsNoTracking()
            .Where(entry => entry.Resource == "ErrorLog" && entry.ResourceKey == resourceKey)
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .Take(100)
            .Select(entry => ToAuditTrailDto(entry))
            .ToListAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<AuditTrailDto>>.Success("activity loaded", history));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ErrorLogDto>>> Create(CreateErrorLogRequest request, CancellationToken ct)
    {
        var item = new ErrorLog
        {
            ErrorTime = request.ErrorTime,
            UserName = request.UserName,
            ErrorNumber = request.ErrorNumber,
            ErrorSeverity = request.ErrorSeverity,
            ErrorState = request.ErrorState,
            ErrorProcedure = request.ErrorProcedure,
            ErrorLine = request.ErrorLine,
            ErrorMessage = request.ErrorMessage,
        };
        OnBeforeCreate(request, item);
        _db.Set<ErrorLog>().Add(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Created", Convert.ToString(item.ErrorLogID) ?? string.Empty, $"Created ErrorLog record {item.ErrorLogID}.", ToDto(item), ct);
        OnAfterCreate(item);
        return CreatedAtAction(nameof(GetById), new { id = item.ErrorLogID }, ApiResponse<ErrorLogDto>.Success("record created", ToDto(item)));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateErrorLogRequest request, CancellationToken ct)
    {
        var item = await _db.Set<ErrorLog>().FirstOrDefaultAsync(x => x.ErrorLogID!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeUpdate(item, request);
        item.ErrorTime = request.ErrorTime;
        item.UserName = request.UserName;
        item.ErrorNumber = request.ErrorNumber;
        item.ErrorSeverity = request.ErrorSeverity;
        item.ErrorState = request.ErrorState;
        item.ErrorProcedure = request.ErrorProcedure;
        item.ErrorLine = request.ErrorLine;
        item.ErrorMessage = request.ErrorMessage;
        var auditChanges = GetEntityChanges(_db.Entry(item));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Updated", Convert.ToString(item.ErrorLogID) ?? string.Empty, $"Updated ErrorLog record {item.ErrorLogID}.", auditChanges, ct);
        return Ok(ApiResponse<object>.Success("record updated", new { updated = 1 }));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch(int id, UpdateErrorLogRequest request, CancellationToken ct)
    {
        return await Update(id, request, ct);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var item = await _db.Set<ErrorLog>().FirstOrDefaultAsync(x => x.ErrorLogID!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeDelete(item);
        _db.Set<ErrorLog>().Remove(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Deleted", Convert.ToString(id) ?? string.Empty, $"Hard deleted ErrorLog record {id}.", ToDto(item), ct);
        return Ok(ApiResponse<object>.Success("record deleted", new { deleted = 1, mode = "Hard" }));
    }

    [HttpPost("bulk/export")]
    public async Task<ActionResult<ApiResponse<PagedResult<ErrorLogDto>>>> ExportBulk(BulkIdsRequest request, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return Ok(ApiResponse<PagedResult<ErrorLogDto>>.Warning("no records selected", new PagedResult<ErrorLogDto>(Array.Empty<ErrorLogDto>(), page, pageSize, 0)));
        IQueryable<ErrorLog> query = _db.Set<ErrorLog>().AsNoTracking().Where(x => ids.Contains(x.ErrorLogID));
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<ErrorLogDto>>.Success("records exported", new PagedResult<ErrorLogDto>(items, page, pageSize, total)));
    }

    [HttpPatch("bulk")]
    public async Task<IActionResult> UpdateBulk(BulkUpdateRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        if (string.IsNullOrWhiteSpace(request.Field)) return BadRequest(ApiResponse<object>.Error("error", new { error = "Choose a field to update." }));
        IQueryable<ErrorLog> query = _db.Set<ErrorLog>().Where(x => ids.Contains(x.ErrorLogID));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return NotFound(ApiResponse<object>.Warning("records not found"));
        if (!ApplyBulkUpdate(items, request, out var error)) return BadRequest(ApiResponse<object>.Error("error", new { error }));
        var auditChanges = items.ToDictionary(item => Convert.ToString(item.ErrorLogID) ?? string.Empty, item => GetEntityChanges(_db.Entry(item)));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Updated", Convert.ToString(item.ErrorLogID) ?? string.Empty, $"Updated ErrorLog record {item.ErrorLogID} in bulk update.", auditChanges[Convert.ToString(item.ErrorLogID) ?? string.Empty], ct);
        return Ok(ApiResponse<object>.Success("records updated", new { updated = items.Count }));
    }

    [HttpPost("bulk/delete")]
    public async Task<IActionResult> DeleteBulk(BulkIdsRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        IQueryable<ErrorLog> query = _db.Set<ErrorLog>().Where(x => ids.Contains(x.ErrorLogID));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return Ok(ApiResponse<object>.Warning("records not found", new { deleted = 0 }));
        foreach (var item in items)
        {
            OnBeforeDelete(item);
        }
        _db.Set<ErrorLog>().RemoveRange(items);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Deleted", Convert.ToString(item.ErrorLogID) ?? string.Empty, $"Hard deleted ErrorLog record {item.ErrorLogID} in bulk delete.", ToDto(item), ct);
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

    private static bool ApplyBulkUpdate(IReadOnlyList<ErrorLog> items, BulkUpdateRequest request, out string error)
    {
        error = string.Empty;
        return request.Field.Trim().ToLowerInvariant() switch
        {
            "errortime" => ApplyBulkErrorTime(items, request.Value, out error),
            "username" => ApplyBulkUserName(items, request.Value, out error),
            "errornumber" => ApplyBulkErrorNumber(items, request.Value, out error),
            "errorseverity" => ApplyBulkErrorSeverity(items, request.Value, out error),
            "errorstate" => ApplyBulkErrorState(items, request.Value, out error),
            "errorprocedure" => ApplyBulkErrorProcedure(items, request.Value, out error),
            "errorline" => ApplyBulkErrorLine(items, request.Value, out error),
            "errormessage" => ApplyBulkErrorMessage(items, request.Value, out error),
            _ => FailBulkUpdate("Field is not bulk editable.", out error)
        };
    }

    private static bool ApplyBulkErrorTime(IReadOnlyList<ErrorLog> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!DateTime.TryParse(raw, out var value)) return FailBulkUpdate("ErrorTime requires a DateTime value.", out error);
        foreach (var item in items) item.ErrorTime = value;
        return true;
    }

    private static bool ApplyBulkUserName(IReadOnlyList<ErrorLog> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.UserName = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkErrorNumber(IReadOnlyList<ErrorLog> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("ErrorNumber requires a int value.", out error);
        foreach (var item in items) item.ErrorNumber = value;
        return true;
    }

    private static bool ApplyBulkErrorSeverity(IReadOnlyList<ErrorLog> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.ErrorSeverity = null;
            return true;
        }
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("ErrorSeverity requires a int value.", out error);
        foreach (var item in items) item.ErrorSeverity = value;
        return true;
    }

    private static bool ApplyBulkErrorState(IReadOnlyList<ErrorLog> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.ErrorState = null;
            return true;
        }
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("ErrorState requires a int value.", out error);
        foreach (var item in items) item.ErrorState = value;
        return true;
    }

    private static bool ApplyBulkErrorProcedure(IReadOnlyList<ErrorLog> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.ErrorProcedure = null;
            return true;
        }
        foreach (var item in items) item.ErrorProcedure = raw;
        return true;
    }

    private static bool ApplyBulkErrorLine(IReadOnlyList<ErrorLog> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.ErrorLine = null;
            return true;
        }
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("ErrorLine requires a int value.", out error);
        foreach (var item in items) item.ErrorLine = value;
        return true;
    }

    private static bool ApplyBulkErrorMessage(IReadOnlyList<ErrorLog> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.ErrorMessage = raw ?? string.Empty;
        return true;
    }

    private static bool FailBulkUpdate(string message, out string error)
    {
        error = message;
        return false;
    }


    private static IQueryable<ErrorLog> ApplySearch(IQueryable<ErrorLog> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        search = search.Trim();
        return query.Where(x => (x.UserName != null && x.UserName.Contains(search)) || (x.ErrorProcedure != null && x.ErrorProcedure.Contains(search)) || (x.ErrorMessage != null && x.ErrorMessage.Contains(search)));
    }

    private static IQueryable<ErrorLog> ApplyFilter(IQueryable<ErrorLog> query, string? filterField, string? filterValue)
    {
        if (string.IsNullOrWhiteSpace(filterField) || string.IsNullOrWhiteSpace(filterValue)) return query;
        filterField = filterField.Trim();
        filterValue = filterValue.Trim();
        return filterField.ToLowerInvariant() switch
        {
            "errorlogid" => int.TryParse(filterValue, out var ErrorLogIDValue) ? query.Where(x => x.ErrorLogID == ErrorLogIDValue) : query,
            "errortime" => DateTime.TryParse(filterValue, out var ErrorTimeValue) ? query.Where(x => x.ErrorTime == ErrorTimeValue) : query,
            "username" => query.Where(x => x.UserName != null && x.UserName.Contains(filterValue)),
            "errornumber" => int.TryParse(filterValue, out var ErrorNumberValue) ? query.Where(x => x.ErrorNumber == ErrorNumberValue) : query,
            "errorseverity" => int.TryParse(filterValue, out var ErrorSeverityValue) ? query.Where(x => x.ErrorSeverity == ErrorSeverityValue) : query,
            "errorstate" => int.TryParse(filterValue, out var ErrorStateValue) ? query.Where(x => x.ErrorState == ErrorStateValue) : query,
            "errorprocedure" => query.Where(x => x.ErrorProcedure != null && x.ErrorProcedure.Contains(filterValue)),
            "errorline" => int.TryParse(filterValue, out var ErrorLineValue) ? query.Where(x => x.ErrorLine == ErrorLineValue) : query,
            "errormessage" => query.Where(x => x.ErrorMessage != null && x.ErrorMessage.Contains(filterValue)),
            _ => query
        };
    }

    private static IQueryable<ErrorLog> ApplySort(IQueryable<ErrorLog> query, string? sortBy, string? sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) || string.Equals(sortDirection, "descending", StringComparison.OrdinalIgnoreCase);
        var field = string.IsNullOrWhiteSpace(sortBy) ? "ErrorLogID" : sortBy.Trim();
        return field.ToLowerInvariant() switch
        {
            "errorlogid" => descending ? query.OrderByDescending(x => x.ErrorLogID) : query.OrderBy(x => x.ErrorLogID),
            "errortime" => descending ? query.OrderByDescending(x => x.ErrorTime) : query.OrderBy(x => x.ErrorTime),
            "username" => descending ? query.OrderByDescending(x => x.UserName) : query.OrderBy(x => x.UserName),
            "errornumber" => descending ? query.OrderByDescending(x => x.ErrorNumber) : query.OrderBy(x => x.ErrorNumber),
            "errorseverity" => descending ? query.OrderByDescending(x => x.ErrorSeverity) : query.OrderBy(x => x.ErrorSeverity),
            "errorstate" => descending ? query.OrderByDescending(x => x.ErrorState) : query.OrderBy(x => x.ErrorState),
            "errorprocedure" => descending ? query.OrderByDescending(x => x.ErrorProcedure) : query.OrderBy(x => x.ErrorProcedure),
            "errorline" => descending ? query.OrderByDescending(x => x.ErrorLine) : query.OrderBy(x => x.ErrorLine),
            "errormessage" => descending ? query.OrderByDescending(x => x.ErrorMessage) : query.OrderBy(x => x.ErrorMessage),
            _ => descending ? query.OrderByDescending(x => x.ErrorLogID) : query.OrderBy(x => x.ErrorLogID)
        };
    }
    private static ErrorLogDto ToDto(ErrorLog item) => new(
        item.ErrorLogID,
        item.ErrorTime,
        item.UserName,
        item.ErrorNumber,
        item.ErrorSeverity,
        item.ErrorState,
        item.ErrorProcedure,
        item.ErrorLine,
        item.ErrorMessage
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
            Resource = "ErrorLog",
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
