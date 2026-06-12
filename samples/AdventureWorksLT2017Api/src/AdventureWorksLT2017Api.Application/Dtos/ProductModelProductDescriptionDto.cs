namespace AdventureWorksLT2017Api.Application.Dtos;

public sealed partial record ProductModelProductDescriptionDto
(
    int ProductModelID,
    int ProductDescriptionID,
    string Culture,
    Guid Rowguid,
    DateTime ModifiedDate
);
