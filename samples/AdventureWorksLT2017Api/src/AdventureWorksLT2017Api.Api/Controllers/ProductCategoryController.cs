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
[Route("api/productCategories")]
public sealed partial class ProductCategoryController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProductCategoryController(AppDbContext db)
    {
        _db = db;
    }

    partial void OnBeforeCreate(CreateProductCategoryRequest request, ProductCategory item);
    partial void OnAfterCreate(ProductCategory item);
    partial void OnBeforeUpdate(ProductCategory item, UpdateProductCategoryRequest request);
    partial void OnBeforeDelete(ProductCategory item);

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<ProductCategoryDto>>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null, [FromQuery] string? filterField = null, [FromQuery] string? filterValue = null, [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        IQueryable<ProductCategory> query = _db.Set<ProductCategory>().AsNoTracking();
        query = ApplySearch(query, search);
        query = ApplyFilter(query, filterField, filterValue);
        query = ApplySort(query, sortBy, sortDirection);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<ProductCategoryDto>>.Success("records loaded", new PagedResult<ProductCategoryDto>(items, page, pageSize, total)));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<ProductCategoryDto>>> GetById(int id, CancellationToken ct)
    {
        IQueryable<ProductCategory> query = _db.Set<ProductCategory>().AsNoTracking();
        var item = await query.FirstOrDefaultAsync(x => x.ProductCategoryID!.Equals(id), ct);
        return item is null ? NotFound(ApiResponse<object>.Warning("record not found")) : Ok(ApiResponse<ProductCategoryDto>.Success("record loaded", ToDto(item)));
    }

    [HttpGet("{id}/history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuditTrailDto>>>> GetHistory(int id, CancellationToken ct)
    {
        var canReadRecord = await _db.Set<ProductCategory>().AsNoTracking().AnyAsync(x => x.ProductCategoryID!.Equals(id), ct);
        if (!canReadRecord) return NotFound(ApiResponse<object>.Warning("record not found"));
        await EnsureAuditTrailTableAsync(ct);
        var resourceKey = Convert.ToString(id) ?? string.Empty;
        var history = await _db.AuditTrailEntries
            .AsNoTracking()
            .Where(entry => entry.Resource == "ProductCategory" && entry.ResourceKey == resourceKey)
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .Take(100)
            .Select(entry => ToAuditTrailDto(entry))
            .ToListAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<AuditTrailDto>>.Success("activity loaded", history));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ProductCategoryDto>>> Create(CreateProductCategoryRequest request, CancellationToken ct)
    {
        var item = new ProductCategory
        {
            ParentProductCategoryID = request.ParentProductCategoryID,
            Name = request.Name,
            Rowguid = request.Rowguid,
            ModifiedDate = request.ModifiedDate,
        };
        OnBeforeCreate(request, item);
        _db.Set<ProductCategory>().Add(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Created", Convert.ToString(item.ProductCategoryID) ?? string.Empty, $"Created ProductCategory record {item.ProductCategoryID}.", ToDto(item), ct);
        OnAfterCreate(item);
        return CreatedAtAction(nameof(GetById), new { id = item.ProductCategoryID }, ApiResponse<ProductCategoryDto>.Success("record created", ToDto(item)));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateProductCategoryRequest request, CancellationToken ct)
    {
        var item = await _db.Set<ProductCategory>().FirstOrDefaultAsync(x => x.ProductCategoryID!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeUpdate(item, request);
        item.ParentProductCategoryID = request.ParentProductCategoryID;
        item.Name = request.Name;
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
        await LogAuditTrailAsync("Updated", Convert.ToString(item.ProductCategoryID) ?? string.Empty, $"Updated ProductCategory record {item.ProductCategoryID}.", auditChanges, ct);
        return Ok(ApiResponse<object>.Success("record updated", new { updated = 1 }));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch(int id, UpdateProductCategoryRequest request, CancellationToken ct)
    {
        return await Update(id, request, ct);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var item = await _db.Set<ProductCategory>().FirstOrDefaultAsync(x => x.ProductCategoryID!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeDelete(item);
        _db.Set<ProductCategory>().Remove(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Deleted", Convert.ToString(id) ?? string.Empty, $"Hard deleted ProductCategory record {id}.", ToDto(item), ct);
        return Ok(ApiResponse<object>.Success("record deleted", new { deleted = 1, mode = "Hard" }));
    }

    [HttpPost("bulk/export")]
    public async Task<ActionResult<ApiResponse<PagedResult<ProductCategoryDto>>>> ExportBulk(BulkIdsRequest request, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return Ok(ApiResponse<PagedResult<ProductCategoryDto>>.Warning("no records selected", new PagedResult<ProductCategoryDto>(Array.Empty<ProductCategoryDto>(), page, pageSize, 0)));
        IQueryable<ProductCategory> query = _db.Set<ProductCategory>().AsNoTracking().Where(x => ids.Contains(x.ProductCategoryID));
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<ProductCategoryDto>>.Success("records exported", new PagedResult<ProductCategoryDto>(items, page, pageSize, total)));
    }

    [HttpPatch("bulk")]
    public async Task<IActionResult> UpdateBulk(BulkUpdateRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        if (string.IsNullOrWhiteSpace(request.Field)) return BadRequest(ApiResponse<object>.Error("error", new { error = "Choose a field to update." }));
        IQueryable<ProductCategory> query = _db.Set<ProductCategory>().Where(x => ids.Contains(x.ProductCategoryID));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return NotFound(ApiResponse<object>.Warning("records not found"));
        if (!ApplyBulkUpdate(items, request, out var error)) return BadRequest(ApiResponse<object>.Error("error", new { error }));
        var auditChanges = items.ToDictionary(item => Convert.ToString(item.ProductCategoryID) ?? string.Empty, item => GetEntityChanges(_db.Entry(item)));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Updated", Convert.ToString(item.ProductCategoryID) ?? string.Empty, $"Updated ProductCategory record {item.ProductCategoryID} in bulk update.", auditChanges[Convert.ToString(item.ProductCategoryID) ?? string.Empty], ct);
        return Ok(ApiResponse<object>.Success("records updated", new { updated = items.Count }));
    }

    [HttpPost("bulk/delete")]
    public async Task<IActionResult> DeleteBulk(BulkIdsRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        IQueryable<ProductCategory> query = _db.Set<ProductCategory>().Where(x => ids.Contains(x.ProductCategoryID));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return Ok(ApiResponse<object>.Warning("records not found", new { deleted = 0 }));
        foreach (var item in items)
        {
            OnBeforeDelete(item);
        }
        _db.Set<ProductCategory>().RemoveRange(items);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Deleted", Convert.ToString(item.ProductCategoryID) ?? string.Empty, $"Hard deleted ProductCategory record {item.ProductCategoryID} in bulk delete.", ToDto(item), ct);
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

    private static bool ApplyBulkUpdate(IReadOnlyList<ProductCategory> items, BulkUpdateRequest request, out string error)
    {
        error = string.Empty;
        return request.Field.Trim().ToLowerInvariant() switch
        {
            "parentproductcategoryid" => ApplyBulkParentProductCategoryID(items, request.Value, out error),
            "name" => ApplyBulkName(items, request.Value, out error),
            "rowguid" => ApplyBulkRowguid(items, request.Value, out error),
            "modifieddate" => ApplyBulkModifiedDate(items, request.Value, out error),
            _ => FailBulkUpdate("Field is not bulk editable.", out error)
        };
    }

    private static bool ApplyBulkParentProductCategoryID(IReadOnlyList<ProductCategory> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.ParentProductCategoryID = null;
            return true;
        }
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("ParentProductCategoryID requires a int value.", out error);
        foreach (var item in items) item.ParentProductCategoryID = value;
        return true;
    }

    private static bool ApplyBulkName(IReadOnlyList<ProductCategory> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.Name = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkRowguid(IReadOnlyList<ProductCategory> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!Guid.TryParse(raw, out var value)) return FailBulkUpdate("Rowguid requires a Guid value.", out error);
        foreach (var item in items) item.Rowguid = value;
        return true;
    }

    private static bool ApplyBulkModifiedDate(IReadOnlyList<ProductCategory> items, string? raw, out string error)
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


    private static IQueryable<ProductCategory> ApplySearch(IQueryable<ProductCategory> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        search = search.Trim();
        return query.Where(x => (x.Name != null && x.Name.Contains(search)));
    }

    private static IQueryable<ProductCategory> ApplyFilter(IQueryable<ProductCategory> query, string? filterField, string? filterValue)
    {
        if (string.IsNullOrWhiteSpace(filterField) || string.IsNullOrWhiteSpace(filterValue)) return query;
        filterField = filterField.Trim();
        filterValue = filterValue.Trim();
        return filterField.ToLowerInvariant() switch
        {
            "productcategoryid" => int.TryParse(filterValue, out var ProductCategoryIDValue) ? query.Where(x => x.ProductCategoryID == ProductCategoryIDValue) : query,
            "parentproductcategoryid" => int.TryParse(filterValue, out var ParentProductCategoryIDValue) ? query.Where(x => x.ParentProductCategoryID == ParentProductCategoryIDValue) : query,
            "name" => query.Where(x => x.Name != null && x.Name.Contains(filterValue)),
            "rowguid" => Guid.TryParse(filterValue, out var RowguidValue) ? query.Where(x => x.Rowguid == RowguidValue) : query,
            "modifieddate" => DateTime.TryParse(filterValue, out var ModifiedDateValue) ? query.Where(x => x.ModifiedDate == ModifiedDateValue) : query,
            _ => query
        };
    }

    private static IQueryable<ProductCategory> ApplySort(IQueryable<ProductCategory> query, string? sortBy, string? sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) || string.Equals(sortDirection, "descending", StringComparison.OrdinalIgnoreCase);
        var field = string.IsNullOrWhiteSpace(sortBy) ? "ProductCategoryID" : sortBy.Trim();
        return field.ToLowerInvariant() switch
        {
            "productcategoryid" => descending ? query.OrderByDescending(x => x.ProductCategoryID) : query.OrderBy(x => x.ProductCategoryID),
            "parentproductcategoryid" => descending ? query.OrderByDescending(x => x.ParentProductCategoryID) : query.OrderBy(x => x.ParentProductCategoryID),
            "name" => descending ? query.OrderByDescending(x => x.Name) : query.OrderBy(x => x.Name),
            "rowguid" => descending ? query.OrderByDescending(x => x.Rowguid) : query.OrderBy(x => x.Rowguid),
            "modifieddate" => descending ? query.OrderByDescending(x => x.ModifiedDate) : query.OrderBy(x => x.ModifiedDate),
            _ => descending ? query.OrderByDescending(x => x.ProductCategoryID) : query.OrderBy(x => x.ProductCategoryID)
        };
    }
    private static ProductCategoryDto ToDto(ProductCategory item) => new(
        item.ProductCategoryID,
        item.ParentProductCategoryID,
        item.Name,
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
            Resource = "ProductCategory",
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
