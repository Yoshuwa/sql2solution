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
[Route("api/address")]
public sealed partial class AddressController : ControllerBase
{
    private readonly AppDbContext _db;

    public AddressController(AppDbContext db)
    {
        _db = db;
    }

    partial void OnBeforeCreate(CreateAddressRequest request, Address item);
    partial void OnAfterCreate(Address item);
    partial void OnBeforeUpdate(Address item, UpdateAddressRequest request);
    partial void OnBeforeDelete(Address item);

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<AddressDto>>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null, [FromQuery] string? filterField = null, [FromQuery] string? filterValue = null, [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        IQueryable<Address> query = _db.Set<Address>().AsNoTracking();
        query = ApplySearch(query, search);
        query = ApplyFilter(query, filterField, filterValue);
        query = ApplySort(query, sortBy, sortDirection);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<AddressDto>>.Success("records loaded", new PagedResult<AddressDto>(items, page, pageSize, total)));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<AddressDto>>> GetById(int id, CancellationToken ct)
    {
        IQueryable<Address> query = _db.Set<Address>().AsNoTracking();
        var item = await query.FirstOrDefaultAsync(x => x.AddressID!.Equals(id), ct);
        return item is null ? NotFound(ApiResponse<object>.Warning("record not found")) : Ok(ApiResponse<AddressDto>.Success("record loaded", ToDto(item)));
    }

