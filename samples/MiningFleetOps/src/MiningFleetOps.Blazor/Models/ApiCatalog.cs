namespace MiningFleetOps.Blazor.Models;

public sealed partial record ApiField(string Name, string ClrType, bool IsPrimaryKey, bool IsNullable, string InputKind, string LookupResourceKey, string LookupRoute, string LookupValueField, bool IsAuditField, string AuditKind)
{
    public ApiField(string name, string clrType, bool isPrimaryKey, bool isNullable)
        : this(name, clrType, isPrimaryKey, isNullable, ResolveInputKind(clrType), "", "", "", false, "")
    {
    }

    public bool HasLookup => !string.IsNullOrWhiteSpace(LookupRoute) && !string.IsNullOrWhiteSpace(LookupValueField);

    private static string ResolveInputKind(string clrType)
    {
        return clrType switch
        {
            "bool" => "checkbox",
            "DateOnly" => "date",
            "DateTime" or "DateTimeOffset" => "datetime-local",
            "TimeOnly" => "time",
            "byte" or "short" or "int" or "long" or "float" or "double" or "decimal" => "number",
            _ => "text"
        };
    }
}
public sealed partial record ApiWorkflowAction(string Action, string From, string To, IReadOnlyList<string> Roles);
public sealed partial record ApiResource(string Key, string DisplayName, string Route, string PrimaryKey, string DeleteMode, bool CanDelete, bool HardDeleteRequiresConfirm, bool SupportsBulkActions, int DefaultPageSize, int MaxPageSize, string WorkflowStatusField, IReadOnlyList<ApiWorkflowAction> WorkflowActions, IReadOnlyList<ApiField> Fields)
{
    public bool UsesSoftDelete => string.Equals(DeleteMode, "Soft", StringComparison.OrdinalIgnoreCase);
    public string DeleteLabel => UsesSoftDelete ? "Soft delete" : "Hard delete";
    public string DeleteSuccessMessage => UsesSoftDelete ? "Soft delete request completed." : "Hard delete request completed.";
    public bool HasWorkflow => !string.IsNullOrWhiteSpace(WorkflowStatusField) && WorkflowActions.Count > 0;
}
public sealed partial record LookupOption(string Value, string Label);
public sealed partial record FieldInputModel(ApiField Field, IReadOnlyList<LookupOption> LookupOptions, string Prefix);

