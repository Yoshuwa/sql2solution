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
[Route("api/salesOrderHeaders")]
public sealed partial class SalesOrderHeaderController : ControllerBase
{
    private readonly AppDbContext _db;

    public SalesOrderHeaderController(AppDbContext db)
    {
        _db = db;
    }

    partial void OnBeforeCreate(CreateSalesOrderHeaderRequest request, SalesOrderHeader item);
    partial void OnAfterCreate(SalesOrderHeader item);
    partial void OnBeforeUpdate(SalesOrderHeader item, UpdateSalesOrderHeaderRequest request);
    partial void OnBeforeDelete(SalesOrderHeader item);

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<SalesOrderHeaderDto>>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null, [FromQuery] string? filterField = null, [FromQuery] string? filterValue = null, [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        IQueryable<SalesOrderHeader> query = _db.Set<SalesOrderHeader>().AsNoTracking();
        query = ApplySearch(query, search);
        query = ApplyFilter(query, filterField, filterValue);
        query = ApplySort(query, sortBy, sortDirection);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<SalesOrderHeaderDto>>.Success("records loaded", new PagedResult<SalesOrderHeaderDto>(items, page, pageSize, total)));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<SalesOrderHeaderDto>>> GetById(int id, CancellationToken ct)
    {
        IQueryable<SalesOrderHeader> query = _db.Set<SalesOrderHeader>().AsNoTracking();
        var item = await query.FirstOrDefaultAsync(x => x.SalesOrderID!.Equals(id), ct);
        return item is null ? NotFound(ApiResponse<object>.Warning("record not found")) : Ok(ApiResponse<SalesOrderHeaderDto>.Success("record loaded", ToDto(item)));
    }

