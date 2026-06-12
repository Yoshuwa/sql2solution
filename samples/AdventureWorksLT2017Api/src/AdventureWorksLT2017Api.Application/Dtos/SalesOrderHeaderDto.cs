namespace AdventureWorksLT2017Api.Application.Dtos;

public sealed partial record SalesOrderHeaderDto
(
    int SalesOrderID,
    byte RevisionNumber,
    DateTime OrderDate,
    DateTime DueDate,
    DateTime? ShipDate,
    byte Status,
    bool OnlineOrderFlag,
    string SalesOrderNumber,
    string? PurchaseOrderNumber,
    string? AccountNumber,
    int CustomerID,
    int? ShipToAddressID,
    int? BillToAddressID,
    string ShipMethod,
    string? CreditCardApprovalCode,
    decimal SubTotal,
    decimal TaxAmt,
    decimal Freight,
    decimal TotalDue,
    string? Comment,
    Guid Rowguid,
    DateTime ModifiedDate
);
