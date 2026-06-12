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
[Route("api/salesOrderDetails")]
public sealed partial class SalesOrderDetailController : ControllerBase
{
    private readonly AppDbContext _db;

    public SalesOrderDetailController(AppDbContext db)
    {
        _db = db;
    }

    partial void OnBeforeCreate(CreateSalesOrderDetailRequest request, SalesOrderDetail item);
    partial void OnAfterCreate(SalesOrderDetail item);
    partial void OnBeforeUpdate(SalesOrderDetail item, UpdateSalesOrderDetailRequest request);
    partial void OnBeforeDelete(SalesOrderDetail item);

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<SalesOrderDetailDto>>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null, [FromQuery] string? filterField = null, [FromQuery] string? filterValue = null, [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        IQueryable<SalesOrderDetail> query = _db.Set<SalesOrderDetail>().AsNoTracking();
        query = ApplySearch(query, search);
        query = ApplyFilter(query, filterField, filterValue);
        query = ApplySort(query, sortBy, sortDirection);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<SalesOrderDetailDto>>.Success("records loaded", new PagedResult<SalesOrderDetailDto>(items, page, pageSize, total)));
    }


    private static IQueryable<SalesOrderDetail> ApplySearch(IQueryable<SalesOrderDetail> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        search = search.Trim();
        return query;
    }

    private static IQueryable<SalesOrderDetail> ApplyFilter(IQueryable<SalesOrderDetail> query, string? filterField, string? filterValue)
    {
        if (string.IsNullOrWhiteSpace(filterField) || string.IsNullOrWhiteSpace(filterValue)) return query;
        filterField = filterField.Trim();
        filterValue = filterValue.Trim();
        return filterField.ToLowerInvariant() switch
        {
            "salesorderid" => int.TryParse(filterValue, out var SalesOrderIDValue) ? query.Where(x => x.SalesOrderID == SalesOrderIDValue) : query,
            "salesorderdetailid" => int.TryParse(filterValue, out var SalesOrderDetailIDValue) ? query.Where(x => x.SalesOrderDetailID == SalesOrderDetailIDValue) : query,
            "orderqty" => short.TryParse(filterValue, out var OrderQtyValue) ? query.Where(x => x.OrderQty == OrderQtyValue) : query,
            "productid" => int.TryParse(filterValue, out var ProductIDValue) ? query.Where(x => x.ProductID == ProductIDValue) : query,
            "unitprice" => decimal.TryParse(filterValue, out var UnitPriceValue) ? query.Where(x => x.UnitPrice == UnitPriceValue) : query,
            "unitpricediscount" => decimal.TryParse(filterValue, out var UnitPriceDiscountValue) ? query.Where(x => x.UnitPriceDiscount == UnitPriceDiscountValue) : query,
            "linetotal" => decimal.TryParse(filterValue, out var LineTotalValue) ? query.Where(x => x.LineTotal == LineTotalValue) : query,
            "rowguid" => Guid.TryParse(filterValue, out var RowguidValue) ? query.Where(x => x.Rowguid == RowguidValue) : query,
            "modifieddate" => DateTime.TryParse(filterValue, out var ModifiedDateValue) ? query.Where(x => x.ModifiedDate == ModifiedDateValue) : query,
            _ => query
        };
    }

    private static IQueryable<SalesOrderDetail> ApplySort(IQueryable<SalesOrderDetail> query, string? sortBy, string? sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) || string.Equals(sortDirection, "descending", StringComparison.OrdinalIgnoreCase);
        var field = string.IsNullOrWhiteSpace(sortBy) ? "SalesOrderID" : sortBy.Trim();
        return field.ToLowerInvariant() switch
        {
            "salesorderid" => descending ? query.OrderByDescending(x => x.SalesOrderID) : query.OrderBy(x => x.SalesOrderID),
            "salesorderdetailid" => descending ? query.OrderByDescending(x => x.SalesOrderDetailID) : query.OrderBy(x => x.SalesOrderDetailID),
            "orderqty" => descending ? query.OrderByDescending(x => x.OrderQty) : query.OrderBy(x => x.OrderQty),
            "productid" => descending ? query.OrderByDescending(x => x.ProductID) : query.OrderBy(x => x.ProductID),
            "unitprice" => descending ? query.OrderByDescending(x => x.UnitPrice) : query.OrderBy(x => x.UnitPrice),
            "unitpricediscount" => descending ? query.OrderByDescending(x => x.UnitPriceDiscount) : query.OrderBy(x => x.UnitPriceDiscount),
            "linetotal" => descending ? query.OrderByDescending(x => x.LineTotal) : query.OrderBy(x => x.LineTotal),
            "rowguid" => descending ? query.OrderByDescending(x => x.Rowguid) : query.OrderBy(x => x.Rowguid),
            "modifieddate" => descending ? query.OrderByDescending(x => x.ModifiedDate) : query.OrderBy(x => x.ModifiedDate),
            _ => descending ? query.OrderByDescending(x => x.SalesOrderID) : query.OrderBy(x => x.SalesOrderID)
        };
    }
    private static SalesOrderDetailDto ToDto(SalesOrderDetail item) => new(
        item.SalesOrderID,
        item.SalesOrderDetailID,
        item.OrderQty,
        item.ProductID,
        item.UnitPrice,
        item.UnitPriceDiscount,
        item.LineTotal,
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
            Resource = "SalesOrderDetail",
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
