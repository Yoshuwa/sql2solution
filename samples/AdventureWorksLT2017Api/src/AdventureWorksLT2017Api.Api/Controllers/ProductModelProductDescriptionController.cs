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
[Route("api/productModelProductDescriptions")]
public sealed partial class ProductModelProductDescriptionController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProductModelProductDescriptionController(AppDbContext db)
    {
        _db = db;
    }

    partial void OnBeforeCreate(CreateProductModelProductDescriptionRequest request, ProductModelProductDescription item);
    partial void OnAfterCreate(ProductModelProductDescription item);
    partial void OnBeforeUpdate(ProductModelProductDescription item, UpdateProductModelProductDescriptionRequest request);
    partial void OnBeforeDelete(ProductModelProductDescription item);

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<ProductModelProductDescriptionDto>>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null, [FromQuery] string? filterField = null, [FromQuery] string? filterValue = null, [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        IQueryable<ProductModelProductDescription> query = _db.Set<ProductModelProductDescription>().AsNoTracking();
        query = ApplySearch(query, search);
        query = ApplyFilter(query, filterField, filterValue);
        query = ApplySort(query, sortBy, sortDirection);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<ProductModelProductDescriptionDto>>.Success("records loaded", new PagedResult<ProductModelProductDescriptionDto>(items, page, pageSize, total)));
    }


    private static IQueryable<ProductModelProductDescription> ApplySearch(IQueryable<ProductModelProductDescription> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        search = search.Trim();
        return query.Where(x => (x.Culture != null && x.Culture.Contains(search)));
    }

    private static IQueryable<ProductModelProductDescription> ApplyFilter(IQueryable<ProductModelProductDescription> query, string? filterField, string? filterValue)
    {
        if (string.IsNullOrWhiteSpace(filterField) || string.IsNullOrWhiteSpace(filterValue)) return query;
        filterField = filterField.Trim();
        filterValue = filterValue.Trim();
        return filterField.ToLowerInvariant() switch
        {
            "productmodelid" => int.TryParse(filterValue, out var ProductModelIDValue) ? query.Where(x => x.ProductModelID == ProductModelIDValue) : query,
            "productdescriptionid" => int.TryParse(filterValue, out var ProductDescriptionIDValue) ? query.Where(x => x.ProductDescriptionID == ProductDescriptionIDValue) : query,
            "culture" => query.Where(x => x.Culture != null && x.Culture.Contains(filterValue)),
            "rowguid" => Guid.TryParse(filterValue, out var RowguidValue) ? query.Where(x => x.Rowguid == RowguidValue) : query,
            "modifieddate" => DateTime.TryParse(filterValue, out var ModifiedDateValue) ? query.Where(x => x.ModifiedDate == ModifiedDateValue) : query,
            _ => query
        };
    }

    private static IQueryable<ProductModelProductDescription> ApplySort(IQueryable<ProductModelProductDescription> query, string? sortBy, string? sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) || string.Equals(sortDirection, "descending", StringComparison.OrdinalIgnoreCase);
        var field = string.IsNullOrWhiteSpace(sortBy) ? "ProductModelID" : sortBy.Trim();
        return field.ToLowerInvariant() switch
        {
            "productmodelid" => descending ? query.OrderByDescending(x => x.ProductModelID) : query.OrderBy(x => x.ProductModelID),
            "productdescriptionid" => descending ? query.OrderByDescending(x => x.ProductDescriptionID) : query.OrderBy(x => x.ProductDescriptionID),
            "culture" => descending ? query.OrderByDescending(x => x.Culture) : query.OrderBy(x => x.Culture),
            "rowguid" => descending ? query.OrderByDescending(x => x.Rowguid) : query.OrderBy(x => x.Rowguid),
            "modifieddate" => descending ? query.OrderByDescending(x => x.ModifiedDate) : query.OrderBy(x => x.ModifiedDate),
            _ => descending ? query.OrderByDescending(x => x.ProductModelID) : query.OrderBy(x => x.ProductModelID)
        };
    }
    private static ProductModelProductDescriptionDto ToDto(ProductModelProductDescription item) => new(
        item.ProductModelID,
        item.ProductDescriptionID,
        item.Culture,
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
            Resource = "ProductModelProductDescription",
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
