namespace MiningFleetOps.Application.Dtos;

public sealed partial record CreateFluidTypeRequest
(
    string FluidCode,
    string FluidName,
    string FluidCategory,
    decimal? DefaultIntervalHours,
    bool IsActive
);
