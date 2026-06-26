namespace MiningFleetOps.Application.Dtos;

public sealed partial record UpdatePartRequest
(
    string PartNumber,
    string PartName,
    string? PartCategory,
    string UnitOfMeasure,
    decimal? StandardCost,
    decimal ReorderPoint,
    decimal OnHandQuantity
);
