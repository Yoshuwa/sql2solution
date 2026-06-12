namespace AdventureWorksLT2017Api.Application.Dtos;

public sealed partial record ProductDto
(
    int ProductID,
    string Name,
    string ProductNumber,
    string? Color,
    decimal StandardCost,
    decimal ListPrice,
    string? Size,
    decimal? Weight,
    int? ProductCategoryID,
    int? ProductModelID,
    DateTime SellStartDate,
    DateTime? SellEndDate,
    DateTime? DiscontinuedDate,
    byte[]? ThumbNailPhoto,
    string? ThumbnailPhotoFileName,
    Guid Rowguid,
    DateTime ModifiedDate
);
