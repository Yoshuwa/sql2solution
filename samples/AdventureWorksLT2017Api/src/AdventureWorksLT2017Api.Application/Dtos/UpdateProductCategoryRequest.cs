namespace AdventureWorksLT2017Api.Application.Dtos;

public sealed partial record UpdateProductCategoryRequest
(
    int? ParentProductCategoryID,
    string Name,
    Guid Rowguid,
    DateTime ModifiedDate
);