    [HttpGet("{id}/history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuditTrailDto>>>> GetHistory(int id, CancellationToken ct)
    {
        var canReadRecord = await _db.Set<SalesOrderHeader>().AsNoTracking().AnyAsync(x => x.SalesOrderID!.Equals(id), ct);
        if (!canReadRecord) return NotFound(ApiResponse<object>.Warning("record not found"));
        await EnsureAuditTrailTableAsync(ct);
        var resourceKey = Convert.ToString(id) ?? string.Empty;
        var history = await _db.AuditTrailEntries
            .AsNoTracking()
            .Where(entry => entry.Resource == "SalesOrderHeader" && entry.ResourceKey == resourceKey)
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .Take(100)
            .Select(entry => ToAuditTrailDto(entry))
            .ToListAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<AuditTrailDto>>.Success("activity loaded", history));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<SalesOrderHeaderDto>>> Create(CreateSalesOrderHeaderRequest request, CancellationToken ct)
    {
        var item = new SalesOrderHeader
        {
            RevisionNumber = request.RevisionNumber,
            OrderDate = request.OrderDate,
            DueDate = request.DueDate,
            ShipDate = request.ShipDate,
            Status = request.Status,
            OnlineOrderFlag = request.OnlineOrderFlag,
            SalesOrderNumber = request.SalesOrderNumber,
            PurchaseOrderNumber = request.PurchaseOrderNumber,
            AccountNumber = request.AccountNumber,
            CustomerID = request.CustomerID,
            ShipToAddressID = request.ShipToAddressID,
            BillToAddressID = request.BillToAddressID,
            ShipMethod = request.ShipMethod,
            CreditCardApprovalCode = request.CreditCardApprovalCode,
            SubTotal = request.SubTotal,
            TaxAmt = request.TaxAmt,
            Freight = request.Freight,
            TotalDue = request.TotalDue,
            Comment = request.Comment,
            Rowguid = request.Rowguid,
            ModifiedDate = request.ModifiedDate,
        };
        OnBeforeCreate(request, item);
        _db.Set<SalesOrderHeader>().Add(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Created", Convert.ToString(item.SalesOrderID) ?? string.Empty, $"Created SalesOrderHeader record {item.SalesOrderID}.", ToDto(item), ct);
        OnAfterCreate(item);
        return CreatedAtAction(nameof(GetById), new { id = item.SalesOrderID }, ApiResponse<SalesOrderHeaderDto>.Success("record created", ToDto(item)));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateSalesOrderHeaderRequest request, CancellationToken ct)
    {
        var item = await _db.Set<SalesOrderHeader>().FirstOrDefaultAsync(x => x.SalesOrderID!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeUpdate(item, request);
        item.RevisionNumber = request.RevisionNumber;
        item.OrderDate = request.OrderDate;
        item.DueDate = request.DueDate;
        item.ShipDate = request.ShipDate;
        item.Status = request.Status;
        item.OnlineOrderFlag = request.OnlineOrderFlag;
        item.SalesOrderNumber = request.SalesOrderNumber;
        item.PurchaseOrderNumber = request.PurchaseOrderNumber;
        item.AccountNumber = request.AccountNumber;
        item.CustomerID = request.CustomerID;
        item.ShipToAddressID = request.ShipToAddressID;
        item.BillToAddressID = request.BillToAddressID;
        item.ShipMethod = request.ShipMethod;
        item.CreditCardApprovalCode = request.CreditCardApprovalCode;
        item.SubTotal = request.SubTotal;
        item.TaxAmt = request.TaxAmt;
        item.Freight = request.Freight;
        item.TotalDue = request.TotalDue;
        item.Comment = request.Comment;
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
        await LogAuditTrailAsync("Updated", Convert.ToString(item.SalesOrderID) ?? string.Empty, $"Updated SalesOrderHeader record {item.SalesOrderID}.", auditChanges, ct);
        return Ok(ApiResponse<object>.Success("record updated", new { updated = 1 }));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch(int id, UpdateSalesOrderHeaderRequest request, CancellationToken ct)
    {
        return await Update(id, request, ct);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var item = await _db.Set<SalesOrderHeader>().FirstOrDefaultAsync(x => x.SalesOrderID!.Equals(id), ct);
        if (item is null) return NotFound(ApiResponse<object>.Warning("record not found"));
        OnBeforeDelete(item);
        _db.Set<SalesOrderHeader>().Remove(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        await LogAuditTrailAsync("Deleted", Convert.ToString(id) ?? string.Empty, $"Hard deleted SalesOrderHeader record {id}.", ToDto(item), ct);
        return Ok(ApiResponse<object>.Success("record deleted", new { deleted = 1, mode = "Hard" }));
    }

    [HttpPost("bulk/export")]
    public async Task<ActionResult<ApiResponse<PagedResult<SalesOrderHeaderDto>>>> ExportBulk(BulkIdsRequest request, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return Ok(ApiResponse<PagedResult<SalesOrderHeaderDto>>.Warning("no records selected", new PagedResult<SalesOrderHeaderDto>(Array.Empty<SalesOrderHeaderDto>(), page, pageSize, 0)));
        IQueryable<SalesOrderHeader> query = _db.Set<SalesOrderHeader>().AsNoTracking().Where(x => ids.Contains(x.SalesOrderID));
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToDto(x)).ToListAsync(ct);
        return Ok(ApiResponse<PagedResult<SalesOrderHeaderDto>>.Success("records exported", new PagedResult<SalesOrderHeaderDto>(items, page, pageSize, total)));
    }

    [HttpPatch("bulk")]
    public async Task<IActionResult> UpdateBulk(BulkUpdateRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        if (string.IsNullOrWhiteSpace(request.Field)) return BadRequest(ApiResponse<object>.Error("error", new { error = "Choose a field to update." }));
        IQueryable<SalesOrderHeader> query = _db.Set<SalesOrderHeader>().Where(x => ids.Contains(x.SalesOrderID));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return NotFound(ApiResponse<object>.Warning("records not found"));
        if (!ApplyBulkUpdate(items, request, out var error)) return BadRequest(ApiResponse<object>.Error("error", new { error }));
        var auditChanges = items.ToDictionary(item => Convert.ToString(item.SalesOrderID) ?? string.Empty, item => GetEntityChanges(_db.Entry(item)));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Updated", Convert.ToString(item.SalesOrderID) ?? string.Empty, $"Updated SalesOrderHeader record {item.SalesOrderID} in bulk update.", auditChanges[Convert.ToString(item.SalesOrderID) ?? string.Empty], ct);
        return Ok(ApiResponse<object>.Success("records updated", new { updated = items.Count }));
    }

    [HttpPost("bulk/delete")]
    public async Task<IActionResult> DeleteBulk(BulkIdsRequest request, CancellationToken ct)
    {
        var ids = ParseBulkIds(request.Ids);
        if (ids.Count == 0) return BadRequest(ApiResponse<object>.Error("error", new { error = "Select at least one row." }));
        IQueryable<SalesOrderHeader> query = _db.Set<SalesOrderHeader>().Where(x => ids.Contains(x.SalesOrderID));
        var items = await query.ToListAsync(ct);
        if (items.Count == 0) return Ok(ApiResponse<object>.Warning("records not found", new { deleted = 0 }));
        foreach (var item in items)
        {
            OnBeforeDelete(item);
        }
        _db.Set<SalesOrderHeader>().RemoveRange(items);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponse<object>.Error("error", new { error = "The request conflicts with an existing record or database constraint." }));
        }
        foreach (var item in items)
            await LogAuditTrailAsync("Deleted", Convert.ToString(item.SalesOrderID) ?? string.Empty, $"Hard deleted SalesOrderHeader record {item.SalesOrderID} in bulk delete.", ToDto(item), ct);
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

    private static bool ApplyBulkUpdate(IReadOnlyList<SalesOrderHeader> items, BulkUpdateRequest request, out string error)
    {
        error = string.Empty;
        return request.Field.Trim().ToLowerInvariant() switch
        {
            "revisionnumber" => ApplyBulkRevisionNumber(items, request.Value, out error),
            "orderdate" => ApplyBulkOrderDate(items, request.Value, out error),
            "duedate" => ApplyBulkDueDate(items, request.Value, out error),
            "shipdate" => ApplyBulkShipDate(items, request.Value, out error),
            "status" => ApplyBulkStatus(items, request.Value, out error),
            "onlineorderflag" => ApplyBulkOnlineOrderFlag(items, request.Value, out error),
            "salesordernumber" => ApplyBulkSalesOrderNumber(items, request.Value, out error),
            "purchaseordernumber" => ApplyBulkPurchaseOrderNumber(items, request.Value, out error),
            "accountnumber" => ApplyBulkAccountNumber(items, request.Value, out error),
            "customerid" => ApplyBulkCustomerID(items, request.Value, out error),
            "shiptoaddressid" => ApplyBulkShipToAddressID(items, request.Value, out error),
            "billtoaddressid" => ApplyBulkBillToAddressID(items, request.Value, out error),
            "shipmethod" => ApplyBulkShipMethod(items, request.Value, out error),
            "creditcardapprovalcode" => ApplyBulkCreditCardApprovalCode(items, request.Value, out error),
            "subtotal" => ApplyBulkSubTotal(items, request.Value, out error),
            "taxamt" => ApplyBulkTaxAmt(items, request.Value, out error),
            "freight" => ApplyBulkFreight(items, request.Value, out error),
            "totaldue" => ApplyBulkTotalDue(items, request.Value, out error),
            "comment" => ApplyBulkComment(items, request.Value, out error),
            "rowguid" => ApplyBulkRowguid(items, request.Value, out error),
            "modifieddate" => ApplyBulkModifiedDate(items, request.Value, out error),
            _ => FailBulkUpdate("Field is not bulk editable.", out error)
        };
    }

    private static bool ApplyBulkRevisionNumber(IReadOnlyList<SalesOrderHeader> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!byte.TryParse(raw, out var value)) return FailBulkUpdate("RevisionNumber requires a byte value.", out error);
        foreach (var item in items) item.RevisionNumber = value;
        return true;
    }

    private static bool ApplyBulkOrderDate(IReadOnlyList<SalesOrderHeader> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!DateTime.TryParse(raw, out var value)) return FailBulkUpdate("OrderDate requires a DateTime value.", out error);
        foreach (var item in items) item.OrderDate = value;
        return true;
    }

    private static bool ApplyBulkDueDate(IReadOnlyList<SalesOrderHeader> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!DateTime.TryParse(raw, out var value)) return FailBulkUpdate("DueDate requires a DateTime value.", out error);
        foreach (var item in items) item.DueDate = value;
        return true;
    }

    private static bool ApplyBulkShipDate(IReadOnlyList<SalesOrderHeader> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.ShipDate = null;
            return true;
        }
        if (!DateTime.TryParse(raw, out var value)) return FailBulkUpdate("ShipDate requires a DateTime value.", out error);
        foreach (var item in items) item.ShipDate = value;
        return true;
    }

