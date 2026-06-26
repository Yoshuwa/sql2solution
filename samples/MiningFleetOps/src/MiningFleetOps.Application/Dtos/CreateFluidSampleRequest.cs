namespace MiningFleetOps.Application.Dtos;

public sealed partial record CreateFluidSampleRequest
(
    int EquipmentId,
    int FluidTypeId,
    DateTime SampledAt,
    decimal HourMeter,
    string? LabReference,
    decimal? IronPpm,
    decimal? CopperPpm,
    decimal? SiliconPpm,
    decimal? ViscosityCst,
    decimal? WaterPercent,
    string Severity,
    string? Recommendation
);
