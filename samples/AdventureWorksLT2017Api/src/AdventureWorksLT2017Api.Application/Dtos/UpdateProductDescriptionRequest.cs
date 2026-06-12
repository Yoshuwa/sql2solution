namespace AdventureWorksLT2017Api.Application.Dtos;

public sealed partial record UpdateProductDescriptionRequest
(
    string Description,
    Guid Rowguid,
    DateTime ModifiedDate
);