    private static bool ApplyBulkStatus(IReadOnlyList<SalesOrderHeader> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!byte.TryParse(raw, out var value)) return FailBulkUpdate("Status requires a byte value.", out error);
        foreach (var item in items) item.Status = value;
        return true;
    }

    private static bool ApplyBulkOnlineOrderFlag(IReadOnlyList<SalesOrderHeader> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!bool.TryParse(raw, out var value)) return FailBulkUpdate("OnlineOrderFlag requires a boolean value.", out error);
        foreach (var item in items) item.OnlineOrderFlag = value;
        return true;
    }

    private static bool ApplyBulkSalesOrderNumber(IReadOnlyList<SalesOrderHeader> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.SalesOrderNumber = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkPurchaseOrderNumber(IReadOnlyList<SalesOrderHeader> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.PurchaseOrderNumber = null;
            return true;
        }
        foreach (var item in items) item.PurchaseOrderNumber = raw;
        return true;
    }

    private static bool ApplyBulkAccountNumber(IReadOnlyList<SalesOrderHeader> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.AccountNumber = null;
            return true;
        }
        foreach (var item in items) item.AccountNumber = raw;
        return true;
    }

    private static bool ApplyBulkCustomerID(IReadOnlyList<SalesOrderHeader> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("CustomerID requires a int value.", out error);
        foreach (var item in items) item.CustomerID = value;
        return true;
    }

    private static bool ApplyBulkShipToAddressID(IReadOnlyList<SalesOrderHeader> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.ShipToAddressID = null;
            return true;
        }
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("ShipToAddressID requires a int value.", out error);
        foreach (var item in items) item.ShipToAddressID = value;
        return true;
    }

    private static bool ApplyBulkBillToAddressID(IReadOnlyList<SalesOrderHeader> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.BillToAddressID = null;
            return true;
        }
        if (!int.TryParse(raw, out var value)) return FailBulkUpdate("BillToAddressID requires a int value.", out error);
        foreach (var item in items) item.BillToAddressID = value;
        return true;
    }

    private static bool ApplyBulkShipMethod(IReadOnlyList<SalesOrderHeader> items, string? raw, out string error)
    {
        error = string.Empty;
        foreach (var item in items) item.ShipMethod = raw ?? string.Empty;
        return true;
    }

    private static bool ApplyBulkCreditCardApprovalCode(IReadOnlyList<SalesOrderHeader> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.CreditCardApprovalCode = null;
            return true;
        }
        foreach (var item in items) item.CreditCardApprovalCode = raw;
        return true;
    }

    private static bool ApplyBulkSubTotal(IReadOnlyList<SalesOrderHeader> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("SubTotal requires a decimal value.", out error);
        foreach (var item in items) item.SubTotal = value;
        return true;
    }

    private static bool ApplyBulkTaxAmt(IReadOnlyList<SalesOrderHeader> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("TaxAmt requires a decimal value.", out error);
        foreach (var item in items) item.TaxAmt = value;
        return true;
    }

    private static bool ApplyBulkFreight(IReadOnlyList<SalesOrderHeader> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("Freight requires a decimal value.", out error);
        foreach (var item in items) item.Freight = value;
        return true;
    }

    private static bool ApplyBulkTotalDue(IReadOnlyList<SalesOrderHeader> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!decimal.TryParse(raw, out var value)) return FailBulkUpdate("TotalDue requires a decimal value.", out error);
        foreach (var item in items) item.TotalDue = value;
        return true;
    }

    private static bool ApplyBulkComment(IReadOnlyList<SalesOrderHeader> items, string? raw, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var item in items) item.Comment = null;
            return true;
        }
        foreach (var item in items) item.Comment = raw;
        return true;
    }

    private static bool ApplyBulkRowguid(IReadOnlyList<SalesOrderHeader> items, string? raw, out string error)
    {
        error = string.Empty;
        if (!Guid.TryParse(raw, out var value)) return FailBulkUpdate("Rowguid requires a Guid value.", out error);
        foreach (var item in items) item.Rowguid = value;
        return true;
    }

    private static bool ApplyBulkModifiedDate(IReadOnlyList<SalesOrderHeader> items, string? raw, out string error)
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


    private static IQueryable<SalesOrderHeader> ApplySearch(IQueryable<SalesOrderHeader> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        search = search.Trim();
        return query.Where(x => (x.SalesOrderNumber != null && x.SalesOrderNumber.Contains(search)) || (x.PurchaseOrderNumber != null && x.PurchaseOrderNumber.Contains(search)) || (x.AccountNumber != null && x.AccountNumber.Contains(search)) || (x.ShipMethod != null && x.ShipMethod.Contains(search)) || (x.CreditCardApprovalCode != null && x.CreditCardApprovalCode.Contains(search)) || (x.Comment != null && x.Comment.Contains(search)));
    }

    private static IQueryable<SalesOrderHeader> ApplyFilter(IQueryable<SalesOrderHeader> query, string? filterField, string? filterValue)
    {
        if (string.IsNullOrWhiteSpace(filterField) || string.IsNullOrWhiteSpace(filterValue)) return query;
        filterField = filterField.Trim();
        filterValue = filterValue.Trim();
        return filterField.ToLowerInvariant() switch
        {
            "salesorderid" => int.TryParse(filterValue, out var SalesOrderIDValue) ? query.Where(x => x.SalesOrderID == SalesOrderIDValue) : query,
            "revisionnumber" => byte.TryParse(filterValue, out var RevisionNumberValue) ? query.Where(x => x.RevisionNumber == RevisionNumberValue) : query,
            "orderdate" => DateTime.TryParse(filterValue, out var OrderDateValue) ? query.Where(x => x.OrderDate == OrderDateValue) : query,
            "duedate" => DateTime.TryParse(filterValue, out var DueDateValue) ? query.Where(x => x.DueDate == DueDateValue) : query,
            "shipdate" => DateTime.TryParse(filterValue, out var ShipDateValue) ? query.Where(x => x.ShipDate == ShipDateValue) : query,
            "status" => byte.TryParse(filterValue, out var StatusValue) ? query.Where(x => x.Status == StatusValue) : query,
            "onlineorderflag" => bool.TryParse(filterValue, out var OnlineOrderFlagValue) ? query.Where(x => x.OnlineOrderFlag == OnlineOrderFlagValue) : query,
            "salesordernumber" => query.Where(x => x.SalesOrderNumber != null && x.SalesOrderNumber.Contains(filterValue)),
            "purchaseordernumber" => query.Where(x => x.PurchaseOrderNumber != null && x.PurchaseOrderNumber.Contains(filterValue)),
            "accountnumber" => query.Where(x => x.AccountNumber != null && x.AccountNumber.Contains(filterValue)),
            "customerid" => int.TryParse(filterValue, out var CustomerIDValue) ? query.Where(x => x.CustomerID == CustomerIDValue) : query,
            "shiptoaddressid" => int.TryParse(filterValue, out var ShipToAddressIDValue) ? query.Where(x => x.ShipToAddressID == ShipToAddressIDValue) : query,
            "billtoaddressid" => int.TryParse(filterValue, out var BillToAddressIDValue) ? query.Where(x => x.BillToAddressID == BillToAddressIDValue) : query,
            "shipmethod" => query.Where(x => x.ShipMethod != null && x.ShipMethod.Contains(filterValue)),
            "creditcardapprovalcode" => query.Where(x => x.CreditCardApprovalCode != null && x.CreditCardApprovalCode.Contains(filterValue)),
            "subtotal" => decimal.TryParse(filterValue, out var SubTotalValue) ? query.Where(x => x.SubTotal == SubTotalValue) : query,
            "taxamt" => decimal.TryParse(filterValue, out var TaxAmtValue) ? query.Where(x => x.TaxAmt == TaxAmtValue) : query,
            "freight" => decimal.TryParse(filterValue, out var FreightValue) ? query.Where(x => x.Freight == FreightValue) : query,
            "totaldue" => decimal.TryParse(filterValue, out var TotalDueValue) ? query.Where(x => x.TotalDue == TotalDueValue) : query,
            "comment" => query.Where(x => x.Comment != null && x.Comment.Contains(filterValue)),
            "rowguid" => Guid.TryParse(filterValue, out var RowguidValue) ? query.Where(x => x.Rowguid == RowguidValue) : query,
            "modifieddate" => DateTime.TryParse(filterValue, out var ModifiedDateValue) ? query.Where(x => x.ModifiedDate == ModifiedDateValue) : query,
            _ => query
        };
    }

    private static IQueryable<SalesOrderHeader> ApplySort(IQueryable<SalesOrderHeader> query, string? sortBy, string? sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) || string.Equals(sortDirection, "descending", StringComparison.OrdinalIgnoreCase);
        var field = string.IsNullOrWhiteSpace(sortBy) ? "SalesOrderID" : sortBy.Trim();
        return field.ToLowerInvariant() switch
        {
            "salesorderid" => descending ? query.OrderByDescending(x => x.SalesOrderID) : query.OrderBy(x => x.SalesOrderID),
            "revisionnumber" => descending ? query.OrderByDescending(x => x.RevisionNumber) : query.OrderBy(x => x.RevisionNumber),
            "orderdate" => descending ? query.OrderByDescending(x => x.OrderDate) : query.OrderBy(x => x.OrderDate),
            "duedate" => descending ? query.OrderByDescending(x => x.DueDate) : query.OrderBy(x => x.DueDate),
            "shipdate" => descending ? query.OrderByDescending(x => x.ShipDate) : query.OrderBy(x => x.ShipDate),
            "status" => descending ? query.OrderByDescending(x => x.Status) : query.OrderBy(x => x.Status),
            "onlineorderflag" => descending ? query.OrderByDescending(x => x.OnlineOrderFlag) : query.OrderBy(x => x.OnlineOrderFlag),
            "salesordernumber" => descending ? query.OrderByDescending(x => x.SalesOrderNumber) : query.OrderBy(x => x.SalesOrderNumber),
            "purchaseordernumber" => descending ? query.OrderByDescending(x => x.PurchaseOrderNumber) : query.OrderBy(x => x.PurchaseOrderNumber),
            "accountnumber" => descending ? query.OrderByDescending(x => x.AccountNumber) : query.OrderBy(x => x.AccountNumber),
            "customerid" => descending ? query.OrderByDescending(x => x.CustomerID) : query.OrderBy(x => x.CustomerID),
            "shiptoaddressid" => descending ? query.OrderByDescending(x => x.ShipToAddressID) : query.OrderBy(x => x.ShipToAddressID),
            "billtoaddressid" => descending ? query.OrderByDescending(x => x.BillToAddressID) : query.OrderBy(x => x.BillToAddressID),
            "shipmethod" => descending ? query.OrderByDescending(x => x.ShipMethod) : query.OrderBy(x => x.ShipMethod),
            "creditcardapprovalcode" => descending ? query.OrderByDescending(x => x.CreditCardApprovalCode) : query.OrderBy(x => x.CreditCardApprovalCode),
            "subtotal" => descending ? query.OrderByDescending(x => x.SubTotal) : query.OrderBy(x => x.SubTotal),
            "taxamt" => descending ? query.OrderByDescending(x => x.TaxAmt) : query.OrderBy(x => x.TaxAmt),
            "freight" => descending ? query.OrderByDescending(x => x.Freight) : query.OrderBy(x => x.Freight),
            "totaldue" => descending ? query.OrderByDescending(x => x.TotalDue) : query.OrderBy(x => x.TotalDue),
            "comment" => descending ? query.OrderByDescending(x => x.Comment) : query.OrderBy(x => x.Comment),
            "rowguid" => descending ? query.OrderByDescending(x => x.Rowguid) : query.OrderBy(x => x.Rowguid),
            "modifieddate" => descending ? query.OrderByDescending(x => x.ModifiedDate) : query.OrderBy(x => x.ModifiedDate),
            _ => descending ? query.OrderByDescending(x => x.SalesOrderID) : query.OrderBy(x => x.SalesOrderID)
        };
    }
    private static SalesOrderHeaderDto ToDto(SalesOrderHeader item) => new(
        item.SalesOrderID,
        item.RevisionNumber,
        item.OrderDate,
        item.DueDate,
        item.ShipDate,
        item.Status,
        item.OnlineOrderFlag,
        item.SalesOrderNumber,
        item.PurchaseOrderNumber,
        item.AccountNumber,
        item.CustomerID,
        item.ShipToAddressID,
        item.BillToAddressID,
        item.ShipMethod,
        item.CreditCardApprovalCode,
        item.SubTotal,
        item.TaxAmt,
        item.Freight,
        item.TotalDue,
        item.Comment,
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
            Resource = "SalesOrderHeader",
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
