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
[Route("api/employees")]
public sealed partial class EmployeeController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<DataChangeHub> _changes;

    public EmployeeController(AppDbContext db, IHubContext<DataChangeHub> changes)
    {
        _db = db;
        _changes = changes;
    }

    partial void OnBeforeCreate(CreateEmployeeRequest request, Employee item);
    partial void OnAfterCreate(Employee item);
    partial void OnBeforeUpdate(Employee item, UpdateEmployeeRequest request);
    partial void OnBeforeDelete(Employee item);

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<EmployeeDto>>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null, [FromQuery] string? filterField = null, [FromQuery] string? filterValue = null, [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        IQueryable<Employee> query = _db.Set<Employee>().AsNoTracking();
        query = ApplySearch(query, search);
        query = ApplyFilter(query, filterField, filterValue);
        query = ApplySort(query, sortBy, sortDirection);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<EmployeeDto>>.Success("records loaded", new PagedResult<EmployeeDto>(items, page, pageSize, total)));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<EmployeeDto>>> GetById(int id, CancellationToken ct)
    {
        IQueryable<Employee> query = _db.Set<Employee>().AsNoTracking();
        var item = await query.FirstOrDefaultAsync(x => x.EmployeeId!.Equals(id), ct);
        return item is null ? NotFound(ApiResponse<object>.Warning("record not found")) : Ok(ApiResponse<EmployeeDto>.Success("record loaded", ToDto(item)));
    }

    [HttpGet("{id}/history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuditTrailDto>>>> GetHistory(int id, CancellationToken ct)
    {
        var canReadRecord = await _db.Set<Employee>().AsNoTracking().AnyAsync(x => x.EmployeeId!.Equals(id), ct);
        if (!canReadRecord) return NotFound(ApiResponse<object>.Warning("record not found"));
        await EnsureAuditTrailTableAsync(ct);
        var resourceKey = Convert.ToString(id) ?? string.Empty;
        var history = await _db.AuditTrailEntries
            .AsNoTracking()
            .Where(entry => entry.Resource == "Employee" && entry.ResourceKey == resourceKey)
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .Take(100)
            .Select(entry => ToAuditTrailDto(entry))
            .ToListAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<AuditTrailDto>>.Success("activity loaded", history));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<EmployeeDto>>> Create(CreateEmployeeRequest request, CancellationToken ct)
    {
        var item = new Employee
        {
            SiteId = request.SiteId,
            EmployeeCode = request.EmployeeCode,
            FullName = request.FullName,
            RoleName = request.RoleName,
            LicenseClass = request.LicenseClass,
            Phone = request.Phone,
            Email = request.Email,
            IsActive = request.IsActive,
            CreatedAt = request.CreatedAt,
        };
        OnBeforeCreate(request, item);
        _db.Set<Employee>().Add(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Created", Convert.ToString(item.EmployeeId) ?? string.Empty, $"Created Employee record {item.EmployeeId}.", ToDto(item), ct);
        OnAfterCreate(item);
        await NotifyResourceChangedAsync("Created", Convert.ToString(item.EmployeeId), ct);
        return CreatedAtAction(nameof(GetById), new { id = item.EmployeeId }, ApiResponse<EmployeeDto>.Success("record created", ToDto(item)));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateEmployeeRequest request, CancellationToken ct)
    {
        var item = await _db.Set<Employee>().FirstOrDefaultAsync(x => x.EmployeeId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeUpdate(item, request);
        item.SiteId = request.SiteId;
        item.EmployeeCode = request.EmployeeCode;
        item.FullName = request.FullName;
        item.RoleName = request.RoleName;
        item.LicenseClass = request.LicenseClass;
        item.Phone = request.Phone;
        item.Email = request.Email;
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
        await LogAuditTrailAsync("Updated", Convert.ToString(item.EmployeeId) ?? string.Empty, $"Updated Employee record {item.EmployeeId}.", auditChanges, ct);
        await NotifyResourceChangedAsync("Updated", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record updated", new { updated = 1 }));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch(int id, UpdateEmployeeRequest request, CancellationToken ct)
    {
        return await Update(id, request, ct);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var item = await _db.Set<Employee>().FirstOrDefaultAsync(x => x.EmployeeId!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeDelete(item);
        _db.Set<Employee>().Remove(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Deleted", Convert.ToString(id) ?? string.Empty, $"Hard deleted Employee record {id}.", ToDto(item), ct);
        await NotifyResourceChangedAsync("Deleted", Convert.ToString(id), ct);
        return Ok(ApiResponse<object>.Success("record deleted", new { deleted = 1, mode = "Hard" }));
    }

    [HttpPost("bulk/export")]
    public async Task<ActionResult<ApiResponse<PagedResult<EmployeeDto>>>> ExportBulk(BulkIdsRequest request, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return Ok(ApiResponse<PagedResult<EmployeeDto>>.Warning("no records selected", new PagedResult<EmployeeDto>(Array.Empty<EmployeeDto>(), page, pageSize, 0)));
        IQueryable<Employee> query = _db.Set<Employee>().AsNoTracking().Where(x => ids.Contains(x.EmployeeId));
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<EmployeeDto>>.Success("records exported", new PagedResult<EmployeeDto>(items, page, pageSize, total)));
    }

    [HttpPatch("bulk")]
    public async Task<IActionResult> UpdateBulk(BulkUpdateRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        if (string.IsNullOrWhiteSpace(request.Field)) return BadRequest(ApiResponse<object>.Error("error", new { error = "Choose a field to update." }));
        IQueryable<Employee> query = _db.Set<Employee>().Where(x => ids.Contains(x.EmployeeId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return NotFound(ApiResponse<object>.Warning("records not found"));
        if (!ApplyBulkUpdate(items, request, out var error)) return BadRequest(ApiResponse<object>.Error("error", new { error }));
        var auditChanges = items.ToDictionary(item => Convert.ToString(item.EmployeeId) ?? string.Empty, item => GetEntityChanges(_db.Entry(item)));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Updated", Convert.ToString(item.EmployeeId) ?? string.Empty, $"Updated Employee record {item.EmployeeId} in bulk update.", auditChanges[Convert.ToString(item.EmployeeId) ?? string.Empty], ct);
        await NotifyResourceChangedAsync("Updated", null, ct);
        return Ok(ApiResponse<object>.Success("records updated", new { updated = items.Count }));
    }

    [HttpPost("bulk/delete")]
    public async Task<IActionResult> DeleteBulk(BulkIdsRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        IQueryable<Employee> query = _db.Set<Employee>().Where(x => ids.Contains(x.EmployeeId));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return Ok(ApiResponse<object>.Warning("records not found", new { deleted = 0 }));
        foreach (var item in items)
        {
            OnBeforeDelete(item);
        }
        _db.Set<Employee>().RemoveRange(items);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Deleted", Convert.ToString(item.EmployeeId) ?? string.Empty, $"Hard deleted Employee record {item.EmployeeId} in bulk delete.", ToDto(item), ct);
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

    private static bool ApplyBulkUpdate(IReadOnlyList<Employee> items, BulkUpdateRequest request, out string error)
    {
        error = string.Empty;
        return request.Field.Trim().ToLowerInvariant() switch
        {
            "siteid" => ApplyBulkSiteId(items, request.Value, out error),
            "employeecode" => ApplyBulkEmployeeCode(items, request.Value, out error),
            "fullname" => ApplyBulkFullName(items, request.Value, out error),
            "rolename" => ApplyBulkRoleName(items, request.Value, out error),
            "licenseclass" => ApplyBulkLicenseClass(items, request.Value, out error),
            "phone" => ApplyBulkPhone(items, request.Value, out error),
            "email" => ApplyBulkEmail(items, request.Value, out error),
            "isactive" => ApplyBulkIsActive(items, request.Value, out error),
            "createdat" => ApplyBulkCreatedAt(items, request.Value, out error),
            _ => FailBulkUpdate("Field is not bulk editable.", out error)
        };
    }

    private static bool ApplyBulkSiteId(IReadOnlyList<Employee> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("SiteId requires a int value.", out error);
        foreach (var item in items) item.SiteId = value;
        return true;
    }

    private static bool ApplyBulkEmployeeCode(IReadOnlyList<Employee> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.EmployeeCode = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkFullName(IReadOnlyList<Employee> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.FullName = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkRoleName(IReadOnlyList<Employee> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.RoleName = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkLicenseClass(IReadOnlyList<Employee> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.LicenseClass = null;
            return true;
        }
        foreach (var item in items) item.LicenseClass = raw;
        return true;
    }

    private static bool ApplyBulkPhone(IReadOnlyList<Employee> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.Phone = null;
            return true;
        }
        foreach (var item in items) item.Phone = raw;
        return true;
    }

    private static bool ApplyBulkEmail(IReadOnlyList<Employee> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.Email = null;
            return true;
        }
        foreach (var item in items) item.Email = raw;
        return true;
    }

    private static bool ApplyBulkIsActive(IReadOnlyList<Employee> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!bool.TryParse(raw, out var value)) return FailBulkUpdate("IsActive requires a boolean value.", out error);
        foreach (var item in items) item.IsActive = value;
        return true;
    }

    private static bool ApplyBulkCreatedAt(IReadOnlyList<Employee> items, string? raw, out string error)
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


    private static IQueryable<Employee> ApplySearch(IQueryable<Employee> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        search = search.Trim();
        return query.Where(x => (x.EmployeeCode != null && x.EmployeeCode.Contains(search)) || (x.FullName != null && x.FullName.Contains(search)) || (x.RoleName != null && x.RoleName.Contains(search)) || (x.LicenseClass != null && x.LicenseClass.Contains(search)) || (x.Phone != null && x.Phone.Contains(search)) || (x.Email != null && x.Email.Contains(search)));
    }

    private static IQueryable<Employee> ApplyFilter(IQueryable<Employee> query, string? filterField, string? filterValue)
    {
        if (string.IsNullOrWhiteSpace(filterField) || string.IsNullOrWhiteSpace(filterValue)) return query;
        filterField = filterField.Trim();
        filterValue = filterValue.Trim();
        return filterField.ToLowerInvariant() switch
        {
            "employeeid" => int.TryParse(filterValue, out var EmployeeIdValue) ? query.Where(x => x.EmployeeId == EmployeeIdValue) : query,
            "siteid" => int.TryParse(filterValue, out var SiteIdValue) ? query.Where(x => x.SiteId == SiteIdValue) : query,
            "employeecode" => query.Where(x => x.EmployeeCode != null && x.EmployeeCode.Contains(filterValue)),
            "fullname" => query.Where(x => x.FullName != null && x.FullName.Contains(filterValue)),
            "rolename" => query.Where(x => x.RoleName != null && x.RoleName.Contains(filterValue)),
            "licenseclass" => query.Where(x => x.LicenseClass != null && x.LicenseClass.Contains(filterValue)),
            "phone" => query.Where(x => x.Phone != null && x.Phone.Contains(filterValue)),
            "email" => query.Where(x => x.Email != null && x.Email.Contains(filterValue)),
            "isactive" => bool.TryParse(filterValue, out var IsActiveValue) ? query.Where(x => x.IsActive == IsActiveValue) : query,
            "createdat" => DateTime.TryParse(filterValue, out var CreatedAtValue) ? query.Where(x => x.CreatedAt == CreatedAtValue) : query,
            _ => query
        };
    }

    private static IQueryable<Employee> ApplySort(IQueryable<Employee> query, string? sortBy, string? sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) || string.Equals(sortDirection, "descending", StringComparison.OrdinalIgnoreCase);
        var field = string.IsNullOrWhiteSpace(sortBy) ? "EmployeeId" : sortBy.Trim();
        return field.ToLowerInvariant() switch
        {
            "employeeid" => descending ? query.OrderByDescending(x => x.EmployeeId) : query.OrderBy(x => x.EmployeeId),
            "siteid" => descending ? query.OrderByDescending(x => x.SiteId) : query.OrderBy(x => x.SiteId),
            "employeecode" => descending ? query.OrderByDescending(x => x.EmployeeCode) : query.OrderBy(x => x.EmployeeCode),
            "fullname" => descending ? query.OrderByDescending(x => x.FullName) : query.OrderBy(x => x.FullName),
            "rolename" => descending ? query.OrderByDescending(x => x.RoleName) : query.OrderBy(x => x.RoleName),
            "licenseclass" => descending ? query.OrderByDescending(x => x.LicenseClass) : query.OrderBy(x => x.LicenseClass),
            "phone" => descending ? query.OrderByDescending(x => x.Phone) : query.OrderBy(x => x.Phone),
            "email" => descending ? query.OrderByDescending(x => x.Email) : query.OrderBy(x => x.Email),
            "isactive" => descending ? query.OrderByDescending(x => x.IsActive) : query.OrderBy(x => x.IsActive),
            "createdat" => descending ? query.OrderByDescending(x => x.CreatedAt) : query.OrderBy(x => x.CreatedAt),
            _ => descending ? query.OrderByDescending(x => x.EmployeeId) : query.OrderBy(x => x.EmployeeId)
        };
    }
    private static EmployeeDto ToDto(Employee item) => new(
        item.EmployeeId,
        item.SiteId,
        item.EmployeeCode,
        item.FullName,
        item.RoleName,
        item.LicenseClass,
        item.Phone,
        item.Email,
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
            Resource = "Employee",
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
        _changes.Clients.All.SendAsync(DataChangeHub.DataChangedMethod, new DataChangeNotification("Employee", action, resourceKey, DateTimeOffset.UtcNow), ct);

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
