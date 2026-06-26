namespace MiningFleetOps.Application.Dtos;

public sealed partial record CreateMaterialRequest
(
    string MaterialCode,
    string MaterialName,
    decimal? DensityTonnesPerM3,
    bool IsOre
);