    [HttpGet("{id}/history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuditTrailDto>>>> GetHistory(int id, CancellationToken ct)
    {
        var canReadRecord = await _db.Set<Address>().AsNoTracking().AnyAsync(x => x.AddressID!.Equals(id), ct);
        if (!canReadRecord) return NotFound(ApiResponse<object>.Warning("record not found"));
        await EnsureAuditTrailTableAsync(ct);
        var resourceKey = Convert.ToString(id) ?? string.Empty;
        var history = await _db.AuditTrailEntries
            .AsNoTracking()
            .Where(entry => entry.Resource == "Address" && entry.ResourceKey == resourceKey)
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .Take(100)
            .Select(entry => ToAuditTrailDto(entry))
            .ToListAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<AuditTrailDto>>.Success("activity loaded", history));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<AddressDto>>> Create(CreateAddressRequest request, CancellationToken ct)
    {
        var item = new Address
        {
            AddressLine1 = request.AddressLine1,
            AddressLine2 = request.AddressLine2,
            City = request.City,
            StateProvince = request.StateProvince,
            CountryRegion = request.CountryRegion,
            PostalCode = request.PostalCode,
            Rowguid = request.Rowguid,
            ModifiedDate = request.ModifiedDate,
        };
        OnBeforeCreate(request, item);
        _db.Set<Address>().Add(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Created", Convert.ToString(item.AddressID) ?? string.Empty, $"Created Address record {item.AddressID}.", ToDto(item), ct);
        OnAfterCreate(item);
        return CreatedAtAction(nameof(GetById), new { id = item.AddressID }, ApiResponse<AddressDto>.Success("record created", ToDto(item)));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateAddressRequest request, CancellationToken ct)
    {
        var item = await _db.Set<Address>().FirstOrDefaultAsync(x => x.AddressID!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeUpdate(item, request);
        item.AddressLine1 = request.AddressLine1;
        item.AddressLine2 = request.AddressLine2;
        item.City = request.City;
        item.StateProvince = request.StateProvince;
        item.CountryRegion = request.CountryRegion;
        item.PostalCode = request.PostalCode;
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
        await LogAuditTrailAsync("Updated", Convert.ToString(item.AddressID) ?? string.Empty, $"Updated Address record {item.AddressID}.", auditChanges, ct);
        return Ok(ApiResponse<object>.Success("record updated", new { updated = 1 }));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch(int id, UpdateAddressRequest request, CancellationToken ct)
    {
        return await Update(id, request, ct);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var item = await _db.Set<Address>().FirstOrDefaultAsync(x => x.AddressID!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeDelete(item);
        _db.Set<Address>().Remove(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Deleted", Convert.ToString(id) ?? string.Empty, $"Hard deleted Address record {id}.", ToDto(item), ct);
        return Ok(ApiResponse<object>.Success("record deleted", new { deleted = 1, mode = "Hard" }));
    }

    [HttpPost("bulk/export")]
    public async Task<ActionResult<ApiResponse<PagedResult<AddressDto>>>> ExportBulk(BulkIdsRequest request, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return Ok(ApiResponse<PagedResult<AddressDto>>.Warning("no records selected", new PagedResult<AddressDto>(Array.Empty<AddressDto>(), page, pageSize, 0)));
        IQueryable<Address> query = _db.Set<Address>().AsNoTracking().Where(x => ids.Contains(x.AddressID));
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<AddressDto>>.Success("records exported", new PagedResult<AddressDto>(items, page, pageSize, total)));
    }

    [HttpPatch("bulk")]
    public async Task<IActionResult> UpdateBulk(BulkUpdateRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        if (string.IsNullOrWhiteSpace(request.Field)) return BadRequest(ApiResponse<object>.Error("error", new { error = "Choose a field to update." }));
        IQueryable<Address> query = _db.Set<Address>().Where(x => ids.Contains(x.AddressID));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return NotFound(ApiResponse<object>.Warning("records not found"));
        if (!ApplyBulkUpdate(items, request, out var error)) return BadRequest(ApiResponse<object>.Error("error", new { error }));
        var auditChanges = items.ToDictionary(item => Convert.ToString(item.AddressID) ?? string.Empty, item => GetEntityChanges(_db.Entry(item)));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Updated", Convert.ToString(item.AddressID) ?? string.Empty, $"Updated Address record {item.AddressID} in bulk update.", auditChanges[Convert.ToString(item.AddressID) ?? string.Empty], ct);
        return Ok(ApiResponse<object>.Success("records updated", new { updated = items.Count }));
    }

    [HttpPost("bulk/delete")]
    public async Task<IActionResult> DeleteBulk(BulkIdsRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        IQueryable<Address> query = _db.Set<Address>().Where(x => ids.Contains(x.AddressID));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return Ok(ApiResponse<object>.Warning("records not found", new { deleted = 0 }));
        foreach (var item in items)
        {
            OnBeforeDelete(item);
        }
        _db.Set<Address>().RemoveRange(items);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Deleted", Convert.ToString(item.AddressID) ?? string.Empty, $"Hard deleted Address record {item.AddressID} in bulk delete.", ToDto(item), ct);
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

    private static bool ApplyBulkUpdate(IReadOnlyList<Address> items, BulkUpdateRequest request, out string error)
    {
        error = string.Empty;
        return request.Field.Trim().ToLowerInvariant() switch
        {
            "addressline1" => ApplyBulkAddressLine1(items, request.Value, out error),
            "addressline2" => ApplyBulkAddressLine2(items, request.Value, out error),
            "city" => ApplyBulkCity(items, request.Value, out error),
            "stateprovince" => ApplyBulkStateProvince(items, request.Value, out error),
            "countryregion" => ApplyBulkCountryRegion(items, request.Value, out error),
            "postalcode" => ApplyBulkPostalCode(items, request.Value, out error),
            "rowguid" => ApplyBulkRowguid(items, request.Value, out error),
            "modifieddate" => ApplyBulkModifiedDate(items, request.Value, out error),
            _ => FailBulkUpdate("Field is not bulk editable.", out error)
        };
    }

    private static bool ApplyBulkAddressLine1(IReadOnlyList<Address> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.AddressLine1 = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkAddressLine2(IReadOnlyList<Address> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.AddressLine2 = null;
            return true;
        }
        foreach (var item in items) item.AddressLine2 = raw;
        return true;
    }

    private static bool ApplyBulkCity(IReadOnlyList<Address> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.City = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkStateProvince(IReadOnlyList<Address> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.StateProvince = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkCountryRegion(IReadOnlyList<Address> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.CountryRegion = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkPostalCode(IReadOnlyList<Address> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.PostalCode = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkRowguid(IReadOnlyList<Address> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!Guid.TryParse(raw, out var value)) return FailBulkUpdate("Rowguid requires a Guid value.", out error);
        foreach (var item in items) item.Rowguid = value;
        return true;
    }

    private static bool ApplyBulkModifiedDate(IReadOnlyList<Address> items, string? raw, out string error)
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


    private static IQueryable<Address> ApplySearch(IQueryable<Address> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        search = search.Trim();
        return query.Where(x => (x.AddressLine1 != null && x.AddressLine1.Contains(search)) || (x.AddressLine2 != null && x.AddressLine2.Contains(search)) || (x.City != null && x.City.Contains(search)) || (x.StateProvince != null && x.StateProvince.Contains(search)) || (x.CountryRegion != null && x.CountryRegion.Contains(search)) || (x.PostalCode != null && x.PostalCode.Contains(search)));
    }

    private static IQueryable<Address> ApplyFilter(IQueryable<Address> query, string? filterField, string? filterValue)
    {
        if (string.IsNullOrWhiteSpace(filterField) || string.IsNullOrWhiteSpace(filterValue)) return query;
        filterField = filterField.Trim();
        filterValue = filterValue.Trim();
        return filterField.ToLowerInvariant() switch
        {
            "addressid" => int.TryParse(filterValue, out var AddressIDValue) ? query.Where(x => x.AddressID == AddressIDValue) : query,
            "addressline1" => query.Where(x => x.AddressLine1 != null && x.AddressLine1.Contains(filterValue)),
            "addressline2" => query.Where(x => x.AddressLine2 != null && x.AddressLine2.Contains(filterValue)),
            "city" => query.Where(x => x.City != null && x.City.Contains(filterValue)),
            "stateprovince" => query.Where(x => x.StateProvince != null && x.StateProvince.Contains(filterValue)),
            "countryregion" => query.Where(x => x.CountryRegion != null && x.CountryRegion.Contains(filterValue)),
            "postalcode" => query.Where(x => x.PostalCode != null && x.PostalCode.Contains(filterValue)),
            "rowguid" => Guid.TryParse(filterValue, out var RowguidValue) ? query.Where(x => x.Rowguid == RowguidValue) : query,
            "modifieddate" => DateTime.TryParse(filterValue, out var ModifiedDateValue) ? query.Where(x => x.ModifiedDate == ModifiedDateValue) : query,
            _ => query
        };
    }

    private static IQueryable<Address> ApplySort(IQueryable<Address> query, string? sortBy, string? sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) || string.Equals(sortDirection, "descending", StringComparison.OrdinalIgnoreCase);
        var field = string.IsNullOrWhiteSpace(sortBy) ? "AddressID" : sortBy.Trim();
        return field.ToLowerInvariant() switch
        {
            "addressid" => descending ? query.OrderByDescending(x => x.AddressID) : query.OrderBy(x => x.AddressID),
            "addressline1" => descending ? query.OrderByDescending(x => x.AddressLine1) : query.OrderBy(x => x.AddressLine1),
            "addressline2" => descending ? query.OrderByDescending(x => x.AddressLine2) : query.OrderBy(x => x.AddressLine2),
            "city" => descending ? query.OrderByDescending(x => x.City) : query.OrderBy(x => x.City),
            "stateprovince" => descending ? query.OrderByDescending(x => x.StateProvince) : query.OrderBy(x => x.StateProvince),
            "countryregion" => descending ? query.OrderByDescending(x => x.CountryRegion) : query.OrderBy(x => x.CountryRegion),
            "postalcode" => descending ? query.OrderByDescending(x => x.PostalCode) : query.OrderBy(x => x.PostalCode),
            "rowguid" => descending ? query.OrderByDescending(x => x.Rowguid) : query.OrderBy(x => x.Rowguid),
            "modifieddate" => descending ? query.OrderByDescending(x => x.ModifiedDate) : query.OrderBy(x => x.ModifiedDate),
            _ => descending ? query.OrderByDescending(x => x.AddressID) : query.OrderBy(x => x.AddressID)
        };
    }
    private static AddressDto ToDto(Address item) => new(
        item.AddressID,
        item.AddressLine1,
        item.AddressLine2,
        item.City,
        item.StateProvince,
        item.CountryRegion,
        item.PostalCode,
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
            Resource = "Address",
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
