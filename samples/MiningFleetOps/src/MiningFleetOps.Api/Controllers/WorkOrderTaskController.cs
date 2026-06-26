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
[Route("api/workOrderTasks")]
public sealed partial class WorkOrderTaskController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<DataChangeHub> _changes;

    public WorkOrderTaskController(AppDbContext db, IHubContext<DataChangeHub> changes)
    {
        _db = db;
        _changes = changes;
    }

    partial void OnBeforeCreate(CreateWorkOrderTaskRequest request, WorkOrderTask item);
    partial void OnAfterCreate(WorkOrderTask item);
    partial void OnBeforeUpdate(WorkOrderTask item, UpdateWorkOrderTaskRequest request);
    partial void OnBeforeDelete(WorkOrderTask item);

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<WorkOrderTaskDto>>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null, [FromQuery] string? filterField = null, [FromQuery] string? filterValue = null, [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        IQueryable<WorkOrderTask> query = _db.Set<WorkOrderTask>().AsNoTracking();
        query = ApplySearch(query, search);
        query = ApplyFilter(query, filterField, filterValue);
        query = ApplySort(query, sortBy, sortDirection);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<WorkOrderTaskDto>>.Success("records loaded", new PagedResult<WorkOrderTaskDto>(items, page, pageSize, total)));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<WorkOrderTaskDto>>> GetById(long id, CancellationToken ct)
    {
        IQueryable<WorkOrderTask> query = _db.Set<WorkOrderTask>().AsNoTracking();
        var item = await query.FirstOrDefaultAsync(x => x.WorkOrderTaskId!.Equals(id), ct);
        return item is null ? NotFound(ApiResponse<object>.Warning("record not found")) : Ok(ApiResponse<WorkOrderTaskDto>.Success("record loaded", ToDto(item)));
    }

    [HttpGet("{id}/history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuditTrailDto>>>> GetHistory(long id, CancellationToken ct)
    {
        var canReadRecord = await _db.Set<WorkOrderTask>().AsNoTracking().AnyAsync(x => x.WorkOrderTaskId!.Equals(id), ct);
        if (!canReadRecord) return NotFound(ApiResponse<object>.Warning("record not found"));
        await EnsureAuditTrailTableAsync(ct);
        var resourceKey = Convert.ToString(id) ?? string.Empty;
        var history = await _db.AuditTrailEntries
            .AsNoTracking()
            .Where(entry => entry.Resource == "WorkOrderTask" && entry.ResourceKey == resourceKey)
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .Take(100)
            .Select(entry => ToAuditTrailDto(entry))
            .ToListAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<AuditTrailDto>>.Success("activity loaded", history));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<WorkOrderTaskDto>>> Create(CreateWorkOrderTaskRequest request, CancellationToken ct)
    {
        var item = new WorkOrderTask
        {
            WorkOrderId = request.WorkOrderId,
            TaskSequence = request.TaskSequence,
            TaskDescription = request.TaskDescription,
            IsCompleted = request.IsCompleted,
            CompletedAt = request.CompletedAt,
            CompletedByEmployeeId = request.CompletedByEmployeeId,
        };
        OnBeforeCreate(request, item);
        _db.Set<WorkOrderTask>().Add(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Created", Convert.ToString(item.WorkOrderTaskId) ?? string.Empty, $"Created WorkOrderTask record {item.WorkOrderTaskId}.", ToDto(item), ct);
        OnAfterCreate(item);
        await NotifyResourceChangedAsync("Created", Convert.ToString(item.WorkOrderTaskId), ct);
        return CreatedAtAction(nameof(GetById), new { id = item.WorkOrderTaskId }, ApiResponse<WorkOrderTaskDto>.Success("record created", ToDto(item)));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, UpdateWorkOrderTaskRequest request, CancellationToken ct)
    {
        var item = await _db.Set<WorkOrderTask>().FirstOrDefaultAsync(x => x.WorkOrderTaskId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeUpdate(item, request);
        item.WorkOrderId = request.WorkOrderId;
        item.TaskSequence = request.TaskSequence;
        item.TaskDescription = request.TaskDescription;
        item.IsCompleted = request.IsCompleted;
        item.CompletedAt = request.CompletedAt;
        item.CompletedByEmployeeId = request.CompletedByEmployeeId;
        var auditChanges = GetEntityChanges(_db.Entry(item));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Updated", Convert.ToString(item.WorkOrderTaskId) ?? string.Empty, $"Updated WorkOrderTask record {item.WorkOrderTaskId}.", auditChanges, ct);
        await NotifyResourceChangedAsync("Updated", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record updated", new { updated = 1 }));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch(long id, UpdateWorkOrderTaskRequest request, CancellationToken ct)
    {
        return await Update(id, request, ct);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var item = await _db.Set<WorkOrderTask>().FirstOrDefaultAsync(x => x.WorkOrderTaskId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeDelete(item);
        _db.Set<WorkOrderTask>().Remove(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Deleted", Convert.ToString(id) ?? string.Empty, $"Hard deleted WorkOrderTask record {id}.", ToDto(item), ct);
        await NotifyResourceChangedAsync("Deleted", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record deleted", new { deleted = 1, mode = "Hard" }));
    }

    [HttpPost("bulk/export")]
    public async Task<ActionResult<ApiResponse<PagedResult<WorkOrderTaskDto>>>> ExportBulk(BulkIdsRequest request, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return Ok(ApiResponse<PagedResult<WorkOrderTaskDto>>.Warning("no records selected", new PagedResult<WorkOrderTaskDto>(Array.Empty<WorkOrderTaskDto>(), page, pageSize, 0)));
        IQueryable<WorkOrderTask> query = _db.Set<WorkOrderTask>().AsNoTracking().Where(x => ids.Contains(x.WorkOrderTaskId));
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<WorkOrderTaskDto>>.Success("records exported", new PagedResult<WorkOrderTaskDto>(items, page, pageSize, total)));
    }

    [HttpPatch("bulk")]
    public async Task<IActionResult> UpdateBulk(BulkUpdateRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        if (string.IsNullOrWhiteSpace(request.Field)) return BadRequest(ApiResponse<object>.Error("error", new { error = "Choose a field to update." }));
        IQueryable<WorkOrderTask> query = _db.Set<WorkOrderTask>().Where(x => ids.Contains(x.WorkOrderTaskId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return NotFound(ApiResponse<object>.Warning("records not found"));
        if (!ApplyBulkUpdate(items, request, out var error)) return BadRequest(ApiResponse<object>.Error("error", new { error }));
        var auditChanges = items.ToDictionary(item => Convert.ToString(item.WorkOrderTaskId) ?? string.Empty, item => GetEntityChanges(_db.Entry(item)));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Updated", Convert.ToString(item.WorkOrderTaskId) ?? string.Empty, $"Updated WorkOrderTask record {item.WorkOrderTaskId} in bulk update.", auditChanges[Convert.ToString(item.WorkOrderTaskId) ?? string.Empty], ct);
        await NotifyResourceChangedAsync("Updated", null, ct);
        return Ok(ApiResponse<object>.Success("records updated", new { updated = items.Count }));
    }

    [HttpPost("bulk/delete")]
    public async Task<IActionResult> DeleteBulk(BulkIdsRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        IQueryable<WorkOrderTask> query = _db.Set<WorkOrderTask>().Where(x => ids.Contains(x.WorkOrderTaskId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return Ok(ApiResponse<object>.Warning("records not found", new { deleted = 0 }));
        foreach (var item in items)
        {
            OnBeforeDelete(item);
        }
        _db.Set<WorkOrderTask>().RemoveRange(items);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Deleted", Convert.ToString(item.WorkOrderTaskId) ?? string.Empty, $"Hard deleted WorkOrderTask record {item.WorkOrderTaskId} in bulk delete.", ToDto(item), ct);
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

    private static bool ApplyBulkUpdate(IReadOnlyList<WorkOrderTask> items, BulkUpdateRequest request, out string error)
    {
        error = string.Empty;
        return request.Field.Trim().ToLowerInvariant() switch
        {
            "workorderid" => ApplyBulkWorkOrderId(items, request.Value, out error),
            "tasksequence" => ApplyBulkTaskSequence(items, request.Value, out error),
            "taskdescription" => ApplyBulkTaskDescription(items, request.Value, out error),
            "iscompleted" => ApplyBulkIsCompleted(items, request.Value, out error),
            "completedat" => ApplyBulkCompletedAt(items, request.Value, out error),
            "completedbyemployeeid" => ApplyBulkCompletedByEmployeeId(items, request.Value, out error),
            _ => FailBulkUpdate("Field is not bulk editable.", out error)
        };
    }

    private static bool ApplyBulkWorkOrderId(IReadOnlyList<WorkOrderTask> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!long.TryParse(raw, out var value)) return FailBulkUpdate("WorkOrderId requires a long value.", out error);
        foreach (var item in items) item.WorkOrderId = value;
        return true;
    }

    private static bool ApplyBulkTaskSequence(IReadOnlyList<WorkOrderTask> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("TaskSequence requires a int value.", out error);
        foreach (var item in items) item.TaskSequence = value;
        return true;
    }

    private static bool ApplyBulkTaskDescription(IReadOnlyList<WorkOrderTask> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.TaskDescription = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkIsCompleted(IReadOnlyList<WorkOrderTask> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!bool.TryParse(raw, out var value)) return FailBulkUpdate("IsCompleted requires a boolean value.", out error);
        foreach (var item in items) item.IsCompleted = value;
        return true;
    }

    private static bool ApplyBulkCompletedAt(IReadOnlyList<WorkOrderTask> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.CompletedAt = null;
            return true;
        }
        if (!DateTime.TryParse(raw, out var value)) return FailBulkUpdate("CompletedAt requires a DateTime value.", out error);
        foreach (var item in items) item.CompletedAt = value;
        return true;
    }

    private static bool ApplyBulkCompletedByEmployeeId(IReadOnlyList<WorkOrderTask> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.CompletedByEmployeeId = null;
            return true;
        }
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("CompletedByEmployeeId requires a int value.", out error);
        foreach (var item in items) item.CompletedByEmployeeId = value;
        return true;
    }

    private static bool FailBulkUpdate(string message, out string error)
    {
        error = message;
        return false;
    }


    private static IQueryable<WorkOrderTask> ApplySearch(IQueryable<WorkOrderTask> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        search = search.Trim();
        return query.Where(x => (x.TaskDescription != null && x.TaskDescription.Contains(search)));
    }

    private static IQueryable<WorkOrderTask> ApplyFilter(IQueryable<WorkOrderTask> query, string? filterField, string? filterValue)
    {
        if (string.IsNullOrWhiteSpace(filterField) || string.IsNullOrWhiteSpace(filterValue)) return query;
        filterField = filterField.Trim();
        filterValue = filterValue.Trim();
        return filterField.ToLowerInvariant() switch
        {
            "workordertaskid" => long.TryParse(filterValue, out var WorkOrderTaskIdValue) ? query.Where(x => x.WorkOrderTaskId == WorkOrderTaskIdValue) : query,
            "workorderid" => long.TryParse(filterValue, out var WorkOrderIdValue) ? query.Where(x => x.WorkOrderId == WorkOrderIdValue) : query,
            "tasksequence" => int.TryParse(filterValue, out var TaskSequenceValue) ? query.Where(x => x.TaskSequence == TaskSequenceValue) : query,
            "taskdescription" => query.Where(x => x.TaskDescription != null && x.TaskDescription.Contains(filterValue)),
            "iscompleted" => bool.TryParse(filterValue, out var IsCompletedValue) ? query.Where(x => x.IsCompleted == IsCompletedValue) : query,
            "completedat" => DateTime.TryParse(filterValue, out var CompletedAtValue) ? query.Where(x => x.CompletedAt == CompletedAtValue) : query,
            "completedbyemployeeid" => int.TryParse(filterValue, out var CompletedByEmployeeIdValue) ? query.Where(x => x.CompletedByEmployeeId == CompletedByEmployeeIdValue) : query,
            _ => query
        };
    }

    private static IQueryable<WorkOrderTask> ApplySort(IQueryable<WorkOrderTask> query, string? sortBy, string? sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) || string.Equals(sortDirection, "descending", StringComparison.OrdinalIgnoreCase);
        var field = string.IsNullOrWhiteSpace(sortBy) ? "WorkOrderTaskId" : sortBy.Trim();
        return field.ToLowerInvariant() switch
        {
            "workordertaskid" => descending ? query.OrderByDescending(x => x.WorkOrderTaskId) : query.OrderBy(x => x.WorkOrderTaskId),
            "workorderid" => descending ? query.OrderByDescending(x => x.WorkOrderId) : query.OrderBy(x => x.WorkOrderId),
            "tasksequence" => descending ? query.OrderByDescending(x => x.TaskSequence) : query.OrderBy(x => x.TaskSequence),
            "taskdescription" => descending ? query.OrderByDescending(x => x.TaskDescription) : query.OrderBy(x => x.TaskDescription),
            "iscompleted" => descending ? query.OrderByDescending(x => x.IsCompleted) : query.OrderBy(x => x.IsCompleted),
            "completedat" => descending ? query.OrderByDescending(x => x.CompletedAt) : query.OrderBy(x => x.CompletedAt),
            "completedbyemployeeid" => descending ? query.OrderByDescending(x => x.CompletedByEmployeeId) : query.OrderBy(x => x.CompletedByEmployeeId),
            _ => descending ? query.OrderByDescending(x => x.WorkOrderTaskId) : query.OrderBy(x => x.WorkOrderTaskId)
        };
    }
    private static WorkOrderTaskDto ToDto(WorkOrderTask item) => new(
        item.WorkOrderTaskId,
        item.WorkOrderId,
        item.TaskSequence,
        item.TaskDescription,
        item.IsCompleted,
        item.CompletedAt,
        item.CompletedByEmployeeId
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
            Resource = "WorkOrderTask",
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
        _changes.Clients.All.SendAsync(DataChangeHub.DataChangedMethod, new DataChangeNotification("WorkOrderTask", action, resourceKey, DateTimeOffset.UtcNow), ct);

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
