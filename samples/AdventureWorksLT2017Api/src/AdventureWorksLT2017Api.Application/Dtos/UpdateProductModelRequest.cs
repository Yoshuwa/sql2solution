namespace AdventureWorksLT2017Api.Application.Dtos;

public sealed partial record UpdateProductModelRequest
(
    string Name,
    string? CatalogDescription,
    Guid Rowguid,
    DateTime ModifiedDate
);
