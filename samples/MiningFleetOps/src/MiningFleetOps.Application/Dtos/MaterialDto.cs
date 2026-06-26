namespace MiningFleetOps.Application.Dtos;

public sealed partial record MaterialDto
(
    int MaterialId,
    string MaterialCode,
    string MaterialName,
    decimal? DensityTonnesPerM3,
    bool IsOre
);
