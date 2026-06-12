namespace AdventureWorksLT2017Api.Application.Dtos;

public sealed partial record ProductDescriptionDto
(
    int ProductDescriptionID,
    string Description,
    Guid Rowguid,
    DateTime ModifiedDate
);
