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
[Route("api/customers")]
public sealed partial class CustomerController : ControllerBase
{
    private readonly AppDbContext _db;

    public CustomerController(AppDbContext db)
    {
        _db = db;
    }

    partial void OnBeforeCreate(CreateCustomerRequest request, Customer item);
    partial void OnAfterCreate(Customer item);
    partial void OnBeforeUpdate(Customer item, UpdateCustomerRequest request);
    partial void OnBeforeDelete(Customer item);

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<CustomerDto>>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null, [FromQuery] string? filterField = null, [FromQuery] string? filterValue = null, [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        IQueryable<Customer> query = _db.Set<Customer>().AsNoTracking();
        query = ApplySearch(query, search);
        query = ApplyFilter(query, filterField, filterValue);
        query = ApplySort(query, sortBy, sortDirection);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<CustomerDto>>.Success("records loaded", new PagedResult<CustomerDto>(items, page, pageSize, total)));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<CustomerDto>>> GetById(int id, CancellationToken ct)
    {
        IQueryable<Customer> query = _db.Set<Customer>().AsNoTracking();
        var item = await query.FirstOrDefaultAsync(x => x.CustomerID!.Equals(id), ct);
        return item is null ? NotFound(ApiResponse<object>.Warning("record not found")) : Ok(ApiResponse<CustomerDto>.Success("record loaded", ToDto(item)));
    }

