namespace AdventureWorksLT2017Api.Application.Dtos;

public sealed partial record CreateProductCategoryRequest
(
    int? ParentProductCategoryID,
    string Name,
    Guid Rowguid,
    DateTime ModifiedDate
);