public static partial class ApiCatalog
{
    public const bool HasAuthentication = false;
    public const bool RequireAuthenticationForMenus = true;
    public const bool IncludeSecurityPages = false;
    public const string MenuStyle = "Sidebar";
    public const bool IsTopNavigation = false;
    public const bool ShowAuditFields = false;
    public const bool HasPermissionManagement = false;
    public static IReadOnlyList<ApiResource> Resources { get; } = new[]
    {
    new ApiResource(
        "DowntimeEvent",
        "DowntimeEvent",
        "api/downtimeEvents",
        "DowntimeEventId",
        "Soft",
        true,
        true,
        true,
        25,
        200,
        "",
        Array.Empty<ApiWorkflowAction>(),
        new[]
        {
            new ApiField("DowntimeEventId", "long", true, false, "number", "", "", "", false, ""),
            new ApiField("EquipmentId", "int", false, false, "number", "", "", "", false, ""),
            new ApiField("WorkOrderId", "long", false, true, "number", "", "", "", false, ""),
            new ApiField("StartedAt", "DateTime", false, false, "datetime-local", "", "", "", false, ""),
            new ApiField("EndedAt", "DateTime", false, true, "datetime-local", "", "", "", false, ""),
            new ApiField("ReasonCategory", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("ReasonDetail", "string", false, true, "text", "", "", "", false, ""),
            new ApiField("IsPlanned", "bool", false, false, "checkbox", "", "", "", false, ""),
            new ApiField("DowntimeHours", "decimal", false, true, "number", "", "", "", false, "")
        }),
    new ApiResource(
        "Employee",
        "Employee",
        "api/employees",
        "EmployeeId",
        "Soft",
        true,
        true,
        true,
        25,
        200,
        "",
        Array.Empty<ApiWorkflowAction>(),
        new[]
        {
            new ApiField("EmployeeId", "int", true, false, "number", "", "", "", false, ""),
            new ApiField("SiteId", "int", false, false, "number", "", "", "", false, ""),
            new ApiField("EmployeeCode", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("FullName", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("RoleName", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("LicenseClass", "string", false, true, "text", "", "", "", false, ""),
            new ApiField("Phone", "string", false, true, "text", "", "", "", false, ""),
            new ApiField("Email", "string", false, true, "text", "", "", "", false, ""),
            new ApiField("IsActive", "bool", false, false, "checkbox", "", "", "", false, ""),
            new ApiField("CreatedAt", "DateTime", false, false, "datetime-local", "", "", "", true, "createdOn")
        }),
    new ApiResource(
        "Equipment",
        "Equipment",
        "api/equipments",
        "EquipmentId",
        "Soft",
        true,
        true,
        true,
        25,
        200,
        "",
        Array.Empty<ApiWorkflowAction>(),
        new[]
        {
            new ApiField("EquipmentId", "int", true, false, "number", "", "", "", false, ""),
            new ApiField("SiteId", "int", false, false, "number", "", "", "", false, ""),
            new ApiField("EquipmentClassId", "int", false, false, "number", "", "", "", false, ""),
            new ApiField("AssetTag", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("SerialNumber", "string", false, true, "text", "", "", "", false, ""),
            new ApiField("Manufacturer", "string", false, true, "text", "", "", "", false, ""),
            new ApiField("Model", "string", false, true, "text", "", "", "", false, ""),
            new ApiField("CommissionDate", "DateTime", false, true, "datetime-local", "", "", "", false, ""),
            new ApiField("FuelTypeId", "int", false, false, "number", "", "", "", false, ""),
            new ApiField("TankCapacityL", "decimal", false, true, "number", "", "", "", false, ""),
            new ApiField("CurrentHourMeter", "decimal", false, false, "number", "", "", "", false, ""),
            new ApiField("CurrentOdometerKm", "decimal", false, true, "number", "", "", "", false, ""),
            new ApiField("Status", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("IsActive", "bool", false, false, "checkbox", "", "", "", false, ""),
            new ApiField("CreatedAt", "DateTime", false, false, "datetime-local", "", "", "", true, "createdOn")
        }),
    new ApiResource(
        "EquipmentClass",
        "EquipmentClass",
        "api/equipmentClass",
        "EquipmentClassId",
        "Soft",
        true,
        true,
        true,
        25,
        200,
        "",
        Array.Empty<ApiWorkflowAction>(),
        new[]
        {
            new ApiField("EquipmentClassId", "int", true, false, "number", "", "", "", false, ""),
            new ApiField("ClassCode", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("ClassName", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("CategoryName", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("TypicalPayloadTonnes", "decimal", false, true, "number", "", "", "", false, ""),
            new ApiField("DefaultFuelBurnLph", "decimal", false, true, "number", "", "", "", false, ""),
            new ApiField("MaintenanceIntervalHours", "decimal", false, false, "number", "", "", "", false, ""),
            new ApiField("OilIntervalHours", "decimal", false, false, "number", "", "", "", false, "")
        }),
    new ApiResource(
        "FluidSample",
        "FluidSample",
        "api/fluidSamples",
        "FluidSampleId",
        "Soft",
        true,
        true,
        true,
        25,
        200,
        "",
        Array.Empty<ApiWorkflowAction>(),
        new[]
        {
            new ApiField("FluidSampleId", "long", true, false, "number", "", "", "", false, ""),
            new ApiField("EquipmentId", "int", false, false, "number", "", "", "", false, ""),
            new ApiField("FluidTypeId", "int", false, false, "number", "", "", "", false, ""),
            new ApiField("SampledAt", "DateTime", false, false, "datetime-local", "", "", "", false, ""),
            new ApiField("HourMeter", "decimal", false, false, "number", "", "", "", false, ""),
            new ApiField("LabReference", "string", false, true, "text", "", "", "", false, ""),
            new ApiField("IronPpm", "decimal", false, true, "number", "", "", "", false, ""),
            new ApiField("CopperPpm", "decimal", false, true, "number", "", "", "", false, ""),
            new ApiField("SiliconPpm", "decimal", false, true, "number", "", "", "", false, ""),
            new ApiField("ViscosityCst", "decimal", false, true, "number", "", "", "", false, ""),
            new ApiField("WaterPercent", "decimal", false, true, "number", "", "", "", false, ""),
            new ApiField("Severity", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("Recommendation", "string", false, true, "text", "", "", "", false, "")
        }),
    new ApiResource(
        "FluidService",
        "FluidService",
        "api/fluidServices",
        "FluidServiceId",
        "Soft",
        true,
        true,
        true,
        25,
        200,
        "",
        Array.Empty<ApiWorkflowAction>(),
        new[]
        {
            new ApiField("FluidServiceId", "long", true, false, "number", "", "", "", false, ""),
            new ApiField("EquipmentId", "int", false, false, "number", "", "", "", false, ""),
            new ApiField("FluidTypeId", "int", false, false, "number", "", "", "", false, ""),
            new ApiField("ServicedAt", "DateTime", false, false, "datetime-local", "", "", "", false, ""),
            new ApiField("HourMeter", "decimal", false, false, "number", "", "", "", false, ""),
            new ApiField("LitersChanged", "decimal", false, false, "number", "", "", "", false, ""),
            new ApiField("FilterChanged", "bool", false, false, "checkbox", "", "", "", false, ""),
            new ApiField("WorkOrderId", "long", false, true, "number", "", "", "", false, ""),
            new ApiField("TechnicianEmployeeId", "int", false, true, "number", "", "", "", false, ""),
            new ApiField("NextDueHourMeter", "decimal", false, true, "number", "", "", "", false, ""),
            new ApiField("Notes", "string", false, true, "text", "", "", "", false, "")
        }),
    new ApiResource(
        "FluidType",
        "FluidType",
        "api/fluidTypes",
        "FluidTypeId",
        "Soft",
        true,
        true,
        true,
        25,
        200,
        "",
        Array.Empty<ApiWorkflowAction>(),
        new[]
        {
            new ApiField("FluidTypeId", "int", true, false, "number", "", "", "", false, ""),
            new ApiField("FluidCode", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("FluidName", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("FluidCategory", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("DefaultIntervalHours", "decimal", false, true, "number", "", "", "", false, ""),
            new ApiField("IsActive", "bool", false, false, "checkbox", "", "", "", false, "")
        }),
    new ApiResource(
        "FuelLog",
        "FuelLog",
        "api/fuelLogs",
        "FuelLogId",
        "Soft",
        true,
        true,
        true,
        25,
        200,
        "",
        Array.Empty<ApiWorkflowAction>(),
        new[]
        {
            new ApiField("FuelLogId", "long", true, false, "number", "", "", "", false, ""),
            new ApiField("EquipmentId", "int", false, false, "number", "", "", "", false, ""),
            new ApiField("FuelTypeId", "int", false, false, "number", "", "", "", false, ""),
            new ApiField("FueledAt", "DateTime", false, false, "datetime-local", "", "", "", false, ""),
            new ApiField("ShiftId", "int", false, true, "number", "", "", "", false, ""),
            new ApiField("EmployeeId", "int", false, true, "number", "", "", "", false, ""),
            new ApiField("PitId", "int", false, true, "number", "", "", "", false, ""),
            new ApiField("HourMeter", "decimal", false, false, "number", "", "", "", false, ""),
            new ApiField("OdometerKm", "decimal", false, true, "number", "", "", "", false, ""),
            new ApiField("Liters", "decimal", false, false, "number", "", "", "", false, ""),
            new ApiField("UnitCost", "decimal", false, true, "number", "", "", "", false, ""),
            new ApiField("HoursSinceLastFuel", "decimal", false, true, "number", "", "", "", false, ""),
            new ApiField("FuelBurnLph", "decimal", false, true, "number", "", "", "", false, ""),
            new ApiField("Co2KgPerL", "decimal", false, false, "number", "", "", "", false, ""),
            new ApiField("SourceName", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("Notes", "string", false, true, "text", "", "", "", false, ""),
            new ApiField("CreatedAt", "DateTime", false, false, "datetime-local", "", "", "", true, "createdOn"),
            new ApiField("CostAmount", "decimal", false, true, "number", "", "", "", false, ""),
            new ApiField("Co2Kg", "decimal", false, true, "number", "", "", "", false, "")
        }),
    new ApiResource(
        "FuelType",
        "FuelType",
        "api/fuelTypes",
        "FuelTypeId",
        "Soft",
        true,
        true,
        true,
        25,
        200,
        "",
        Array.Empty<ApiWorkflowAction>(),
        new[]
        {
            new ApiField("FuelTypeId", "int", true, false, "number", "", "", "", false, ""),
            new ApiField("FuelCode", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("FuelName", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("EnergyDensityMjPerL", "decimal", false, true, "number", "", "", "", false, ""),
            new ApiField("Co2KgPerL", "decimal", false, true, "number", "", "", "", false, ""),
            new ApiField("IsActive", "bool", false, false, "checkbox", "", "", "", false, "")
        }),
    new ApiResource(
        "HaulCycle",
        "HaulCycle",
        "api/haulCycles",
        "HaulCycleId",
        "Soft",
        true,
        true,
        true,
        25,
        200,
        "",
        Array.Empty<ApiWorkflowAction>(),
        new[]
        {
            new ApiField("HaulCycleId", "long", true, false, "number", "", "", "", false, ""),
            new ApiField("EquipmentId", "int", false, false, "number", "", "", "", false, ""),
            new ApiField("OperatorEmployeeId", "int", false, true, "number", "", "", "", false, ""),
            new ApiField("ShiftId", "int", false, true, "number", "", "", "", false, ""),
            new ApiField("PitId", "int", false, true, "number", "", "", "", false, ""),
            new ApiField("MaterialId", "int", false, false, "number", "", "", "", false, ""),
            new ApiField("CycleStartedAt", "DateTime", false, false, "datetime-local", "", "", "", false, ""),
            new ApiField("CycleEndedAt", "DateTime", false, false, "datetime-local", "", "", "", false, ""),
            new ApiField("LoadedTonnes", "decimal", false, false, "number", "", "", "", false, ""),
            new ApiField("DistanceKm", "decimal", false, true, "number", "", "", "", false, ""),
            new ApiField("FuelLitersEstimated", "decimal", false, true, "number", "", "", "", false, ""),
            new ApiField("TonnesPerHour", "decimal", false, true, "number", "", "", "", false, ""),
            new ApiField("CycleMinutes", "decimal", false, true, "number", "", "", "", false, ""),
            new ApiField("TonnesKm", "decimal", false, true, "number", "", "", "", false, "")
        }),
    new ApiResource(
        "MaintenancePlan",
        "MaintenancePlan",
        "api/maintenancePlans",
        "MaintenancePlanId",
        "Soft",
        true,
        true,
        true,
        25,
        200,
        "",
        Array.Empty<ApiWorkflowAction>(),
        new[]
        {
            new ApiField("MaintenancePlanId", "int", true, false, "number", "", "", "", false, ""),
            new ApiField("EquipmentClassId", "int", false, false, "number", "", "", "", false, ""),
            new ApiField("PlanCode", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("PlanName", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("IntervalHours", "decimal", false, true, "number", "", "", "", false, ""),
            new ApiField("IntervalDays", "int", false, true, "number", "", "", "", false, ""),
            new ApiField("EstimatedDurationHours", "decimal", false, false, "number", "", "", "", false, ""),
            new ApiField("IsActive", "bool", false, false, "checkbox", "", "", "", false, "")
        }),
    new ApiResource(
        "Material",
        "Material",
        "api/materials",
        "MaterialId",
        "Soft",
        true,
        true,
        true,
        25,
        200,
        "",
        Array.Empty<ApiWorkflowAction>(),
        new[]
        {
            new ApiField("MaterialId", "int", true, false, "number", "", "", "", false, ""),
            new ApiField("MaterialCode", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("MaterialName", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("DensityTonnesPerM3", "decimal", false, true, "number", "", "", "", false, ""),
            new ApiField("IsOre", "bool", false, false, "checkbox", "", "", "", false, "")
        }),
    new ApiResource(
        "MeterReading",
        "MeterReading",
        "api/meterReadings",
        "MeterReadingId",
        "Soft",
        true,
        true,
        true,
        25,
        200,
        "",
        Array.Empty<ApiWorkflowAction>(),
        new[]
        {
            new ApiField("MeterReadingId", "long", true, false, "number", "", "", "", false, ""),
            new ApiField("EquipmentId", "int", false, false, "number", "", "", "", false, ""),
            new ApiField("ReadingAt", "DateTime", false, false, "datetime-local", "", "", "", false, ""),
            new ApiField("HourMeter", "decimal", false, false, "number", "", "", "", false, ""),
            new ApiField("OdometerKm", "decimal", false, true, "number", "", "", "", false, ""),
            new ApiField("SourceName", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("RecordedByEmployeeId", "int", false, true, "number", "", "", "", false, ""),
            new ApiField("Notes", "string", false, true, "text", "", "", "", false, ""),
            new ApiField("CreatedAt", "DateTime", false, false, "datetime-local", "", "", "", true, "createdOn")
        }),
    new ApiResource(
        "Part",
        "Part",
        "api/parts",
        "PartId",
        "Soft",
        true,
        true,
        true,
        25,
        200,
        "",
        Array.Empty<ApiWorkflowAction>(),
        new[]
        {
            new ApiField("PartId", "int", true, false, "number", "", "", "", false, ""),
            new ApiField("PartNumber", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("PartName", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("PartCategory", "string", false, true, "text", "", "", "", false, ""),
            new ApiField("UnitOfMeasure", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("StandardCost", "decimal", false, true, "number", "", "", "", false, ""),
            new ApiField("ReorderPoint", "decimal", false, false, "number", "", "", "", false, ""),
            new ApiField("OnHandQuantity", "decimal", false, false, "number", "", "", "", false, "")
        }),
    new ApiResource(
        "Pit",
        "Pit",
        "api/pits",
        "PitId",
        "Soft",
        true,
        true,
        true,
        25,
        200,
        "",
        Array.Empty<ApiWorkflowAction>(),
        new[]
        {
            new ApiField("PitId", "int", true, false, "number", "", "", "", false, ""),
            new ApiField("SiteId", "int", false, false, "number", "", "", "", false, ""),
            new ApiField("PitCode", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("PitName", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("BenchElevationM", "decimal", false, true, "number", "", "", "", false, ""),
            new ApiField("IsActive", "bool", false, false, "checkbox", "", "", "", false, "")
        }),
    new ApiResource(
        "Shift",
        "Shift",
        "api/shifts",
        "ShiftId",
        "Soft",
        true,
        true,
        true,
        25,
        200,
        "",
        Array.Empty<ApiWorkflowAction>(),
        new[]
        {
            new ApiField("ShiftId", "int", true, false, "number", "", "", "", false, ""),
            new ApiField("SiteId", "int", false, false, "number", "", "", "", false, ""),
            new ApiField("ShiftCode", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("ShiftName", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("StartTime", "TimeSpan", false, false, "text", "", "", "", false, ""),
            new ApiField("EndTime", "TimeSpan", false, false, "text", "", "", "", false, ""),
            new ApiField("PlannedHours", "decimal", false, false, "number", "", "", "", false, "")
        }),
    new ApiResource(
        "Site",
        "Site",
        "api/sites",
        "SiteId",
        "Soft",
        true,
        true,
        true,
        25,
        200,
        "",
        Array.Empty<ApiWorkflowAction>(),
        new[]
        {
            new ApiField("SiteId", "int", true, false, "number", "", "", "", false, ""),
            new ApiField("SiteCode", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("SiteName", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("Country", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("Region", "string", false, true, "text", "", "", "", false, ""),
            new ApiField("TimeZoneName", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("IsActive", "bool", false, false, "checkbox", "", "", "", false, ""),
            new ApiField("CreatedAt", "DateTime", false, false, "datetime-local", "", "", "", true, "createdOn")
        }),
    new ApiResource(
        "TireInspection",
        "TireInspection",
        "api/tireInspections",
        "TireInspectionId",
        "Soft",
        true,
        true,
        true,
        25,
        200,
        "",
        Array.Empty<ApiWorkflowAction>(),
        new[]
        {
            new ApiField("TireInspectionId", "long", true, false, "number", "", "", "", false, ""),
            new ApiField("TireInstallationId", "long", false, false, "number", "", "", "", false, ""),
            new ApiField("InspectedAt", "DateTime", false, false, "datetime-local", "", "", "", false, ""),
            new ApiField("HourMeter", "decimal", false, false, "number", "", "", "", false, ""),
            new ApiField("TreadDepthMm", "decimal", false, false, "number", "", "", "", false, ""),
            new ApiField("PressureKpa", "decimal", false, true, "number", "", "", "", false, ""),
            new ApiField("TemperatureC", "decimal", false, true, "number", "", "", "", false, ""),
            new ApiField("ConditionRating", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("Notes", "string", false, true, "text", "", "", "", false, "")
        }),
    new ApiResource(
        "TireInstallation",
        "TireInstallation",
        "api/tireInstallations",
        "TireInstallationId",
        "Soft",
        true,
        true,
        true,
        25,
        200,
        "",
        Array.Empty<ApiWorkflowAction>(),
        new[]
        {
            new ApiField("TireInstallationId", "long", true, false, "number", "", "", "", false, ""),
            new ApiField("TireId", "int", false, false, "number", "", "", "", false, ""),
            new ApiField("EquipmentId", "int", false, false, "number", "", "", "", false, ""),
            new ApiField("PositionCode", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("InstalledAt", "DateTime", false, false, "datetime-local", "", "", "", false, ""),
            new ApiField("RemovedAt", "DateTime", false, true, "datetime-local", "", "", "", false, ""),
            new ApiField("InstallHourMeter", "decimal", false, false, "number", "", "", "", false, ""),
            new ApiField("RemoveHourMeter", "decimal", false, true, "number", "", "", "", false, ""),
            new ApiField("RemovalReason", "string", false, true, "text", "", "", "", false, "")
        }),
    new ApiResource(
        "TireInventory",
        "TireInventory",
        "api/tireInventories",
        "TireId",
        "Soft",
        true,
        true,
        true,
        25,
        200,
        "",
        Array.Empty<ApiWorkflowAction>(),
        new[]
        {
            new ApiField("TireId", "int", true, false, "number", "", "", "", false, ""),
            new ApiField("TireSerialNumber", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("Manufacturer", "string", false, true, "text", "", "", "", false, ""),
            new ApiField("TireSize", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("TireType", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("PurchaseDate", "DateTime", false, true, "datetime-local", "", "", "", false, ""),
            new ApiField("PurchaseCost", "decimal", false, true, "number", "", "", "", false, ""),
            new ApiField("OriginalTreadDepthMm", "decimal", false, false, "number", "", "", "", false, ""),
            new ApiField("Status", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("CreatedAt", "DateTime", false, false, "datetime-local", "", "", "", true, "createdOn")
        }),
    new ApiResource(
        "WorkOrder",
        "WorkOrder",
        "api/workOrders",
        "WorkOrderId",
        "Soft",
        true,
        true,
        true,
        25,
        200,
        "",
        Array.Empty<ApiWorkflowAction>(),
        new[]
        {
            new ApiField("WorkOrderId", "long", true, false, "number", "", "", "", false, ""),
            new ApiField("WorkOrderNumber", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("EquipmentId", "int", false, false, "number", "", "", "", false, ""),
            new ApiField("MaintenancePlanId", "int", false, true, "number", "", "", "", false, ""),
            new ApiField("OpenedAt", "DateTime", false, false, "datetime-local", "", "", "", false, ""),
            new ApiField("ClosedAt", "DateTime", false, true, "datetime-local", "", "", "", false, ""),
            new ApiField("PriorityName", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("WorkOrderType", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("Status", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("OpenHourMeter", "decimal", false, false, "number", "", "", "", false, ""),
            new ApiField("CloseHourMeter", "decimal", false, true, "number", "", "", "", false, ""),
            new ApiField("ProblemDescription", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("CorrectiveAction", "string", false, true, "text", "", "", "", false, ""),
            new ApiField("LaborHours", "decimal", false, false, "number", "", "", "", false, ""),
            new ApiField("EstimatedCost", "decimal", false, true, "number", "", "", "", false, ""),
            new ApiField("ActualCost", "decimal", false, true, "number", "", "", "", false, ""),
            new ApiField("CreatedByEmployeeId", "int", false, true, "number", "", "", "", false, ""),
            new ApiField("ClosedByEmployeeId", "int", false, true, "number", "", "", "", false, ""),
            new ApiField("DowntimeHours", "decimal", false, true, "number", "", "", "", false, "")
        }),
    new ApiResource(
        "WorkOrderPart",
        "WorkOrderPart",
        "api/workOrderParts",
        "WorkOrderPartId",
        "Soft",
        true,
        true,
        true,
        25,
        200,
        "",
        Array.Empty<ApiWorkflowAction>(),
        new[]
        {
            new ApiField("WorkOrderPartId", "long", true, false, "number", "", "", "", false, ""),
            new ApiField("WorkOrderId", "long", false, false, "number", "", "", "", false, ""),
            new ApiField("PartId", "int", false, false, "number", "", "", "", false, ""),
            new ApiField("QuantityUsed", "decimal", false, false, "number", "", "", "", false, ""),
            new ApiField("UnitCost", "decimal", false, false, "number", "", "", "", false, ""),
            new ApiField("LineCost", "decimal", false, true, "number", "", "", "", false, "")
        }),
    new ApiResource(
        "WorkOrderTask",
        "WorkOrderTask",
        "api/workOrderTasks",
        "WorkOrderTaskId",
        "Soft",
        true,
        true,
        true,
        25,
        200,
        "",
        Array.Empty<ApiWorkflowAction>(),
        new[]
        {
            new ApiField("WorkOrderTaskId", "long", true, false, "number", "", "", "", false, ""),
            new ApiField("WorkOrderId", "long", false, false, "number", "", "", "", false, ""),
            new ApiField("TaskSequence", "int", false, false, "number", "", "", "", false, ""),
            new ApiField("TaskDescription", "string", false, false, "text", "", "", "", false, ""),
            new ApiField("IsCompleted", "bool", false, false, "checkbox", "", "", "", false, ""),
            new ApiField("CompletedAt", "DateTime", false, true, "datetime-local", "", "", "", false, ""),
            new ApiField("CompletedByEmployeeId", "int", false, true, "number", "", "", "", false, "")
        })
    };
    public static IReadOnlyList<ApiResource> WorkflowResources { get; } = Resources.Where(resource => resource.HasWorkflow).ToList();
    public static bool HasWorkflowResources => WorkflowResources.Count > 0;

    public static ApiResource? Find(string key) =>
        Resources.FirstOrDefault(resource => string.Equals(resource.Key, key, StringComparison.OrdinalIgnoreCase));
}