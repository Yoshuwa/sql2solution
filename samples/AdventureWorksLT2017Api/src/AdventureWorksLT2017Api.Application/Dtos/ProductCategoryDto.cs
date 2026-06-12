namespace AdventureWorksLT2017Api.Application.Dtos;

public sealed partial record ProductCategoryDto
(
    int ProductCategoryID,
    int? ParentProductCategoryID,
    string Name,
    Guid Rowguid,
    DateTime ModifiedDate
);
