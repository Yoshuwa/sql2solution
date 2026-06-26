namespace MiningFleetOps.Application.Dtos;

public sealed partial record PartDto
(
    int PartId,
    string PartNumber,
    string PartName,
    string? PartCategory,
    string UnitOfMeasure,
    decimal? StandardCost,
    decimal ReorderPoint,
    decimal OnHandQuantity
);
