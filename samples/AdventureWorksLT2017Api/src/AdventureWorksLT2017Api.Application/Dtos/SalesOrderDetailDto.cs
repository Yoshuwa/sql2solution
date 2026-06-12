namespace AdventureWorksLT2017Api.Application.Dtos;

public sealed partial record SalesOrderDetailDto
(
    int SalesOrderID,
    int SalesOrderDetailID,
    short OrderQty,
    int ProductID,
    decimal UnitPrice,
    decimal UnitPriceDiscount,
    decimal LineTotal,
    Guid Rowguid,
    DateTime ModifiedDate
);
