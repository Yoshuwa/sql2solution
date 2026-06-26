namespace MiningFleetOps.Application.Dtos;

public sealed partial record UpdateMaterialRequest
(
    string MaterialCode,
    string MaterialName,
    decimal? DensityTonnesPerM3,
    bool IsOre
);
