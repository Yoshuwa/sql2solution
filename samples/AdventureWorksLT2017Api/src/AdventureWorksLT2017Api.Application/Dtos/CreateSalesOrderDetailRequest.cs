namespace AdventureWorksLT2017Api.Application.Dtos;

public sealed partial record CreateSalesOrderDetailRequest
(
    short OrderQty,
    int ProductID,
    decimal UnitPrice,
    decimal UnitPriceDiscount,
    decimal LineTotal,
    Guid Rowguid,
    DateTime ModifiedDate
);
