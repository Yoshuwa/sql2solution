namespace MiningFleetOps.Application.Dtos;

public sealed partial record FuelTypeDto
(
    int FuelTypeId,
    string FuelCode,
    string FuelName,
    decimal? EnergyDensityMjPerL,
    decimal? Co2KgPerL,
    bool IsActive
);
