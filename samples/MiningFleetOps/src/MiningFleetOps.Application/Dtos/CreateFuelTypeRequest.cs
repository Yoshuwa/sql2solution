namespace MiningFleetOps.Application.Dtos;

public sealed partial record CreateFuelTypeRequest
(
    string FuelCode,
    string FuelName,
    decimal? EnergyDensityMjPerL,
    decimal? Co2KgPerL,
    bool IsActive
);
