namespace AdventureWorksLT2017Api.Application.Dtos;

public sealed partial record CreateProductModelRequest
(
    string Name,
    string? CatalogDescription,
    Guid Rowguid,
    DateTime ModifiedDate
);
