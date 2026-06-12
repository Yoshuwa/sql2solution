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
[Route("api/products")]
public sealed partial class ProductController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProductController(AppDbContext db)
    {
        _db = db;
    }

    partial void OnBeforeCreate(CreateProductRequest request, Product item);
    partial void OnAfterCreate(Product item);
    partial void OnBeforeUpdate(Product item, UpdateProductRequest request);
    partial void OnBeforeDelete(Product item);

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<ProductDto>>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null, [FromQuery] string? filterField = null, [FromQuery] string? filterValue = null, [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        IQueryable<Product> query = _db.Set<Product>().AsNoTracking();
        query = ApplySearch(query, search);
        query = ApplyFilter(query, filterField, filterValue);
        query = ApplySort(query, sortBy, sortDirection);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<ProductDto>>.Success("records loaded", new PagedResult<ProductDto>(items, page, pageSize, total)));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<ProductDto>>> GetById(int id, CancellationToken ct)
    {
        IQueryable<Product> query = _db.Set<Product>().AsNoTracking();
        var item = await query.FirstOrDefaultAsync(x => x.ProductID!.Equals(id), ct);
        return item is null ? NotFound(ApiResponse<object>.Warning("record not found")) : Ok(ApiResponse<ProductDto>.Success("record loaded", ToDto(item)));
    }

    [HttpGet("{id}/history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuditTrailDto>>>> GetHistory(int id, CancellationToken ct)
    {
        var canReadRecord = await _db.Set<Product>().AsNoTracking().AnyAsync(x => x.ProductID!.Equals(id), ct);
        if (!canReadRecord) return NotFound(ApiResponse<object>.Warning("record not found"));
        await EnsureAuditTrailTableAsync(ct);
        var resourceKey = Convert.ToString(id) ?? string.Empty;
        var history = await _db.AuditTrailEntries
            .AsNoTracking()
            .Where(entry => entry.Resource == "Product" && entry.ResourceKey == resourceKey)
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .Take(100)
            .Select(entry => ToAuditTrailDto(entry))
            .ToListAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<AuditTrailDto>>.Success("activity loaded", history));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ProductDto>>> Create(CreateProductRequest request, CancellationToken ct)
    {
        var item = new Product
        {
            Name = request.Name,
            ProductNumber = request.ProductNumber,
            Color = request.Color,
            StandardCost = request.StandardCost,
            ListPrice = request.ListPrice,
            Size = request.Size,
            Weight = request.Weight,
            ProductCategoryID = request.ProductCategoryID,
            ProductModelID = request.ProductModelID,
            SellStartDate = request.SellStartDate,
            SellEndDate = request.SellEndDate,
            DiscontinuedDate = request.DiscontinuedDate,
            ThumbNailPhoto = request.ThumbNailPhoto,
            ThumbnailPhotoFileName = request.ThumbnailPhotoFileName,
            Rowguid = request.Rowguid,
            ModifiedDate = request.ModifiedDate,
        };
        OnBeforeCreate(request, item);
        _db.Set<Product>().Add(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Created", Convert.ToString(item.ProductID) ?? string.Empty, $"Created Product record {item.ProductID}.", ToDto(item), ct);
        OnAfterCreate(item);
        return CreatedAtAction(nameof(GetById), new { id = item.ProductID }, ApiResponse<ProductDto>.Success("record created", ToDto(item)));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateProductRequest request, CancellationToken ct)
    {
        var item = await _db.Set<Product>().FirstOrDefaultAsync(x => x.ProductID!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeUpdate(item, request);
        item.Name = request.Name;
        item.ProductNumber = request.ProductNumber;
        item.Color = request.Color;
        item.StandardCost = request.StandardCost;
        item.ListPrice = request.ListPrice;
        item.Size = request.Size;
        item.Weight = request.Weight;
        item.ProductCategoryID = request.ProductCategoryID;
        item.ProductModelID = request.ProductModelID;
        item.SellStartDate = request.SellStartDate;
        item.SellEndDate = request.SellEndDate;
        item.DiscontinuedDate = request.DiscontinuedDate;
        item.ThumbNailPhoto = request.ThumbNailPhoto;
        item.ThumbnailPhotoFileName = request.ThumbnailPhotoFileName;
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
        await LogAuditTrailAsync("Updated", Convert.ToString(item.ProductID) ?? string.Empty, $"Updated Product record {item.ProductID}.", auditChanges, ct);
        return Ok(ApiResponse<object>.Success("record updated", new { updated = 1 }));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch(int id, UpdateProductRequest request, CancellationToken ct)
    {
        return await Update(id, request, ct);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var item = await _db.Set<Product>().FirstOrDefaultAsync(x => x.ProductID!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeDelete(item);
        _db.Set<Product>().Remove(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Deleted", Convert.ToString(id) ?? string.Empty, $"Hard deleted Product record {id}.", ToDto(item), ct);
        return Ok(ApiResponse<object>.Success("record deleted", new { deleted = 1, mode = "Hard" }));
    }

    [HttpPost("bulk/export")]
    public async Task<ActionResult<ApiResponse<PagedResult<ProductDto>>>> ExportBulk(BulkIdsRequest request, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return Ok(ApiResponse<PagedResult<ProductDto>>.Warning("no records selected", new PagedResult<ProductDto>(Array.Empty<ProductDto>(), page, pageSize, 0)));
        IQueryable<Product> query = _db.Set<Product>().AsNoTracking().Where(x => ids.Contains(x.ProductID));
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<ProductDto>>.Success("records exported", new PagedResult<ProductDto>(items, page, pageSize, total)));
    }

    [HttpPatch("bulk")]
    public async Task<IActionResult> UpdateBulk(BulkUpdateRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        if (string.IsNullOrWhiteSpace(request.Field)) return BadRequest(ApiResponse<object>.Error("error", new { error = "Choose a field to update." }));
        IQueryable<Product> query = _db.Set<Product>().Where(x => ids.Contains(x.ProductID));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return NotFound(ApiResponse<object>.Warning("records not found"));
        if (!ApplyBulkUpdate(items, request, out var error)) return BadRequest(ApiResponse<object>.Error("error", new { error }));
        var auditChanges = items.ToDictionary(item => Convert.ToString(item.ProductID) ?? string.Empty, item => GetEntityChanges(_db.Entry(item)));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Updated", Convert.ToString(item.ProductID) ?? string.Empty, $"Updated Product record {item.ProductID} in bulk update.", auditChanges[Convert.ToString(item.ProductID) ?? string.Empty], ct);
        return Ok(ApiResponse<object>.Success("records updated", new { updated = items.Count }));
    }

    [HttpPost("bulk/delete")]
    public async Task<IActionResult> DeleteBulk(BulkIdsRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        IQueryable<Product> query = _db.Set<Product>().Where(x => ids.Contains(x.ProductID));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return Ok(ApiResponse<object>.Warning("records not found", new { deleted = 0 }));
        foreach (var item in items)
        {
            OnBeforeDelete(item);
        }
        _db.Set<Product>().RemoveRange(items);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Deleted", Convert.ToString(item.ProductID) ?? string.Empty, $"Hard deleted Product record {item.ProductID} in bulk delete.", ToDto(item), ct);
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

    private static bool ApplyBulkUpdate(IReadOnlyList<Product> items, BulkUpdateRequest request, out string error)
    {
        error = string.Empty;
        return request.Field.Trim().ToLowerInvariant() switch
        {
            "name" => ApplyBulkName(items, request.Value, out error),
            "productnumber" => ApplyBulkProductNumber(items, request.Value, out error),
            "color" => ApplyBulkColor(items, request.Value, out error),
            "standardcost" => ApplyBulkStandardCost(items, request.Value, out error),
            "listprice" => ApplyBulkListPrice(items, request.Value, out error),
            "size" => ApplyBulkSize(items, request.Value, out error),
            "weight" => ApplyBulkWeight(items, request.Value, out error),
            "productcategoryid" => ApplyBulkProductCategoryID(items, request.Value, out error),
            "productmodelid" => ApplyBulkProductModelID(items, request.Value, out error),
            "sellstartdate" => ApplyBulkSellStartDate(items, request.Value, out error),
            "sellenddate" => ApplyBulkSellEndDate(items, request.Value, out error),
            "discontinueddate" => ApplyBulkDiscontinuedDate(items, request.Value, out error),
            "thumbnailphotofilename" => ApplyBulkThumbnailPhotoFileName(items, request.Value, out error),
            "rowguid" => ApplyBulkRowguid(items, request.Value, out error),
            "modifieddate" => ApplyBulkModifiedDate(items, request.Value, out error),
            _ => FailBulkUpdate("Field is not bulk editable.", out error)
        };
    }

    private static bool ApplyBulkName(IReadOnlyList<Product> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.Name = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkProductNumber(IReadOnlyList<Product> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.ProductNumber = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkColor(IReadOnlyList<Product> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.Color = null;
            return true;
        }
        foreach (var item in items) item.Color = raw;
        return true;
    }

    private static bool ApplyBulkStandardCost(IReadOnlyList<Product> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("StandardCost requires a decimal value.", out error);
        foreach (var item in items) item.StandardCost = value;
        return true;
    }

    private static bool ApplyBulkListPrice(IReadOnlyList<Product> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("ListPrice requires a decimal value.", out error);
        foreach (var item in items) item.ListPrice = value;
        return true;
    }

    private static bool ApplyBulkSize(IReadOnlyList<Product> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.Size = null;
            return true;
        }
        foreach (var item in items) item.Size = raw;
        return true;
    }

    private static bool ApplyBulkWeight(IReadOnlyList<Product> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.Weight = null;
            return true;
        }
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("Weight requires a decimal value.", out error);
        foreach (var item in items) item.Weight = value;
        return true;
    }

    private static bool ApplyBulkProductCategoryID(IReadOnlyList<Product> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.ProductCategoryID = null;
            return true;
        }
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("ProductCategoryID requires a int value.", out error);
        foreach (var item in items) item.ProductCategoryID = value;
        return true;
    }

    private static bool ApplyBulkProductModelID(IReadOnlyList<Product> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.ProductModelID = null;
            return true;
        }
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("ProductModelID requires a int value.", out error);
        foreach (var item in items) item.ProductModelID = value;
        return true;
    }

    private static bool ApplyBulkSellStartDate(IReadOnlyList<Product> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!DateTime.TryParse(raw, out var value)) return FailBulkUpdate("SellStartDate requires a DateTime value.", out error);
        foreach (var item in items) item.SellStartDate = value;
        return true;
    }

    private static bool ApplyBulkSellEndDate(IReadOnlyList<Product> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.SellEndDate = null;
            return true;
        }
        if (!DateTime.TryParse(raw, out var value)) return FailBulkUpdate("SellEndDate requires a DateTime value.", out error);
        foreach (var item in items) item.SellEndDate = value;
        return true;
    }

    private static bool ApplyBulkDiscontinuedDate(IReadOnlyList<Product> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.DiscontinuedDate = null;
            return true;
        }
        if (!DateTime.TryParse(raw, out var value)) return FailBulkUpdate("DiscontinuedDate requires a DateTime value.", out error);
        foreach (var item in items) item.DiscontinuedDate = value;
        return true;
    }

    private static bool ApplyBulkThumbnailPhotoFileName(IReadOnlyList<Product> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.ThumbnailPhotoFileName = null;
            return true;
        }
        foreach (var item in items) item.ThumbnailPhotoFileName = raw;
        return true;
    }

    private static bool ApplyBulkRowguid(IReadOnlyList<Product> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!Guid.TryParse(raw, out var value)) return FailBulkUpdate("Rowguid requires a Guid value.", out error);
        foreach (var item in items) item.Rowguid = value;
        return true;
    }

    private static bool ApplyBulkModifiedDate(IReadOnlyList<Product> items, string? raw, out string error)
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


    private static IQueryable<Product> ApplySearch(IQueryable<Product> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        search = search.Trim();
        return query.Where(x => (x.Name != null && x.Name.Contains(search)) || (x.ProductNumber != null && x.ProductNumber.Contains(search)) || (x.Color != null && x.Color.Contains(search)) || (x.Size != null && x.Size.Contains(search)) || (x.ThumbnailPhotoFileName != null && x.ThumbnailPhotoFileName.Contains(search)));
    }

    private static IQueryable<Product> ApplyFilter(IQueryable<Product> query, string? filterField, string? filterValue)
    {
        if (string.IsNullOrWhiteSpace(filterField) || string.IsNullOrWhiteSpace(filterValue)) return query;
        filterField = filterField.Trim();
        filterValue = filterValue.Trim();
        return filterField.ToLowerInvariant() switch
        {
            "productid" => int.TryParse(filterValue, out var ProductIDValue) ? query.Where(x => x.ProductID == ProductIDValue) : query,
            "name" => query.Where(x => x.Name != null && x.Name.Contains(filterValue)),
            "productnumber" => query.Where(x => x.ProductNumber != null && x.ProductNumber.Contains(filterValue)),
            "color" => query.Where(x => x.Color != null && x.Color.Contains(filterValue)),
            "standardcost" => decimal.TryParse(filterValue, out var StandardCostValue) ? query.Where(x => x.StandardCost == StandardCostValue) : query,
            "listprice" => decimal.TryParse(filterValue, out var ListPriceValue) ? query.Where(x => x.ListPrice == ListPriceValue) : query,
            "size" => query.Where(x => x.Size != null && x.Size.Contains(filterValue)),
            "weight" => decimal.TryParse(filterValue, out var WeightValue) ? query.Where(x => x.Weight == WeightValue) : query,
            "productcategoryid" => int.TryParse(filterValue, out var ProductCategoryIDValue) ? query.Where(x => x.ProductCategoryID == ProductCategoryIDValue) : query,
            "productmodelid" => int.TryParse(filterValue, out var ProductModelIDValue) ? query.Where(x => x.ProductModelID == ProductModelIDValue) : query,
            "sellstartdate" => DateTime.TryParse(filterValue, out var SellStartDateValue) ? query.Where(x => x.SellStartDate == SellStartDateValue) : query,
            "sellenddate" => DateTime.TryParse(filterValue, out var SellEndDateValue) ? query.Where(x => x.SellEndDate == SellEndDateValue) : query,
            "discontinueddate" => DateTime.TryParse(filterValue, out var DiscontinuedDateValue) ? query.Where(x => x.DiscontinuedDate == DiscontinuedDateValue) : query,
            "thumbnailphoto" => query,
            "thumbnailphotofilename" => query.Where(x => x.ThumbnailPhotoFileName != null && x.ThumbnailPhotoFileName.Contains(filterValue)),
            "rowguid" => Guid.TryParse(filterValue, out var RowguidValue) ? query.Where(x => x.Rowguid == RowguidValue) : query,
            "modifieddate" => DateTime.TryParse(filterValue, out var ModifiedDateValue) ? query.Where(x => x.ModifiedDate == ModifiedDateValue) : query,
            _ => query
        };
    }

    private static IQueryable<Product> ApplySort(IQueryable<Product> query, string? sortBy, string? sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) || string.Equals(sortDirection, "descending", StringComparison.OrdinalIgnoreCase);
        var field = string.IsNullOrWhiteSpace(sortBy) ? "ProductID" : sortBy.Trim();
        return field.ToLowerInvariant() switch
        {
            "productid" => descending ? query.OrderByDescending(x => x.ProductID) : query.OrderBy(x => x.ProductID),
            "name" => descending ? query.OrderByDescending(x => x.Name) : query.OrderBy(x => x.Name),
            "productnumber" => descending ? query.OrderByDescending(x => x.ProductNumber) : query.OrderBy(x => x.ProductNumber),
            "color" => descending ? query.OrderByDescending(x => x.Color) : query.OrderBy(x => x.Color),
            "standardcost" => descending ? query.OrderByDescending(x => x.StandardCost) : query.OrderBy(x => x.StandardCost),
            "listprice" => descending ? query.OrderByDescending(x => x.ListPrice) : query.OrderBy(x => x.ListPrice),
            "size" => descending ? query.OrderByDescending(x => x.Size) : query.OrderBy(x => x.Size),
            "weight" => descending ? query.OrderByDescending(x => x.Weight) : query.OrderBy(x => x.Weight),
            "productcategoryid" => descending ? query.OrderByDescending(x => x.ProductCategoryID) : query.OrderBy(x => x.ProductCategoryID),
            "productmodelid" => descending ? query.OrderByDescending(x => x.ProductModelID) : query.OrderBy(x => x.ProductModelID),
            "sellstartdate" => descending ? query.OrderByDescending(x => x.SellStartDate) : query.OrderBy(x => x.SellStartDate),
            "sellenddate" => descending ? query.OrderByDescending(x => x.SellEndDate) : query.OrderBy(x => x.SellEndDate),
            "discontinueddate" => descending ? query.OrderByDescending(x => x.DiscontinuedDate) : query.OrderBy(x => x.DiscontinuedDate),
            "thumbnailphoto" => descending ? query.OrderByDescending(x => x.ThumbNailPhoto) : query.OrderBy(x => x.ThumbNailPhoto),
            "thumbnailphotofilename" => descending ? query.OrderByDescending(x => x.ThumbnailPhotoFileName) : query.OrderBy(x => x.ThumbnailPhotoFileName),
            "rowguid" => descending ? query.OrderByDescending(x => x.Rowguid) : query.OrderBy(x => x.Rowguid),
            "modifieddate" => descending ? query.OrderByDescending(x => x.ModifiedDate) : query.OrderBy(x => x.ModifiedDate),
            _ => descending ? query.OrderByDescending(x => x.ProductID) : query.OrderBy(x => x.ProductID)
        };
    }
    private static ProductDto ToDto(Product item) => new(
        item.ProductID,
        item.Name,
        item.ProductNumber,
        item.Color,
        item.StandardCost,
        item.ListPrice,
        item.Size,
        item.Weight,
        item.ProductCategoryID,
        item.ProductModelID,
        item.SellStartDate,
        item.SellEndDate,
        item.DiscontinuedDate,
        item.ThumbNailPhoto,
        item.ThumbnailPhotoFileName,
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
            Resource = "Product",
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
