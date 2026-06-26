namespace MiningFleetOps.Application.Dtos;

public sealed partial record UpdateFluidTypeRequest
(
    string FluidCode,
    string FluidName,
    string FluidCategory,
    decimal? DefaultIntervalHours,
    bool IsActive
);
