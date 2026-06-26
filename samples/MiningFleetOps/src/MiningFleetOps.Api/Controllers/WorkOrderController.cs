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
[Route("api/workOrders")]
public sealed partial class WorkOrderController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<DataChangeHub> _changes;

    public WorkOrderController(AppDbContext db, IHubContext<DataChangeHub> changes)
    {
        _db = db;
        _changes = changes;
    }

    partial void OnBeforeCreate(CreateWorkOrderRequest request, WorkOrder item);
    partial void OnAfterCreate(WorkOrder item);
    partial void OnBeforeUpdate(WorkOrder item, UpdateWorkOrderRequest request);
    partial void OnBeforeDelete(WorkOrder item);

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<WorkOrderDto>>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null, [FromQuery] string? filterField = null, [FromQuery] string? filterValue = null, [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        IQueryable<WorkOrder> query = _db.Set<WorkOrder>().AsNoTracking();
        query = ApplySearch(query, search);
        query = ApplyFilter(query, filterField, filterValue);
        query = ApplySort(query, sortBy, sortDirection);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<WorkOrderDto>>.Success("records loaded", new PagedResult<WorkOrderDto>(items, page, pageSize, total)));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<WorkOrderDto>>> GetById(long id, CancellationToken ct)
    {
        IQueryable<WorkOrder> query = _db.Set<WorkOrder>().AsNoTracking();
        var item = await query.FirstOrDefaultAsync(x => x.WorkOrderId!.Equals(id), ct);
        return item is null ? NotFound(ApiResponse<object>.Warning("record not found")) : Ok(ApiResponse<WorkOrderDto>.Success("record loaded", ToDto(item)));
    }

    [HttpGet("{id}/history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuditTrailDto>>>> GetHistory(long id, CancellationToken ct)
    {
        var canReadRecord = await _db.Set<WorkOrder>().AsNoTracking().AnyAsync(x => x.WorkOrderId!.Equals(id), ct);
        if (!canReadRecord) return NotFound(ApiResponse<object>.Warning("record not found"));
        await EnsureAuditTrailTableAsync(ct);
        var resourceKey = Convert.ToString(id) ?? string.Empty;
        var history = await _db.AuditTrailEntries
            .AsNoTracking()
            .Where(entry => entry.Resource == "WorkOrder" && entry.ResourceKey == resourceKey)
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .Take(100)
            .Select(entry => ToAuditTrailDto(entry))
            .ToListAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<AuditTrailDto>>.Success("activity loaded", history));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<WorkOrderDto>>> Create(CreateWorkOrderRequest request, CancellationToken ct)
    {
        var item = new WorkOrder
        {
            WorkOrderNumber = request.WorkOrderNumber,
            EquipmentId = request.EquipmentId,
            MaintenancePlanId = request.MaintenancePlanId,
            OpenedAt = request.OpenedAt,
            ClosedAt = request.ClosedAt,
            PriorityName = request.PriorityName,
            WorkOrderType = request.WorkOrderType,
            Status = request.Status,
            OpenHourMeter = request.OpenHourMeter,
            CloseHourMeter = request.CloseHourMeter,
            ProblemDescription = request.ProblemDescription,
            CorrectiveAction = request.CorrectiveAction,
            LaborHours = request.LaborHours,
            EstimatedCost = request.EstimatedCost,
            ActualCost = request.ActualCost,
            CreatedByEmployeeId = request.CreatedByEmployeeId,
            ClosedByEmployeeId = request.ClosedByEmployeeId,
            DowntimeHours = request.DowntimeHours,
        };
        OnBeforeCreate(request, item);
        _db.Set<WorkOrder>().Add(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Created", Convert.ToString(item.WorkOrderId) ?? string.Empty, $"Created WorkOrder record {item.WorkOrderId}.", ToDto(item), ct);
        OnAfterCreate(item);
        await NotifyResourceChangedAsync("Created", Convert.ToString(item.WorkOrderId), ct);
        return CreatedAtAction(nameof(GetById), new { id = item.WorkOrderId }, ApiResponse<WorkOrderDto>.Success("record created", ToDto(item)));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, UpdateWorkOrderRequest request, CancellationToken ct)
    {
        var item = await _db.Set<WorkOrder>().FirstOrDefaultAsync(x => x.WorkOrderId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeUpdate(item, request);
        item.WorkOrderNumber = request.WorkOrderNumber;
        item.EquipmentId = request.EquipmentId;
        item.MaintenancePlanId = request.MaintenancePlanId;
        item.OpenedAt = request.OpenedAt;
        item.ClosedAt = request.ClosedAt;
        item.PriorityName = request.PriorityName;
        item.WorkOrderType = request.WorkOrderType;
        item.Status = request.Status;
        item.OpenHourMeter = request.OpenHourMeter;
        item.CloseHourMeter = request.CloseHourMeter;
        item.ProblemDescription = request.ProblemDescription;
        item.CorrectiveAction = request.CorrectiveAction;
        item.LaborHours = request.LaborHours;
        item.EstimatedCost = request.EstimatedCost;
        item.ActualCost = request.ActualCost;
        item.CreatedByEmployeeId = request.CreatedByEmployeeId;
        item.ClosedByEmployeeId = request.ClosedByEmployeeId;
        item.DowntimeHours = request.DowntimeHours;
        var auditChanges = GetEntityChanges(_db.Entry(item));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Updated", Convert.ToString(item.WorkOrderId) ?? string.Empty, $"Updated WorkOrder record {item.WorkOrderId}.", auditChanges, ct);
        await NotifyResourceChangedAsync("Updated", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record updated", new { updated = 1 }));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch(long id, UpdateWorkOrderRequest request, CancellationToken ct)
    {
        return await Update(id, request, ct);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var item = await _db.Set<WorkOrder>().FirstOrDefaultAsync(x => x.WorkOrderId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeDelete(item);
        _db.Set<WorkOrder>().Remove(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Deleted", Convert.ToString(id) ?? string.Empty, $"Hard deleted WorkOrder record {id}.", ToDto(item), ct);
        await NotifyResourceChangedAsync("Deleted", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record deleted", new { deleted = 1, mode = "Hard" }));
    }

    [HttpPost("bulk/export")]
    public async Task<ActionResult<ApiResponse<PagedResult<WorkOrderDto>>>> ExportBulk(BulkIdsRequest request, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return Ok(ApiResponse<PagedResult<WorkOrderDto>>.Warning("no records selected", new PagedResult<WorkOrderDto>(Array.Empty<WorkOrderDto>(), page, pageSize, 0)));
        IQueryable<WorkOrder> query = _db.Set<WorkOrder>().AsNoTracking().Where(x => ids.Contains(x.WorkOrderId));
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<WorkOrderDto>>.Success("records exported", new PagedResult<WorkOrderDto>(items, page, pageSize, total)));
    }

    [HttpPatch("bulk")]
    public async Task<IActionResult> UpdateBulk(BulkUpdateRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        if (string.IsNullOrWhiteSpace(request.Field)) return BadRequest(ApiResponse<object>.Error("error", new { error = "Choose a field to update." }));
        IQueryable<WorkOrder> query = _db.Set<WorkOrder>().Where(x => ids.Contains(x.WorkOrderId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return NotFound(ApiResponse<object>.Warning("records not found"));
        if (!ApplyBulkUpdate(items, request, out var error)) return BadRequest(ApiResponse<object>.Error("error", new { error }));
        var auditChanges = items.ToDictionary(item => Convert.ToString(item.WorkOrderId) ?? string.Empty, item => GetEntityChanges(_db.Entry(item)));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Updated", Convert.ToString(item.WorkOrderId) ?? string.Empty, $"Updated WorkOrder record {item.WorkOrderId} in bulk update.", auditChanges[Convert.ToString(item.WorkOrderId) ?? string.Empty], ct);
        await NotifyResourceChangedAsync("Updated", null, ct);
        return Ok(ApiResponse<object>.Success("records updated", new { updated = items.Count }));
    }

    [HttpPost("bulk/delete")]
    public async Task<IActionResult> DeleteBulk(BulkIdsRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        IQueryable<WorkOrder> query = _db.Set<WorkOrder>().Where(x => ids.Contains(x.WorkOrderId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return Ok(ApiResponse<object>.Warning("records not found", new { deleted = 0 }));
        foreach (var item in items)
        {
            OnBeforeDelete(item);
        }
        _db.Set<WorkOrder>().RemoveRange(items);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Deleted", Convert.ToString(item.WorkOrderId) ?? string.Empty, $"Hard deleted WorkOrder record {item.WorkOrderId} in bulk delete.", ToDto(item), ct);
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

    private static bool ApplyBulkUpdate(IReadOnlyList<WorkOrder> items, BulkUpdateRequest request, out string error)
    {
        error = string.Empty;
        return request.Field.Trim().ToLowerInvariant() switch
        {
            "workordernumber" => ApplyBulkWorkOrderNumber(items, request.Value, out error),
            "equipmentid" => ApplyBulkEquipmentId(items, request.Value, out error),
            "maintenanceplanid" => ApplyBulkMaintenancePlanId(items, request.Value, out error),
            "openedat" => ApplyBulkOpenedAt(items, request.Value, out error),
            "closedat" => ApplyBulkClosedAt(items, request.Value, out error),
            "priorityname" => ApplyBulkPriorityName(items, request.Value, out error),
            "workordertype" => ApplyBulkWorkOrderType(items, request.Value, out error),
            "status" => ApplyBulkStatus(items, request.Value, out error),
            "openhourmeter" => ApplyBulkOpenHourMeter(items, request.Value, out error),
            "closehourmeter" => ApplyBulkCloseHourMeter(items, request.Value, out error),
            "problemdescription" => ApplyBulkProblemDescription(items, request.Value, out error),
            "correctiveaction" => ApplyBulkCorrectiveAction(items, request.Value, out error),
            "laborhours" => ApplyBulkLaborHours(items, request.Value, out error),
            "estimatedcost" => ApplyBulkEstimatedCost(items, request.Value, out error),
            "actualcost" => ApplyBulkActualCost(items, request.Value, out error),
            "createdbyemployeeid" => ApplyBulkCreatedByEmployeeId(items, request.Value, out error),
            "closedbyemployeeid" => ApplyBulkClosedByEmployeeId(items, request.Value, out error),
            "downtimehours" => ApplyBulkDowntimeHours(items, request.Value, out error),
            _ => FailBulkUpdate("Field is not bulk editable.", out error)
        };
    }

    private static bool ApplyBulkWorkOrderNumber(IReadOnlyList<WorkOrder> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.WorkOrderNumber = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkEquipmentId(IReadOnlyList<WorkOrder> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("EquipmentId requires a int value.", out error);
        foreach (var item in items) item.EquipmentId = value;
        return true;
    }

    private static bool ApplyBulkMaintenancePlanId(IReadOnlyList<WorkOrder> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.MaintenancePlanId = null;
            return true;
        }
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("MaintenancePlanId requires a int value.", out error);
        foreach (var item in items) item.MaintenancePlanId = value;
        return true;
    }

    private static bool ApplyBulkOpenedAt(IReadOnlyList<WorkOrder> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!DateTime.TryParse(raw, out var value)) return FailBulkUpdate("OpenedAt requires a DateTime value.", out error);
        foreach (var item in items) item.OpenedAt = value;
        return true;
    }

    private static bool ApplyBulkClosedAt(IReadOnlyList<WorkOrder> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.ClosedAt = null;
            return true;
        }
        if (!DateTime.TryParse(raw, out var value)) return FailBulkUpdate("ClosedAt requires a DateTime value.", out error);
        foreach (var item in items) item.ClosedAt = value;
        return true;
    }

    private static bool ApplyBulkPriorityName(IReadOnlyList<WorkOrder> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.PriorityName = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkWorkOrderType(IReadOnlyList<WorkOrder> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.WorkOrderType = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkStatus(IReadOnlyList<WorkOrder> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.Status = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkOpenHourMeter(IReadOnlyList<WorkOrder> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("OpenHourMeter requires a decimal value.", out error);
        foreach (var item in items) item.OpenHourMeter = value;
        return true;
    }

    private static bool ApplyBulkCloseHourMeter(IReadOnlyList<WorkOrder> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.CloseHourMeter = null;
            return true;
        }
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("CloseHourMeter requires a decimal value.", out error);
        foreach (var item in items) item.CloseHourMeter = value;
        return true;
    }

    private static bool ApplyBulkProblemDescription(IReadOnlyList<WorkOrder> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.ProblemDescription = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkCorrectiveAction(IReadOnlyList<WorkOrder> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.CorrectiveAction = null;
            return true;
        }
        foreach (var item in items) item.CorrectiveAction = raw;
        return true;
    }

    private static bool ApplyBulkLaborHours(IReadOnlyList<WorkOrder> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("LaborHours requires a decimal value.", out error);
        foreach (var item in items) item.LaborHours = value;
        return true;
    }

    private static bool ApplyBulkEstimatedCost(IReadOnlyList<WorkOrder> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.EstimatedCost = null;
            return true;
        }
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("EstimatedCost requires a decimal value.", out error);
        foreach (var item in items) item.EstimatedCost = value;
        return true;
    }

    private static bool ApplyBulkActualCost(IReadOnlyList<WorkOrder> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.ActualCost = null;
            return true;
        }
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("ActualCost requires a decimal value.", out error);
        foreach (var item in items) item.ActualCost = value;
        return true;
    }

    private static bool ApplyBulkCreatedByEmployeeId(IReadOnlyList<WorkOrder> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.CreatedByEmployeeId = null;
            return true;
        }
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("CreatedByEmployeeId requires a int value.", out error);
        foreach (var item in items) item.CreatedByEmployeeId = value;
        return true;
    }

    private static bool ApplyBulkClosedByEmployeeId(IReadOnlyList<WorkOrder> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.ClosedByEmployeeId = null;
            return true;
        }
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("ClosedByEmployeeId requires a int value.", out error);
        foreach (var item in items) item.ClosedByEmployeeId = value;
        return true;
    }

    private static bool ApplyBulkDowntimeHours(IReadOnlyList<WorkOrder> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.DowntimeHours = null;
            return true;
        }
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("DowntimeHours requires a decimal value.", out error);
        foreach (var item in items) item.DowntimeHours = value;
        return true;
    }

    private static bool FailBulkUpdate(string message, out string error)
    {
        error = message;
        return false;
    }


    private static IQueryable<WorkOrder> ApplySearch(IQueryable<WorkOrder> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        search = search.Trim();
        return query.Where(x => (x.WorkOrderNumber != null && x.WorkOrderNumber.Contains(search)) || (x.PriorityName != null && x.PriorityName.Contains(search)) || (x.WorkOrderType != null && x.WorkOrderType.Contains(search)) || (x.Status != null && x.Status.Contains(search)) || (x.ProblemDescription != null && x.ProblemDescription.Contains(search)) || (x.CorrectiveAction != null && x.CorrectiveAction.Contains(search)));
    }

    private static IQueryable<WorkOrder> ApplyFilter(IQueryable<WorkOrder> query, string? filterField, string? filterValue)
    {
        if (string.IsNullOrWhiteSpace(filterField) || string.IsNullOrWhiteSpace(filterValue)) return query;
        filterField = filterField.Trim();
        filterValue = filterValue.Trim();
        return filterField.ToLowerInvariant() switch
        {
            "workorderid" => long.TryParse(filterValue, out var WorkOrderIdValue) ? query.Where(x => x.WorkOrderId == WorkOrderIdValue) : query,
            "workordernumber" => query.Where(x => x.WorkOrderNumber != null && x.WorkOrderNumber.Contains(filterValue)),
            "equipmentid" => int.TryParse(filterValue, out var EquipmentIdValue) ? query.Where(x => x.EquipmentId == EquipmentIdValue) : query,
            "maintenanceplanid" => int.TryParse(filterValue, out var MaintenancePlanIdValue) ? query.Where(x => x.MaintenancePlanId == MaintenancePlanIdValue) : query,
            "openedat" => DateTime.TryParse(filterValue, out var OpenedAtValue) ? query.Where(x => x.OpenedAt == OpenedAtValue) : query,
            "closedat" => DateTime.TryParse(filterValue, out var ClosedAtValue) ? query.Where(x => x.ClosedAt == ClosedAtValue) : query,
            "priorityname" => query.Where(x => x.PriorityName != null && x.PriorityName.Contains(filterValue)),
            "workordertype" => query.Where(x => x.WorkOrderType != null && x.WorkOrderType.Contains(filterValue)),
            "status" => query.Where(x => x.Status != null && x.Status.Contains(filterValue)),
            "openhourmeter" => decimal.TryParse(filterValue, out var OpenHourMeterValue) ? query.Where(x => x.OpenHourMeter == OpenHourMeterValue) : query,
            "closehourmeter" => decimal.TryParse(filterValue, out var CloseHourMeterValue) ? query.Where(x => x.CloseHourMeter == CloseHourMeterValue) : query,
            "problemdescription" => query.Where(x => x.ProblemDescription != null && x.ProblemDescription.Contains(filterValue)),
            "correctiveaction" => query.Where(x => x.CorrectiveAction != null && x.CorrectiveAction.Contains(filterValue)),
            "laborhours" => decimal.TryParse(filterValue, out var LaborHoursValue) ? query.Where(x => x.LaborHours == LaborHoursValue) : query,
            "estimatedcost" => decimal.TryParse(filterValue, out var EstimatedCostValue) ? query.Where(x => x.EstimatedCost == EstimatedCostValue) : query,
            "actualcost" => decimal.TryParse(filterValue, out var ActualCostValue) ? query.Where(x => x.ActualCost == ActualCostValue) : query,
            "createdbyemployeeid" => int.TryParse(filterValue, out var CreatedByEmployeeIdValue) ? query.Where(x => x.CreatedByEmployeeId == CreatedByEmployeeIdValue) : query,
            "closedbyemployeeid" => int.TryParse(filterValue, out var ClosedByEmployeeIdValue) ? query.Where(x => x.ClosedByEmployeeId == ClosedByEmployeeIdValue) : query,
            "downtimehours" => decimal.TryParse(filterValue, out var DowntimeHoursValue) ? query.Where(x => x.DowntimeHours == DowntimeHoursValue) : query,
            _ => query
        };
    }

    private static IQueryable<WorkOrder> ApplySort(IQueryable<WorkOrder> query, string? sortBy, string? sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) || string.Equals(sortDirection, "descending", StringComparison.OrdinalIgnoreCase);
        var field = string.IsNullOrWhiteSpace(sortBy) ? "WorkOrderId" : sortBy.Trim();
        return field.ToLowerInvariant() switch
        {
            "workorderid" => descending ? query.OrderByDescending(x => x.WorkOrderId) : query.OrderBy(x => x.WorkOrderId),
            "workordernumber" => descending ? query.OrderByDescending(x => x.WorkOrderNumber) : query.OrderBy(x => x.WorkOrderNumber),
            "equipmentid" => descending ? query.OrderByDescending(x => x.EquipmentId) : query.OrderBy(x => x.EquipmentId),
            "maintenanceplanid" => descending ? query.OrderByDescending(x => x.MaintenancePlanId) : query.OrderBy(x => x.MaintenancePlanId),
            "openedat" => descending ? query.OrderByDescending(x => x.OpenedAt) : query.OrderBy(x => x.OpenedAt),
            "closedat" => descending ? query.OrderByDescending(x => x.ClosedAt) : query.OrderBy(x => x.ClosedAt),
            "priorityname" => descending ? query.OrderByDescending(x => x.PriorityName) : query.OrderBy(x => x.PriorityName),
            "workordertype" => descending ? query.OrderByDescending(x => x.WorkOrderType) : query.OrderBy(x => x.WorkOrderType),
            "status" => descending ? query.OrderByDescending(x => x.Status) : query.OrderBy(x => x.Status),
            "openhourmeter" => descending ? query.OrderByDescending(x => x.OpenHourMeter) : query.OrderBy(x => x.OpenHourMeter),
            "closehourmeter" => descending ? query.OrderByDescending(x => x.CloseHourMeter) : query.OrderBy(x => x.CloseHourMeter),
            "problemdescription" => descending ? query.OrderByDescending(x => x.ProblemDescription) : query.OrderBy(x => x.ProblemDescription),
            "correctiveaction" => descending ? query.OrderByDescending(x => x.CorrectiveAction) : query.OrderBy(x => x.CorrectiveAction),
            "laborhours" => descending ? query.OrderByDescending(x => x.LaborHours) : query.OrderBy(x => x.LaborHours),
            "estimatedcost" => descending ? query.OrderByDescending(x => x.EstimatedCost) : query.OrderBy(x => x.EstimatedCost),
            "actualcost" => descending ? query.OrderByDescending(x => x.ActualCost) : query.OrderBy(x => x.ActualCost),
            "createdbyemployeeid" => descending ? query.OrderByDescending(x => x.CreatedByEmployeeId) : query.OrderBy(x => x.CreatedByEmployeeId),
            "closedbyemployeeid" => descending ? query.OrderByDescending(x => x.ClosedByEmployeeId) : query.OrderBy(x => x.ClosedByEmployeeId),
            "downtimehours" => descending ? query.OrderByDescending(x => x.DowntimeHours) : query.OrderBy(x => x.DowntimeHours),
            _ => descending ? query.OrderByDescending(x => x.WorkOrderId) : query.OrderBy(x => x.WorkOrderId)
        };
    }
    private static WorkOrderDto ToDto(WorkOrder item) => new(
        item.WorkOrderId,
        item.WorkOrderNumber,
        item.EquipmentId,
        item.MaintenancePlanId,
        item.OpenedAt,
        item.ClosedAt,
        item.PriorityName,
        item.WorkOrderType,
        item.Status,
        item.OpenHourMeter,
        item.CloseHourMeter,
        item.ProblemDescription,
        item.CorrectiveAction,
        item.LaborHours,
        item.EstimatedCost,
        item.ActualCost,
        item.CreatedByEmployeeId,
        item.ClosedByEmployeeId,
        item.DowntimeHours
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
            Resource = "WorkOrder",
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
        _changes.Clients.All.SendAsync(DataChangeHub.DataChangedMethod, new DataChangeNotification("WorkOrder", action, resourceKey, DateTimeOffset.UtcNow), ct);

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
