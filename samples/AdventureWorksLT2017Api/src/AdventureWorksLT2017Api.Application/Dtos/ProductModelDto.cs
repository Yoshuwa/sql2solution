namespace AdventureWorksLT2017Api.Application.Dtos;

public sealed partial record ProductModelDto
(
    int ProductModelID,
    string Name,
    string? CatalogDescription,
    Guid Rowguid,
    DateTime ModifiedDate
);
