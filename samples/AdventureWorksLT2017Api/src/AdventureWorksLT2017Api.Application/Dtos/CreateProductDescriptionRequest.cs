namespace AdventureWorksLT2017Api.Application.Dtos;

public sealed partial record CreateProductDescriptionRequest
(
    string Description,
    Guid Rowguid,
    DateTime ModifiedDate
);
