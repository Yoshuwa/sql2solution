namespace MiningFleetOps.Application.Dtos;

public sealed partial record FluidTypeDto
(
    int FluidTypeId,
    string FluidCode,
    string FluidName,
    string FluidCategory,
    decimal? DefaultIntervalHours,
    bool IsActive
);