    [HttpGet("{id}/history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuditTrailDto>>>> GetHistory(int id, CancellationToken ct)
    {
        var canReadRecord = await _db.Set<Customer>().AsNoTracking().AnyAsync(x => x.CustomerID!.Equals(id), ct);
        if (!canReadRecord) return NotFound(ApiResponse<object>.Warning("record not found"));
        await EnsureAuditTrailTableAsync(ct);
        var resourceKey = Convert.ToString(id) ?? string.Empty;
        var history = await _db.AuditTrailEntries
            .AsNoTracking()
            .Where(entry => entry.Resource == "Customer" && entry.ResourceKey == resourceKey)
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .Take(100)
            .Select(entry => ToAuditTrailDto(entry))
            .ToListAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<AuditTrailDto>>.Success("activity loaded", history));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<CustomerDto>>> Create(CreateCustomerRequest request, CancellationToken ct)
    {
        var item = new Customer
        {
            NameStyle = request.NameStyle,
            Title = request.Title,
            FirstName = request.FirstName,
            MiddleName = request.MiddleName,
            LastName = request.LastName,
            Suffix = request.Suffix,
            CompanyName = request.CompanyName,
            SalesPerson = request.SalesPerson,
            EmailAddress = request.EmailAddress,
            Phone = request.Phone,
            PasswordHash = request.PasswordHash,
            PasswordSalt = request.PasswordSalt,
            Rowguid = request.Rowguid,
            ModifiedDate = request.ModifiedDate,
        };
        OnBeforeCreate(request, item);
        _db.Set<Customer>().Add(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Created", Convert.ToString(item.CustomerID) ?? string.Empty, $"Created Customer record {item.CustomerID}.", ToDto(item), ct);
        OnAfterCreate(item);
        return CreatedAtAction(nameof(GetById), new { id = item.CustomerID }, ApiResponse<CustomerDto>.Success("record created", ToDto(item)));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateCustomerRequest request, CancellationToken ct)
    {
        var item = await _db.Set<Customer>().FirstOrDefaultAsync(x => x.CustomerID!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeUpdate(item, request);
        item.NameStyle = request.NameStyle;
        item.Title = request.Title;
        item.FirstName = request.FirstName;
        item.MiddleName = request.MiddleName;
        item.LastName = request.LastName;
        item.Suffix = request.Suffix;
        item.CompanyName = request.CompanyName;
        item.SalesPerson = request.SalesPerson;
        item.EmailAddress = request.EmailAddress;
        item.Phone = request.Phone;
        item.PasswordHash = request.PasswordHash;
        item.PasswordSalt = request.PasswordSalt;
        item.Rowguid = request.Rowguid;
        item.ModifiedDate = request.ModifiedDate;
        var auditChanges = GetEntityChanges(_db.Entry(item));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Updated", Convert.ToString(item.CustomerID) ?? string.Empty, $"Updated Customer record {item.CustomerID}.", auditChanges, ct);
        return Ok(ApiResponse<object>.Success("record updated", new { updated = 1 }));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch(int id, UpdateCustomerRequest request, CancellationToken ct)
    {
        return await Update(id, request, ct);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var item = await _db.Set<Customer>().FirstOrDefaultAsync(x => x.CustomerID!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeDelete(item);
        _db.Set<Customer>().Remove(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Deleted", Convert.ToString(id) ?? string.Empty, $"Hard deleted Customer record {id}.", ToDto(item), ct);
        return Ok(ApiResponse<object>.Success("record deleted", new { deleted = 1, mode = "Hard" }));
    }

    [HttpPost("bulk/export")]
    public async Task<ActionResult<ApiResponse<PagedResult<CustomerDto>>>> ExportBulk(BulkIdsRequest request, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return Ok(ApiResponse<PagedResult<CustomerDto>>.Warning("no records selected", new PagedResult<CustomerDto>(Array.Empty<CustomerDto>(), page, pageSize, 0)));
        IQueryable<Customer> query = _db.Set<Customer>().AsNoTracking().Where(x => ids.Contains(x.CustomerID));
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<CustomerDto>>.Success("records exported", new PagedResult<CustomerDto>(items, page, pageSize, total)));
    }

    [HttpPatch("bulk")]
    public async Task<IActionResult> UpdateBulk(BulkUpdateRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        if (string.IsNullOrWhiteSpace(request.Field)) return BadRequest(ApiResponse<object>.Error("error", new { error = "Choose a field to update." }));
        IQueryable<Customer> query = _db.Set<Customer>().Where(x => ids.Contains(x.CustomerID));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return NotFound(ApiResponse<object>.Warning("records not found"));
        if (!ApplyBulkUpdate(items, request, out var error)) return BadRequest(ApiResponse<object>.Error("error", new { error }));
        var auditChanges = items.ToDictionary(item => Convert.ToString(item.CustomerID) ?? string.Empty, item => GetEntityChanges(_db.Entry(item)));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Updated", Convert.ToString(item.CustomerID) ?? string.Empty, $"Updated Customer record {item.CustomerID} in bulk update.", auditChanges[Convert.ToString(item.CustomerID) ?? string.Empty], ct);
        return Ok(ApiResponse<object>.Success("records updated", new { updated = items.Count }));
    }

    [HttpPost("bulk/delete")]
    public async Task<IActionResult> DeleteBulk(BulkIdsRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        IQueryable<Customer> query = _db.Set<Customer>().Where(x => ids.Contains(x.CustomerID));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return Ok(ApiResponse<object>.Warning("records not found", new { deleted = 0 }));
        foreach (var item in items)
        {
            OnBeforeDelete(item);
        }
        _db.Set<Customer>().RemoveRange(items);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Deleted", Convert.ToString(item.CustomerID) ?? string.Empty, $"Hard deleted Customer record {item.CustomerID} in bulk delete.", ToDto(item), ct);
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

    private static bool ApplyBulkUpdate(IReadOnlyList<Customer> items, BulkUpdateRequest request, out string error)
    {
        error = string.Empty;
        return request.Field.Trim().ToLowerInvariant() switch
        {
            "namestyle" => ApplyBulkNameStyle(items, request.Value, out error),
            "title" => ApplyBulkTitle(items, request.Value, out error),
            "firstname" => ApplyBulkFirstName(items, request.Value, out error),
            "middlename" => ApplyBulkMiddleName(items, request.Value, out error),
            "lastname" => ApplyBulkLastName(items, request.Value, out error),
            "suffix" => ApplyBulkSuffix(items, request.Value, out error),
            "companyname" => ApplyBulkCompanyName(items, request.Value, out error),
            "salesperson" => ApplyBulkSalesPerson(items, request.Value, out error),
            "emailaddress" => ApplyBulkEmailAddress(items, request.Value, out error),
            "phone" => ApplyBulkPhone(items, request.Value, out error),
            "passwordhash" => ApplyBulkPasswordHash(items, request.Value, out error),
            "passwordsalt" => ApplyBulkPasswordSalt(items, request.Value, out error),
            "rowguid" => ApplyBulkRowguid(items, request.Value, out error),
            "modifieddate" => ApplyBulkModifiedDate(items, request.Value, out error),
            _ => FailBulkUpdate("Field is not bulk editable.", out error)
        };
    }

    private static bool ApplyBulkNameStyle(IReadOnlyList<Customer> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!bool.TryParse(raw, out var value)) return FailBulkUpdate("NameStyle requires a boolean value.", out error);
        foreach (var item in items) item.NameStyle = value;
        return true;
    }

    private static bool ApplyBulkTitle(IReadOnlyList<Customer> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.Title = null;
            return true;
        }
        foreach (var item in items) item.Title = raw;
        return true;
    }

    private static bool ApplyBulkFirstName(IReadOnlyList<Customer> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.FirstName = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkMiddleName(IReadOnlyList<Customer> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.MiddleName = null;
            return true;
        }
        foreach (var item in items) item.MiddleName = raw;
        return true;
    }

    private static bool ApplyBulkLastName(IReadOnlyList<Customer> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.LastName = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkSuffix(IReadOnlyList<Customer> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.Suffix = null;
            return true;
        }
        foreach (var item in items) item.Suffix = raw;
        return true;
    }

    private static bool ApplyBulkCompanyName(IReadOnlyList<Customer> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.CompanyName = null;
            return true;
        }
        foreach (var item in items) item.CompanyName = raw;
        return true;
    }

    private static bool ApplyBulkSalesPerson(IReadOnlyList<Customer> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.SalesPerson = null;
            return true;
        }
        foreach (var item in items) item.SalesPerson = raw;
        return true;
    }

    private static bool ApplyBulkEmailAddress(IReadOnlyList<Customer> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.EmailAddress = null;
            return true;
        }
        foreach (var item in items) item.EmailAddress = raw;
        return true;
    }

    private static bool ApplyBulkPhone(IReadOnlyList<Customer> items, string? raw, out string error)
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

    private static bool ApplyBulkPasswordHash(IReadOnlyList<Customer> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.PasswordHash = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkPasswordSalt(IReadOnlyList<Customer> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.PasswordSalt = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkRowguid(IReadOnlyList<Customer> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!Guid.TryParse(raw, out var value)) return FailBulkUpdate("Rowguid requires a Guid value.", out error);
        foreach (var item in items) item.Rowguid = value;
        return true;
    }

    private static bool ApplyBulkModifiedDate(IReadOnlyList<Customer> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!DateTime.TryParse(raw, out var value)) return FailBulkUpdate("ModifiedDate requires a DateTime value.", out error);
        foreach (var item in items) item.ModifiedDate = value;
        return true;
    }

    private static bool FailBulkUpdate(string message, out string error)
    {
        error = message;
        return false;
    }


    private static IQueryable<Customer> ApplySearch(IQueryable<Customer> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        search = search.Trim();
        return query.Where(x => (x.Title != null && x.Title.Contains(search)) || (x.FirstName != null && x.FirstName.Contains(search)) || (x.MiddleName != null && x.MiddleName.Contains(search)) || (x.LastName != null && x.LastName.Contains(search)) || (x.Suffix != null && x.Suffix.Contains(search)) || (x.CompanyName != null && x.CompanyName.Contains(search)) || (x.SalesPerson != null && x.SalesPerson.Contains(search)) || (x.EmailAddress != null && x.EmailAddress.Contains(search)) || (x.Phone != null && x.Phone.Contains(search)) || (x.PasswordHash != null && x.PasswordHash.Contains(search)) || (x.PasswordSalt != null && x.PasswordSalt.Contains(search)));
    }

    private static IQueryable<Customer> ApplyFilter(IQueryable<Customer> query, string? filterField, string? filterValue)
    {
        if (string.IsNullOrWhiteSpace(filterField) || string.IsNullOrWhiteSpace(filterValue)) return query;
        filterField = filterField.Trim();
        filterValue = filterValue.Trim();
        return filterField.ToLowerInvariant() switch
        {
            "customerid" => int.TryParse(filterValue, out var CustomerIDValue) ? query.Where(x => x.CustomerID == CustomerIDValue) : query,
            "namestyle" => bool.TryParse(filterValue, out var NameStyleValue) ? query.Where(x => x.NameStyle == NameStyleValue) : query,
            "title" => query.Where(x => x.Title != null && x.Title.Contains(filterValue)),
            "firstname" => query.Where(x => x.FirstName != null && x.FirstName.Contains(filterValue)),
            "middlename" => query.Where(x => x.MiddleName != null && x.MiddleName.Contains(filterValue)),
            "lastname" => query.Where(x => x.LastName != null && x.LastName.Contains(filterValue)),
            "suffix" => query.Where(x => x.Suffix != null && x.Suffix.Contains(filterValue)),
            "companyname" => query.Where(x => x.CompanyName != null && x.CompanyName.Contains(filterValue)),
            "salesperson" => query.Where(x => x.SalesPerson != null && x.SalesPerson.Contains(filterValue)),
            "emailaddress" => query.Where(x => x.EmailAddress != null && x.EmailAddress.Contains(filterValue)),
            "phone" => query.Where(x => x.Phone != null && x.Phone.Contains(filterValue)),
            "passwordhash" => query.Where(x => x.PasswordHash != null && x.PasswordHash.Contains(filterValue)),
            "passwordsalt" => query.Where(x => x.PasswordSalt != null && x.PasswordSalt.Contains(filterValue)),
            "rowguid" => Guid.TryParse(filterValue, out var RowguidValue) ? query.Where(x => x.Rowguid == RowguidValue) : query,
            "modifieddate" => DateTime.TryParse(filterValue, out var ModifiedDateValue) ? query.Where(x => x.ModifiedDate == ModifiedDateValue) : query,
            _ => query
        };
    }

    private static IQueryable<Customer> ApplySort(IQueryable<Customer> query, string? sortBy, string? sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) || string.Equals(sortDirection, "descending", StringComparison.OrdinalIgnoreCase);
        var field = string.IsNullOrWhiteSpace(sortBy) ? "CustomerID" : sortBy.Trim();
        return field.ToLowerInvariant() switch
        {
            "customerid" => descending ? query.OrderByDescending(x => x.CustomerID) : query.OrderBy(x => x.CustomerID),
            "namestyle" => descending ? query.OrderByDescending(x => x.NameStyle) : query.OrderBy(x => x.NameStyle),
            "title" => descending ? query.OrderByDescending(x => x.Title) : query.OrderBy(x => x.Title),
            "firstname" => descending ? query.OrderByDescending(x => x.FirstName) : query.OrderBy(x => x.FirstName),
            "middlename" => descending ? query.OrderByDescending(x => x.MiddleName) : query.OrderBy(x => x.MiddleName),
            "lastname" => descending ? query.OrderByDescending(x => x.LastName) : query.OrderBy(x => x.LastName),
            "suffix" => descending ? query.OrderByDescending(x => x.Suffix) : query.OrderBy(x => x.Suffix),
            "companyname" => descending ? query.OrderByDescending(x => x.CompanyName) : query.OrderBy(x => x.CompanyName),
            "salesperson" => descending ? query.OrderByDescending(x => x.SalesPerson) : query.OrderBy(x => x.SalesPerson),
            "emailaddress" => descending ? query.OrderByDescending(x => x.EmailAddress) : query.OrderBy(x => x.EmailAddress),
            "phone" => descending ? query.OrderByDescending(x => x.Phone) : query.OrderBy(x => x.Phone),
            "passwordhash" => descending ? query.OrderByDescending(x => x.PasswordHash) : query.OrderBy(x => x.PasswordHash),
            "passwordsalt" => descending ? query.OrderByDescending(x => x.PasswordSalt) : query.OrderBy(x => x.PasswordSalt),
            "rowguid" => descending ? query.OrderByDescending(x => x.Rowguid) : query.OrderBy(x => x.Rowguid),
            "modifieddate" => descending ? query.OrderByDescending(x => x.ModifiedDate) : query.OrderBy(x => x.ModifiedDate),
            _ => descending ? query.OrderByDescending(x => x.CustomerID) : query.OrderBy(x => x.CustomerID)
        };
    }
    private static CustomerDto ToDto(Customer item) => new(
        item.CustomerID,
        item.NameStyle,
        item.Title,
        item.FirstName,
        item.MiddleName,
        item.LastName,
        item.Suffix,
        item.CompanyName,
        item.SalesPerson,
        item.EmailAddress,
        item.Phone,
        item.PasswordHash,
        item.PasswordSalt,
        item.Rowguid,
        item.ModifiedDate
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
            Resource = "Customer",
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
