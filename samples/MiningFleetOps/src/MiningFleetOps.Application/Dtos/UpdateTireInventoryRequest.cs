namespace MiningFleetOps.Application.Dtos;

public sealed partial record UpdateTireInventoryRequest
(
    string TireSerialNumber,
    string? Manufacturer,
    string TireSize,
    string TireType,
    DateTime? PurchaseDate,
    decimal? PurchaseCost,
    decimal OriginalTreadDepthMm,
    string Status,
    DateTime CreatedAt
);
