/*
    Mining heavy vehicle maintenance and operations schema for SQL Server Express.
    Target instance: localhost\SQLEXPRESS

    Run with:
      sqlcmd -S "localhost\SQLEXPRESS" -E -b -i .\mining_fleet_ops_schema.sql
*/

IF DB_ID(N'MiningFleetOpsDB') IS NULL
BEGIN
    CREATE DATABASE MiningFleetOpsDB;
END;
GO

USE MiningFleetOpsDB;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'mining')
BEGIN
    EXEC(N'CREATE SCHEMA mining AUTHORIZATION dbo;');
END;
GO

CREATE TABLE mining.Site (
    SiteId              int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Site PRIMARY KEY,
    SiteCode            nvarchar(30) NOT NULL CONSTRAINT UQ_Site_SiteCode UNIQUE,
    SiteName            nvarchar(160) NOT NULL,
    Country             nvarchar(100) NOT NULL,
    Region              nvarchar(100) NULL,
    TimeZoneName        nvarchar(80) NOT NULL CONSTRAINT DF_Site_TimeZoneName DEFAULT N'UTC',
    IsActive            bit NOT NULL CONSTRAINT DF_Site_IsActive DEFAULT 1,
    CreatedAt           datetime2(0) NOT NULL CONSTRAINT DF_Site_CreatedAt DEFAULT SYSUTCDATETIME()
);
GO

CREATE TABLE mining.Pit (
    PitId               int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Pit PRIMARY KEY,
    SiteId              int NOT NULL,
    PitCode             nvarchar(30) NOT NULL,
    PitName             nvarchar(160) NOT NULL,
    BenchElevationM     decimal(10,2) NULL,
    IsActive            bit NOT NULL CONSTRAINT DF_Pit_IsActive DEFAULT 1,
    CONSTRAINT FK_Pit_Site FOREIGN KEY (SiteId) REFERENCES mining.Site(SiteId),
    CONSTRAINT UQ_Pit_SiteCode UNIQUE (SiteId, PitCode)
);
GO

CREATE TABLE mining.Shift (
    ShiftId             int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Shift PRIMARY KEY,
    SiteId              int NOT NULL,
    ShiftCode           nvarchar(30) NOT NULL,
    ShiftName           nvarchar(80) NOT NULL,
    StartTime           time(0) NOT NULL,
    EndTime             time(0) NOT NULL,
    PlannedHours        decimal(5,2) NOT NULL,
    CONSTRAINT FK_Shift_Site FOREIGN KEY (SiteId) REFERENCES mining.Site(SiteId),
    CONSTRAINT UQ_Shift_SiteCode UNIQUE (SiteId, ShiftCode),
    CONSTRAINT CK_Shift_PlannedHours CHECK (PlannedHours > 0 AND PlannedHours <= 24)
);
GO

CREATE TABLE mining.Employee (
    EmployeeId          int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Employee PRIMARY KEY,
    SiteId              int NOT NULL,
    EmployeeCode        nvarchar(40) NOT NULL,
    FullName            nvarchar(160) NOT NULL,
    RoleName            nvarchar(80) NOT NULL,
    LicenseClass        nvarchar(40) NULL,
    Phone               nvarchar(40) NULL,
    Email               nvarchar(254) NULL,
    IsActive            bit NOT NULL CONSTRAINT DF_Employee_IsActive DEFAULT 1,
    CreatedAt           datetime2(0) NOT NULL CONSTRAINT DF_Employee_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Employee_Site FOREIGN KEY (SiteId) REFERENCES mining.Site(SiteId),
    CONSTRAINT UQ_Employee_SiteCode UNIQUE (SiteId, EmployeeCode)
);
GO

CREATE TABLE mining.EquipmentClass (
    EquipmentClassId        int IDENTITY(1,1) NOT NULL CONSTRAINT PK_EquipmentClass PRIMARY KEY,
    ClassCode               nvarchar(40) NOT NULL CONSTRAINT UQ_EquipmentClass_ClassCode UNIQUE,
    ClassName               nvarchar(120) NOT NULL,
    CategoryName            nvarchar(80) NOT NULL,
    TypicalPayloadTonnes    decimal(10,2) NULL,
    DefaultFuelBurnLph      decimal(10,2) NULL,
    MaintenanceIntervalHours decimal(10,2) NOT NULL CONSTRAINT DF_EquipmentClass_MaintInterval DEFAULT 250,
    OilIntervalHours        decimal(10,2) NOT NULL CONSTRAINT DF_EquipmentClass_OilInterval DEFAULT 500,
    CONSTRAINT CK_EquipmentClass_Payload CHECK (TypicalPayloadTonnes IS NULL OR TypicalPayloadTonnes > 0),
    CONSTRAINT CK_EquipmentClass_FuelBurn CHECK (DefaultFuelBurnLph IS NULL OR DefaultFuelBurnLph > 0),
    CONSTRAINT CK_EquipmentClass_Intervals CHECK (MaintenanceIntervalHours > 0 AND OilIntervalHours > 0)
);
GO

CREATE TABLE mining.FuelType (
    FuelTypeId          int IDENTITY(1,1) NOT NULL CONSTRAINT PK_FuelType PRIMARY KEY,
    FuelCode            nvarchar(30) NOT NULL CONSTRAINT UQ_FuelType_FuelCode UNIQUE,
    FuelName            nvarchar(100) NOT NULL,
    EnergyDensityMjPerL decimal(8,3) NULL,
    Co2KgPerL           decimal(8,4) NULL,
    IsActive            bit NOT NULL CONSTRAINT DF_FuelType_IsActive DEFAULT 1,
    CONSTRAINT CK_FuelType_Energy CHECK (EnergyDensityMjPerL IS NULL OR EnergyDensityMjPerL > 0),
    CONSTRAINT CK_FuelType_Co2 CHECK (Co2KgPerL IS NULL OR Co2KgPerL >= 0)
);
GO

CREATE TABLE mining.FluidType (
    FluidTypeId         int IDENTITY(1,1) NOT NULL CONSTRAINT PK_FluidType PRIMARY KEY,
    FluidCode           nvarchar(30) NOT NULL CONSTRAINT UQ_FluidType_FluidCode UNIQUE,
    FluidName           nvarchar(100) NOT NULL,
    FluidCategory       nvarchar(40) NOT NULL,
    DefaultIntervalHours decimal(10,2) NULL,
    IsActive            bit NOT NULL CONSTRAINT DF_FluidType_IsActive DEFAULT 1,
    CONSTRAINT CK_FluidType_Category CHECK (FluidCategory IN (N'Engine Oil', N'Hydraulic Oil', N'Gear Oil', N'Coolant', N'Grease', N'Other')),
    CONSTRAINT CK_FluidType_Interval CHECK (DefaultIntervalHours IS NULL OR DefaultIntervalHours > 0)
);
GO

CREATE TABLE mining.Material (
    MaterialId          int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Material PRIMARY KEY,
    MaterialCode        nvarchar(30) NOT NULL CONSTRAINT UQ_Material_MaterialCode UNIQUE,
    MaterialName        nvarchar(100) NOT NULL,
    DensityTonnesPerM3  decimal(8,3) NULL,
    IsOre               bit NOT NULL CONSTRAINT DF_Material_IsOre DEFAULT 0,
    CONSTRAINT CK_Material_Density CHECK (DensityTonnesPerM3 IS NULL OR DensityTonnesPerM3 > 0)
);
GO

CREATE TABLE mining.Equipment (
    EquipmentId         int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Equipment PRIMARY KEY,
    SiteId              int NOT NULL,
    EquipmentClassId    int NOT NULL,
    AssetTag            nvarchar(40) NOT NULL,
    SerialNumber        nvarchar(80) NULL,
    Manufacturer        nvarchar(80) NULL,
    Model               nvarchar(80) NULL,
    CommissionDate      date NULL,
    FuelTypeId          int NOT NULL,
    TankCapacityL       decimal(10,2) NULL,
    CurrentHourMeter    decimal(12,2) NOT NULL CONSTRAINT DF_Equipment_CurrentHourMeter DEFAULT 0,
    CurrentOdometerKm   decimal(12,2) NULL,
    Status              nvarchar(30) NOT NULL CONSTRAINT DF_Equipment_Status DEFAULT N'Available',
    IsActive            bit NOT NULL CONSTRAINT DF_Equipment_IsActive DEFAULT 1,
    CreatedAt           datetime2(0) NOT NULL CONSTRAINT DF_Equipment_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Equipment_Site FOREIGN KEY (SiteId) REFERENCES mining.Site(SiteId),
    CONSTRAINT FK_Equipment_Class FOREIGN KEY (EquipmentClassId) REFERENCES mining.EquipmentClass(EquipmentClassId),
    CONSTRAINT FK_Equipment_FuelType FOREIGN KEY (FuelTypeId) REFERENCES mining.FuelType(FuelTypeId),
    CONSTRAINT UQ_Equipment_SiteAssetTag UNIQUE (SiteId, AssetTag),
    CONSTRAINT CK_Equipment_Meters CHECK (CurrentHourMeter >= 0 AND (CurrentOdometerKm IS NULL OR CurrentOdometerKm >= 0)),
    CONSTRAINT CK_Equipment_Status CHECK (Status IN (N'Available', N'Operating', N'Down', N'Maintenance', N'Retired')),
    CONSTRAINT CK_Equipment_Tank CHECK (TankCapacityL IS NULL OR TankCapacityL > 0)
);
GO

CREATE TABLE mining.MeterReading (
    MeterReadingId      bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MeterReading PRIMARY KEY,
    EquipmentId         int NOT NULL,
    ReadingAt           datetime2(0) NOT NULL,
    HourMeter           decimal(12,2) NOT NULL,
    OdometerKm          decimal(12,2) NULL,
    SourceName          nvarchar(40) NOT NULL CONSTRAINT DF_MeterReading_Source DEFAULT N'Manual',
    RecordedByEmployeeId int NULL,
    Notes               nvarchar(500) NULL,
    CreatedAt           datetime2(0) NOT NULL CONSTRAINT DF_MeterReading_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_MeterReading_Equipment FOREIGN KEY (EquipmentId) REFERENCES mining.Equipment(EquipmentId),
    CONSTRAINT FK_MeterReading_Employee FOREIGN KEY (RecordedByEmployeeId) REFERENCES mining.Employee(EmployeeId),
    CONSTRAINT UQ_MeterReading_EquipmentAt UNIQUE (EquipmentId, ReadingAt),
    CONSTRAINT CK_MeterReading_Values CHECK (HourMeter >= 0 AND (OdometerKm IS NULL OR OdometerKm >= 0))
);
GO

CREATE TABLE mining.FuelLog (
    FuelLogId           bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_FuelLog PRIMARY KEY,
    EquipmentId         int NOT NULL,
    FuelTypeId          int NOT NULL,
    FueledAt            datetime2(0) NOT NULL,
    ShiftId             int NULL,
    EmployeeId          int NULL,
    PitId               int NULL,
    HourMeter           decimal(12,2) NOT NULL,
    OdometerKm          decimal(12,2) NULL,
    Liters              decimal(12,3) NOT NULL,
    UnitCost            decimal(12,4) NULL,
    CostAmount          AS (CONVERT(decimal(18,4), Liters * ISNULL(UnitCost, 0))),
    HoursSinceLastFuel  decimal(12,2) NULL,
    FuelBurnLph         AS (CASE WHEN HoursSinceLastFuel IS NULL OR HoursSinceLastFuel <= 0 THEN NULL ELSE CONVERT(decimal(12,3), Liters / NULLIF(HoursSinceLastFuel, 0)) END),
    Co2Kg               AS (CONVERT(decimal(18,3), Liters * ISNULL(Co2KgPerL, 0))),
    Co2KgPerL           decimal(8,4) NOT NULL CONSTRAINT DF_FuelLog_Co2KgPerL DEFAULT 0,
    SourceName          nvarchar(40) NOT NULL CONSTRAINT DF_FuelLog_Source DEFAULT N'Manual',
    Notes               nvarchar(500) NULL,
    CreatedAt           datetime2(0) NOT NULL CONSTRAINT DF_FuelLog_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_FuelLog_Equipment FOREIGN KEY (EquipmentId) REFERENCES mining.Equipment(EquipmentId),
    CONSTRAINT FK_FuelLog_FuelType FOREIGN KEY (FuelTypeId) REFERENCES mining.FuelType(FuelTypeId),
    CONSTRAINT FK_FuelLog_Shift FOREIGN KEY (ShiftId) REFERENCES mining.Shift(ShiftId),
    CONSTRAINT FK_FuelLog_Employee FOREIGN KEY (EmployeeId) REFERENCES mining.Employee(EmployeeId),
    CONSTRAINT FK_FuelLog_Pit FOREIGN KEY (PitId) REFERENCES mining.Pit(PitId),
    CONSTRAINT CK_FuelLog_Values CHECK (HourMeter >= 0 AND Liters > 0 AND (OdometerKm IS NULL OR OdometerKm >= 0) AND (UnitCost IS NULL OR UnitCost >= 0) AND (HoursSinceLastFuel IS NULL OR HoursSinceLastFuel >= 0))
);
GO

CREATE TABLE mining.MaintenancePlan (
    MaintenancePlanId   int IDENTITY(1,1) NOT NULL CONSTRAINT PK_MaintenancePlan PRIMARY KEY,
    EquipmentClassId    int NOT NULL,
    PlanCode            nvarchar(40) NOT NULL,
    PlanName            nvarchar(160) NOT NULL,
    IntervalHours       decimal(10,2) NULL,
    IntervalDays        int NULL,
    EstimatedDurationHours decimal(8,2) NOT NULL CONSTRAINT DF_MaintenancePlan_Duration DEFAULT 4,
    IsActive            bit NOT NULL CONSTRAINT DF_MaintenancePlan_IsActive DEFAULT 1,
    CONSTRAINT FK_MaintenancePlan_Class FOREIGN KEY (EquipmentClassId) REFERENCES mining.EquipmentClass(EquipmentClassId),
    CONSTRAINT UQ_MaintenancePlan_ClassCode UNIQUE (EquipmentClassId, PlanCode),
    CONSTRAINT CK_MaintenancePlan_Interval CHECK ((IntervalHours IS NOT NULL AND IntervalHours > 0) OR (IntervalDays IS NOT NULL AND IntervalDays > 0)),
    CONSTRAINT CK_MaintenancePlan_Duration CHECK (EstimatedDurationHours > 0)
);
GO

CREATE TABLE mining.WorkOrder (
    WorkOrderId         bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_WorkOrder PRIMARY KEY,
    WorkOrderNumber     nvarchar(40) NOT NULL CONSTRAINT UQ_WorkOrder_Number UNIQUE,
    EquipmentId         int NOT NULL,
    MaintenancePlanId   int NULL,
    OpenedAt            datetime2(0) NOT NULL CONSTRAINT DF_WorkOrder_OpenedAt DEFAULT SYSUTCDATETIME(),
    ClosedAt            datetime2(0) NULL,
    PriorityName        nvarchar(20) NOT NULL CONSTRAINT DF_WorkOrder_Priority DEFAULT N'Normal',
    WorkOrderType       nvarchar(30) NOT NULL,
    Status              nvarchar(30) NOT NULL CONSTRAINT DF_WorkOrder_Status DEFAULT N'Open',
    OpenHourMeter       decimal(12,2) NOT NULL,
    CloseHourMeter      decimal(12,2) NULL,
    ProblemDescription  nvarchar(1000) NOT NULL,
    CorrectiveAction    nvarchar(1000) NULL,
    LaborHours          decimal(10,2) NOT NULL CONSTRAINT DF_WorkOrder_LaborHours DEFAULT 0,
    EstimatedCost       decimal(18,2) NULL,
    ActualCost          decimal(18,2) NULL,
    CreatedByEmployeeId int NULL,
    ClosedByEmployeeId  int NULL,
    DowntimeHours       AS (CASE WHEN ClosedAt IS NULL THEN NULL ELSE CONVERT(decimal(12,2), DATEDIFF(minute, OpenedAt, ClosedAt) / 60.0) END),
    CONSTRAINT FK_WorkOrder_Equipment FOREIGN KEY (EquipmentId) REFERENCES mining.Equipment(EquipmentId),
    CONSTRAINT FK_WorkOrder_Plan FOREIGN KEY (MaintenancePlanId) REFERENCES mining.MaintenancePlan(MaintenancePlanId),
    CONSTRAINT FK_WorkOrder_CreatedBy FOREIGN KEY (CreatedByEmployeeId) REFERENCES mining.Employee(EmployeeId),
    CONSTRAINT FK_WorkOrder_ClosedBy FOREIGN KEY (ClosedByEmployeeId) REFERENCES mining.Employee(EmployeeId),
    CONSTRAINT CK_WorkOrder_Status CHECK (Status IN (N'Open', N'Planned', N'In Progress', N'Waiting Parts', N'Closed', N'Cancelled')),
    CONSTRAINT CK_WorkOrder_Priority CHECK (PriorityName IN (N'Low', N'Normal', N'High', N'Critical')),
    CONSTRAINT CK_WorkOrder_Type CHECK (WorkOrderType IN (N'Preventive', N'Corrective', N'Inspection', N'Emergency', N'Overhaul')),
    CONSTRAINT CK_WorkOrder_Meters CHECK (OpenHourMeter >= 0 AND (CloseHourMeter IS NULL OR CloseHourMeter >= OpenHourMeter)),
    CONSTRAINT CK_WorkOrder_Costs CHECK (LaborHours >= 0 AND (EstimatedCost IS NULL OR EstimatedCost >= 0) AND (ActualCost IS NULL OR ActualCost >= 0))
);
GO

CREATE TABLE mining.WorkOrderTask (
    WorkOrderTaskId     bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_WorkOrderTask PRIMARY KEY,
    WorkOrderId         bigint NOT NULL,
    TaskSequence        int NOT NULL,
    TaskDescription     nvarchar(500) NOT NULL,
    IsCompleted         bit NOT NULL CONSTRAINT DF_WorkOrderTask_IsCompleted DEFAULT 0,
    CompletedAt         datetime2(0) NULL,
    CompletedByEmployeeId int NULL,
    CONSTRAINT FK_WorkOrderTask_WorkOrder FOREIGN KEY (WorkOrderId) REFERENCES mining.WorkOrder(WorkOrderId),
    CONSTRAINT FK_WorkOrderTask_Employee FOREIGN KEY (CompletedByEmployeeId) REFERENCES mining.Employee(EmployeeId),
    CONSTRAINT UQ_WorkOrderTask_Sequence UNIQUE (WorkOrderId, TaskSequence),
    CONSTRAINT CK_WorkOrderTask_Sequence CHECK (TaskSequence > 0)
);
GO

CREATE TABLE mining.Part (
    PartId              int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Part PRIMARY KEY,
    PartNumber          nvarchar(80) NOT NULL CONSTRAINT UQ_Part_Number UNIQUE,
    PartName            nvarchar(160) NOT NULL,
    PartCategory        nvarchar(80) NULL,
    UnitOfMeasure       nvarchar(20) NOT NULL CONSTRAINT DF_Part_Uom DEFAULT N'EA',
    StandardCost        decimal(18,4) NULL,
    ReorderPoint        decimal(12,2) NOT NULL CONSTRAINT DF_Part_ReorderPoint DEFAULT 0,
    OnHandQuantity      decimal(12,2) NOT NULL CONSTRAINT DF_Part_OnHand DEFAULT 0,
    CONSTRAINT CK_Part_CostStock CHECK ((StandardCost IS NULL OR StandardCost >= 0) AND ReorderPoint >= 0 AND OnHandQuantity >= 0)
);
GO

CREATE TABLE mining.WorkOrderPart (
    WorkOrderPartId     bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_WorkOrderPart PRIMARY KEY,
    WorkOrderId         bigint NOT NULL,
    PartId              int NOT NULL,
    QuantityUsed        decimal(12,3) NOT NULL,
    UnitCost            decimal(18,4) NOT NULL,
    LineCost            AS (CONVERT(decimal(18,4), QuantityUsed * UnitCost)),
    CONSTRAINT FK_WorkOrderPart_WorkOrder FOREIGN KEY (WorkOrderId) REFERENCES mining.WorkOrder(WorkOrderId),
    CONSTRAINT FK_WorkOrderPart_Part FOREIGN KEY (PartId) REFERENCES mining.Part(PartId),
    CONSTRAINT CK_WorkOrderPart_Values CHECK (QuantityUsed > 0 AND UnitCost >= 0)
);
GO

CREATE TABLE mining.TireInventory (
    TireId              int IDENTITY(1,1) NOT NULL CONSTRAINT PK_TireInventory PRIMARY KEY,
    TireSerialNumber    nvarchar(80) NOT NULL CONSTRAINT UQ_TireInventory_Serial UNIQUE,
    Manufacturer        nvarchar(80) NULL,
    TireSize            nvarchar(40) NOT NULL,
    TireType            nvarchar(40) NOT NULL,
    PurchaseDate        date NULL,
    PurchaseCost        decimal(18,2) NULL,
    OriginalTreadDepthMm decimal(8,2) NOT NULL,
    Status              nvarchar(30) NOT NULL CONSTRAINT DF_TireInventory_Status DEFAULT N'In Stock',
    CreatedAt           datetime2(0) NOT NULL CONSTRAINT DF_TireInventory_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_TireInventory_Tread CHECK (OriginalTreadDepthMm > 0),
    CONSTRAINT CK_TireInventory_Cost CHECK (PurchaseCost IS NULL OR PurchaseCost >= 0),
    CONSTRAINT CK_TireInventory_Status CHECK (Status IN (N'In Stock', N'Installed', N'Repair', N'Scrapped'))
);
GO

CREATE TABLE mining.TireInstallation (
    TireInstallationId  bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_TireInstallation PRIMARY KEY,
    TireId              int NOT NULL,
    EquipmentId         int NOT NULL,
    PositionCode        nvarchar(20) NOT NULL,
    InstalledAt         datetime2(0) NOT NULL,
    RemovedAt           datetime2(0) NULL,
    InstallHourMeter    decimal(12,2) NOT NULL,
    RemoveHourMeter     decimal(12,2) NULL,
    RemovalReason       nvarchar(200) NULL,
    CONSTRAINT FK_TireInstallation_Tire FOREIGN KEY (TireId) REFERENCES mining.TireInventory(TireId),
    CONSTRAINT FK_TireInstallation_Equipment FOREIGN KEY (EquipmentId) REFERENCES mining.Equipment(EquipmentId),
    CONSTRAINT CK_TireInstallation_Meters CHECK (InstallHourMeter >= 0 AND (RemoveHourMeter IS NULL OR RemoveHourMeter >= InstallHourMeter)),
    CONSTRAINT CK_TireInstallation_Dates CHECK (RemovedAt IS NULL OR RemovedAt >= InstalledAt)
);
GO

CREATE TABLE mining.TireInspection (
    TireInspectionId    bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_TireInspection PRIMARY KEY,
    TireInstallationId  bigint NOT NULL,
    InspectedAt         datetime2(0) NOT NULL,
    HourMeter           decimal(12,2) NOT NULL,
    TreadDepthMm        decimal(8,2) NOT NULL,
    PressureKpa         decimal(10,2) NULL,
    TemperatureC        decimal(8,2) NULL,
    ConditionRating     nvarchar(20) NOT NULL,
    Notes               nvarchar(500) NULL,
    CONSTRAINT FK_TireInspection_Installation FOREIGN KEY (TireInstallationId) REFERENCES mining.TireInstallation(TireInstallationId),
    CONSTRAINT CK_TireInspection_Values CHECK (HourMeter >= 0 AND TreadDepthMm >= 0 AND (PressureKpa IS NULL OR PressureKpa > 0)),
    CONSTRAINT CK_TireInspection_Rating CHECK (ConditionRating IN (N'Good', N'Watch', N'Critical', N'Failed'))
);
GO

CREATE TABLE mining.FluidService (
    FluidServiceId      bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_FluidService PRIMARY KEY,
    EquipmentId         int NOT NULL,
    FluidTypeId         int NOT NULL,
    ServicedAt          datetime2(0) NOT NULL,
    HourMeter           decimal(12,2) NOT NULL,
    LitersChanged       decimal(12,3) NOT NULL,
    FilterChanged       bit NOT NULL CONSTRAINT DF_FluidService_FilterChanged DEFAULT 0,
    WorkOrderId         bigint NULL,
    TechnicianEmployeeId int NULL,
    NextDueHourMeter    decimal(12,2) NULL,
    Notes               nvarchar(500) NULL,
    CONSTRAINT FK_FluidService_Equipment FOREIGN KEY (EquipmentId) REFERENCES mining.Equipment(EquipmentId),
    CONSTRAINT FK_FluidService_FluidType FOREIGN KEY (FluidTypeId) REFERENCES mining.FluidType(FluidTypeId),
    CONSTRAINT FK_FluidService_WorkOrder FOREIGN KEY (WorkOrderId) REFERENCES mining.WorkOrder(WorkOrderId),
    CONSTRAINT FK_FluidService_Technician FOREIGN KEY (TechnicianEmployeeId) REFERENCES mining.Employee(EmployeeId),
    CONSTRAINT CK_FluidService_Values CHECK (HourMeter >= 0 AND LitersChanged > 0 AND (NextDueHourMeter IS NULL OR NextDueHourMeter > HourMeter))
);
GO

CREATE TABLE mining.FluidSample (
    FluidSampleId       bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_FluidSample PRIMARY KEY,
    EquipmentId         int NOT NULL,
    FluidTypeId         int NOT NULL,
    SampledAt           datetime2(0) NOT NULL,
    HourMeter           decimal(12,2) NOT NULL,
    LabReference        nvarchar(80) NULL,
    IronPpm             decimal(10,2) NULL,
    CopperPpm           decimal(10,2) NULL,
    SiliconPpm          decimal(10,2) NULL,
    ViscosityCst        decimal(10,2) NULL,
    WaterPercent        decimal(8,4) NULL,
    Severity            nvarchar(20) NOT NULL CONSTRAINT DF_FluidSample_Severity DEFAULT N'Normal',
    Recommendation      nvarchar(500) NULL,
    CONSTRAINT FK_FluidSample_Equipment FOREIGN KEY (EquipmentId) REFERENCES mining.Equipment(EquipmentId),
    CONSTRAINT FK_FluidSample_FluidType FOREIGN KEY (FluidTypeId) REFERENCES mining.FluidType(FluidTypeId),
    CONSTRAINT CK_FluidSample_Meter CHECK (HourMeter >= 0),
    CONSTRAINT CK_FluidSample_Severity CHECK (Severity IN (N'Normal', N'Watch', N'Action', N'Critical'))
);
GO

CREATE TABLE mining.DowntimeEvent (
    DowntimeEventId     bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_DowntimeEvent PRIMARY KEY,
    EquipmentId         int NOT NULL,
    WorkOrderId         bigint NULL,
    StartedAt           datetime2(0) NOT NULL,
    EndedAt             datetime2(0) NULL,
    ReasonCategory      nvarchar(60) NOT NULL,
    ReasonDetail        nvarchar(200) NULL,
    IsPlanned           bit NOT NULL CONSTRAINT DF_DowntimeEvent_IsPlanned DEFAULT 0,
    DowntimeHours       AS (CASE WHEN EndedAt IS NULL THEN NULL ELSE CONVERT(decimal(12,2), DATEDIFF(minute, StartedAt, EndedAt) / 60.0) END),
    CONSTRAINT FK_DowntimeEvent_Equipment FOREIGN KEY (EquipmentId) REFERENCES mining.Equipment(EquipmentId),
    CONSTRAINT FK_DowntimeEvent_WorkOrder FOREIGN KEY (WorkOrderId) REFERENCES mining.WorkOrder(WorkOrderId),
    CONSTRAINT CK_DowntimeEvent_Dates CHECK (EndedAt IS NULL OR EndedAt >= StartedAt)
);
GO

CREATE TABLE mining.HaulCycle (
    HaulCycleId         bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_HaulCycle PRIMARY KEY,
    EquipmentId         int NOT NULL,
    OperatorEmployeeId  int NULL,
    ShiftId             int NULL,
    PitId               int NULL,
    MaterialId          int NOT NULL,
    CycleStartedAt      datetime2(0) NOT NULL,
    CycleEndedAt        datetime2(0) NOT NULL,
    LoadedTonnes        decimal(12,3) NOT NULL,
    DistanceKm          decimal(12,3) NULL,
    FuelLitersEstimated decimal(12,3) NULL,
    CycleMinutes        AS (CONVERT(decimal(12,2), DATEDIFF(second, CycleStartedAt, CycleEndedAt) / 60.0)),
    TonnesPerHour       AS (CASE WHEN DATEDIFF(second, CycleStartedAt, CycleEndedAt) <= 0 THEN NULL ELSE CONVERT(decimal(12,3), LoadedTonnes / NULLIF(DATEDIFF(second, CycleStartedAt, CycleEndedAt) / 3600.0, 0)) END),
    TonnesKm            AS (CASE WHEN DistanceKm IS NULL THEN NULL ELSE CONVERT(decimal(18,3), LoadedTonnes * DistanceKm) END),
    CONSTRAINT FK_HaulCycle_Equipment FOREIGN KEY (EquipmentId) REFERENCES mining.Equipment(EquipmentId),
    CONSTRAINT FK_HaulCycle_Operator FOREIGN KEY (OperatorEmployeeId) REFERENCES mining.Employee(EmployeeId),
    CONSTRAINT FK_HaulCycle_Shift FOREIGN KEY (ShiftId) REFERENCES mining.Shift(ShiftId),
    CONSTRAINT FK_HaulCycle_Pit FOREIGN KEY (PitId) REFERENCES mining.Pit(PitId),
    CONSTRAINT FK_HaulCycle_Material FOREIGN KEY (MaterialId) REFERENCES mining.Material(MaterialId),
    CONSTRAINT CK_HaulCycle_Values CHECK (CycleEndedAt > CycleStartedAt AND LoadedTonnes > 0 AND (DistanceKm IS NULL OR DistanceKm >= 0) AND (FuelLitersEstimated IS NULL OR FuelLitersEstimated >= 0))
);
GO

CREATE INDEX IX_Equipment_Status ON mining.Equipment (SiteId, Status, IsActive);
CREATE INDEX IX_MeterReading_EquipmentAt ON mining.MeterReading (EquipmentId, ReadingAt DESC) INCLUDE (HourMeter, OdometerKm);
CREATE INDEX IX_FuelLog_EquipmentAt ON mining.FuelLog (EquipmentId, FueledAt DESC) INCLUDE (Liters, HourMeter);
CREATE INDEX IX_WorkOrder_EquipmentStatus ON mining.WorkOrder (EquipmentId, Status, OpenedAt DESC);
CREATE INDEX IX_FluidService_EquipmentFluid ON mining.FluidService (EquipmentId, FluidTypeId, ServicedAt DESC) INCLUDE (HourMeter, NextDueHourMeter);
CREATE INDEX IX_HaulCycle_EquipmentStarted ON mining.HaulCycle (EquipmentId, CycleStartedAt DESC) INCLUDE (LoadedTonnes, DistanceKm);
GO

CREATE OR ALTER TRIGGER mining.trg_MeterReading_UpdateEquipment
ON mining.MeterReading
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        OUTER APPLY (
            SELECT MAX(m.HourMeter) AS PriorHourMeter
            FROM mining.MeterReading m
            WHERE m.EquipmentId = i.EquipmentId
              AND m.ReadingAt < i.ReadingAt
        ) p
        WHERE p.PriorHourMeter IS NOT NULL
          AND i.HourMeter < p.PriorHourMeter
    )
    BEGIN
        THROW 51001, 'Hour meter reading cannot be lower than an earlier reading for the same equipment.', 1;
    END;

    ;WITH Latest AS (
        SELECT EquipmentId, HourMeter, OdometerKm,
               ROW_NUMBER() OVER (PARTITION BY EquipmentId ORDER BY ReadingAt DESC, MeterReadingId DESC) AS rn
        FROM mining.MeterReading
        WHERE EquipmentId IN (SELECT DISTINCT EquipmentId FROM inserted)
    )
    UPDATE e
       SET CurrentHourMeter = l.HourMeter,
           CurrentOdometerKm = COALESCE(l.OdometerKm, e.CurrentOdometerKm)
    FROM mining.Equipment e
    INNER JOIN Latest l ON l.EquipmentId = e.EquipmentId AND l.rn = 1;
END;
GO

CREATE OR ALTER VIEW mining.vw_EquipmentOperationalHealth
AS
SELECT
    e.EquipmentId,
    s.SiteCode,
    e.AssetTag,
    ec.ClassName,
    e.Status,
    e.CurrentHourMeter,
    LastFuel.FueledAt AS LastFueledAt,
    LastFuel.FuelBurnLph AS LastFuelBurnLph,
    Fuel30.TotalLiters30d,
    Fuel30.AvgFuelBurnLph30d,
    WO.OpenWorkOrders,
    WO.CriticalOpenWorkOrders,
    DT.OpenDowntimeStartedAt,
    Oil.LastEngineOilHourMeter,
    Oil.NextEngineOilDueHourMeter,
    CASE
        WHEN e.Status IN (N'Down', N'Maintenance') THEN N'Red'
        WHEN WO.CriticalOpenWorkOrders > 0 THEN N'Red'
        WHEN Oil.NextEngineOilDueHourMeter IS NOT NULL AND e.CurrentHourMeter >= Oil.NextEngineOilDueHourMeter THEN N'Red'
        WHEN Oil.NextEngineOilDueHourMeter IS NOT NULL AND e.CurrentHourMeter >= Oil.NextEngineOilDueHourMeter - 50 THEN N'Amber'
        WHEN LastFuel.FuelBurnLph IS NOT NULL AND ec.DefaultFuelBurnLph IS NOT NULL AND LastFuel.FuelBurnLph > ec.DefaultFuelBurnLph * 1.20 THEN N'Amber'
        ELSE N'Green'
    END AS HealthStatus
FROM mining.Equipment e
INNER JOIN mining.Site s ON s.SiteId = e.SiteId
INNER JOIN mining.EquipmentClass ec ON ec.EquipmentClassId = e.EquipmentClassId
OUTER APPLY (
    SELECT TOP (1) FueledAt, FuelBurnLph
    FROM mining.FuelLog fl
    WHERE fl.EquipmentId = e.EquipmentId
    ORDER BY FueledAt DESC, FuelLogId DESC
) LastFuel
OUTER APPLY (
    SELECT SUM(Liters) AS TotalLiters30d,
           AVG(FuelBurnLph) AS AvgFuelBurnLph30d
    FROM mining.FuelLog fl
    WHERE fl.EquipmentId = e.EquipmentId
      AND fl.FueledAt >= DATEADD(day, -30, SYSUTCDATETIME())
) Fuel30
OUTER APPLY (
    SELECT COUNT(*) AS OpenWorkOrders,
           SUM(CASE WHEN PriorityName = N'Critical' THEN 1 ELSE 0 END) AS CriticalOpenWorkOrders
    FROM mining.WorkOrder wo
    WHERE wo.EquipmentId = e.EquipmentId
      AND wo.Status NOT IN (N'Closed', N'Cancelled')
) WO
OUTER APPLY (
    SELECT TOP (1) StartedAt AS OpenDowntimeStartedAt
    FROM mining.DowntimeEvent d
    WHERE d.EquipmentId = e.EquipmentId
      AND d.EndedAt IS NULL
    ORDER BY StartedAt DESC
) DT
OUTER APPLY (
    SELECT TOP (1)
           fs.HourMeter AS LastEngineOilHourMeter,
           fs.NextDueHourMeter AS NextEngineOilDueHourMeter
    FROM mining.FluidService fs
    INNER JOIN mining.FluidType ft ON ft.FluidTypeId = fs.FluidTypeId
    WHERE fs.EquipmentId = e.EquipmentId
      AND ft.FluidCategory = N'Engine Oil'
    ORDER BY fs.ServicedAt DESC, fs.FluidServiceId DESC
) Oil;
GO

CREATE OR ALTER VIEW mining.vw_MaintenanceDue
AS
SELECT
    e.EquipmentId,
    e.AssetTag,
    ec.ClassName,
    mp.MaintenancePlanId,
    mp.PlanCode,
    mp.PlanName,
    mp.IntervalHours,
    LastClosed.LastClosedAt,
    LastClosed.LastCloseHourMeter,
    DueAtHourMeter = CASE WHEN mp.IntervalHours IS NULL THEN NULL ELSE COALESCE(LastClosed.LastCloseHourMeter, 0) + mp.IntervalHours END,
    HoursRemaining = CASE WHEN mp.IntervalHours IS NULL THEN NULL ELSE COALESCE(LastClosed.LastCloseHourMeter, 0) + mp.IntervalHours - e.CurrentHourMeter END,
    DueStatus = CASE
        WHEN mp.IntervalHours IS NOT NULL AND e.CurrentHourMeter >= COALESCE(LastClosed.LastCloseHourMeter, 0) + mp.IntervalHours THEN N'Overdue'
        WHEN mp.IntervalHours IS NOT NULL AND e.CurrentHourMeter >= COALESCE(LastClosed.LastCloseHourMeter, 0) + mp.IntervalHours - 50 THEN N'Due Soon'
        WHEN LastClosed.LastClosedAt IS NULL THEN N'Never Done'
        ELSE N'OK'
    END
FROM mining.Equipment e
INNER JOIN mining.EquipmentClass ec ON ec.EquipmentClassId = e.EquipmentClassId
INNER JOIN mining.MaintenancePlan mp ON mp.EquipmentClassId = e.EquipmentClassId AND mp.IsActive = 1
OUTER APPLY (
    SELECT TOP (1) wo.ClosedAt AS LastClosedAt, wo.CloseHourMeter AS LastCloseHourMeter
    FROM mining.WorkOrder wo
    WHERE wo.EquipmentId = e.EquipmentId
      AND wo.MaintenancePlanId = mp.MaintenancePlanId
      AND wo.Status = N'Closed'
    ORDER BY wo.ClosedAt DESC, wo.WorkOrderId DESC
) LastClosed
WHERE e.IsActive = 1;
GO

CREATE OR ALTER VIEW mining.vw_FuelEfficiencyDaily
AS
WITH FuelDaily AS (
    SELECT
        EquipmentId,
        CAST(FueledAt AS date) AS ActivityDate,
        SUM(Liters) AS TotalLiters,
        SUM(CostAmount) AS TotalFuelCost,
        SUM(Co2Kg) AS TotalCo2Kg,
        AVG(FuelBurnLph) AS AvgFuelBurnLph
    FROM mining.FuelLog
    GROUP BY EquipmentId, CAST(FueledAt AS date)
),
HaulDaily AS (
    SELECT
        EquipmentId,
        CAST(CycleStartedAt AS date) AS ActivityDate,
        SUM(LoadedTonnes) AS TotalTonnes,
        SUM(TonnesKm) AS TotalTonnesKm
    FROM mining.HaulCycle
    GROUP BY EquipmentId, CAST(CycleStartedAt AS date)
)
SELECT
    e.EquipmentId,
    e.AssetTag,
    fd.ActivityDate AS FuelDate,
    fd.TotalLiters,
    fd.TotalFuelCost,
    fd.TotalCo2Kg,
    fd.AvgFuelBurnLph,
    hd.TotalTonnes,
    hd.TotalTonnesKm,
    CASE WHEN hd.TotalTonnes > 0 THEN CONVERT(decimal(12,4), fd.TotalLiters / hd.TotalTonnes) END AS LitersPerTonne,
    CASE WHEN hd.TotalTonnesKm > 0 THEN CONVERT(decimal(12,4), fd.TotalLiters / hd.TotalTonnesKm) END AS LitersPerTonneKm
FROM mining.Equipment e
INNER JOIN FuelDaily fd ON fd.EquipmentId = e.EquipmentId
LEFT JOIN HaulDaily hd ON hd.EquipmentId = e.EquipmentId AND hd.ActivityDate = fd.ActivityDate;
GO

CREATE OR ALTER VIEW mining.vw_TireCurrentStatus
AS
SELECT
    ti.TireInstallationId,
    tire.TireId,
    tire.TireSerialNumber,
    ti.EquipmentId,
    e.AssetTag,
    ti.PositionCode,
    ti.InstalledAt,
    ti.InstallHourMeter,
    e.CurrentHourMeter,
    TireHours = e.CurrentHourMeter - ti.InstallHourMeter,
    LastInspection.InspectedAt AS LastInspectedAt,
    LastInspection.TreadDepthMm,
    TreadUsedMm = tire.OriginalTreadDepthMm - LastInspection.TreadDepthMm,
    TreadRemainingPct = CASE WHEN tire.OriginalTreadDepthMm > 0 AND LastInspection.TreadDepthMm IS NOT NULL
                             THEN CONVERT(decimal(8,2), LastInspection.TreadDepthMm * 100.0 / tire.OriginalTreadDepthMm) END,
    LastInspection.ConditionRating
FROM mining.TireInstallation ti
INNER JOIN mining.TireInventory tire ON tire.TireId = ti.TireId
INNER JOIN mining.Equipment e ON e.EquipmentId = ti.EquipmentId
OUTER APPLY (
    SELECT TOP (1) tins.InspectedAt, tins.TreadDepthMm, tins.ConditionRating
    FROM mining.TireInspection tins
    WHERE tins.TireInstallationId = ti.TireInstallationId
    ORDER BY tins.InspectedAt DESC, tins.TireInspectionId DESC
) LastInspection
WHERE ti.RemovedAt IS NULL;
GO

CREATE OR ALTER VIEW mining.vw_ShiftProductionKpi
AS
SELECT
    s.SiteCode,
    sh.ShiftCode,
    CAST(h.CycleStartedAt AS date) AS ProductionDate,
    e.AssetTag,
    COUNT_BIG(*) AS CycleCount,
    SUM(h.LoadedTonnes) AS LoadedTonnes,
    SUM(h.DistanceKm) AS DistanceKm,
    SUM(h.TonnesKm) AS TonnesKm,
    AVG(h.CycleMinutes) AS AvgCycleMinutes,
    SUM(h.FuelLitersEstimated) AS EstimatedFuelLiters,
    CASE WHEN SUM(h.FuelLitersEstimated) > 0 THEN CONVERT(decimal(12,3), SUM(h.LoadedTonnes) / SUM(h.FuelLitersEstimated)) END AS TonnesPerFuelLiter
FROM mining.HaulCycle h
INNER JOIN mining.Equipment e ON e.EquipmentId = h.EquipmentId
INNER JOIN mining.Site s ON s.SiteId = e.SiteId
LEFT JOIN mining.Shift sh ON sh.ShiftId = h.ShiftId
GROUP BY s.SiteCode, sh.ShiftCode, CAST(h.CycleStartedAt AS date), e.AssetTag;
GO

CREATE OR ALTER PROCEDURE mining.usp_RecordMeterReading
    @EquipmentId int,
    @ReadingAt datetime2(0),
    @HourMeter decimal(12,2),
    @OdometerKm decimal(12,2) = NULL,
    @SourceName nvarchar(40) = N'API',
    @RecordedByEmployeeId int = NULL,
    @Notes nvarchar(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @HourMeter < 0 OR (@OdometerKm IS NOT NULL AND @OdometerKm < 0)
        THROW 52001, 'Meter values must be zero or greater.', 1;

    IF EXISTS (
        SELECT 1
        FROM mining.MeterReading
        WHERE EquipmentId = @EquipmentId
          AND ReadingAt < @ReadingAt
          AND HourMeter > @HourMeter
    )
        THROW 52002, 'Hour meter is lower than an earlier reading.', 1;

    BEGIN TRANSACTION;

    INSERT INTO mining.MeterReading (EquipmentId, ReadingAt, HourMeter, OdometerKm, SourceName, RecordedByEmployeeId, Notes)
    VALUES (@EquipmentId, @ReadingAt, @HourMeter, @OdometerKm, @SourceName, @RecordedByEmployeeId, @Notes);

    COMMIT TRANSACTION;
END;
GO

CREATE OR ALTER PROCEDURE mining.usp_RecordFueling
    @EquipmentId int,
    @FuelTypeId int,
    @FueledAt datetime2(0),
    @HourMeter decimal(12,2),
    @Liters decimal(12,3),
    @UnitCost decimal(12,4) = NULL,
    @OdometerKm decimal(12,2) = NULL,
    @ShiftId int = NULL,
    @EmployeeId int = NULL,
    @PitId int = NULL,
    @SourceName nvarchar(40) = N'API',
    @Notes nvarchar(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @PriorHourMeter decimal(12,2);
    DECLARE @HoursSinceLastFuel decimal(12,2);
    DECLARE @Co2KgPerL decimal(8,4);

    IF @Liters <= 0
        THROW 52101, 'Fuel liters must be greater than zero.', 1;

    SELECT TOP (1) @PriorHourMeter = HourMeter
    FROM mining.FuelLog
    WHERE EquipmentId = @EquipmentId
      AND FueledAt < @FueledAt
    ORDER BY FueledAt DESC, FuelLogId DESC;

    SET @HoursSinceLastFuel = CASE WHEN @PriorHourMeter IS NULL THEN NULL ELSE @HourMeter - @PriorHourMeter END;

    IF @HoursSinceLastFuel IS NOT NULL AND @HoursSinceLastFuel < 0
        THROW 52102, 'Fuel hour meter cannot be lower than the previous fuel event.', 1;

    SELECT @Co2KgPerL = ISNULL(Co2KgPerL, 0)
    FROM mining.FuelType
    WHERE FuelTypeId = @FuelTypeId;

    BEGIN TRANSACTION;

    INSERT INTO mining.FuelLog
        (EquipmentId, FuelTypeId, FueledAt, ShiftId, EmployeeId, PitId, HourMeter, OdometerKm, Liters, UnitCost, HoursSinceLastFuel, Co2KgPerL, SourceName, Notes)
    VALUES
        (@EquipmentId, @FuelTypeId, @FueledAt, @ShiftId, @EmployeeId, @PitId, @HourMeter, @OdometerKm, @Liters, @UnitCost, @HoursSinceLastFuel, @Co2KgPerL, @SourceName, @Notes);

    EXEC mining.usp_RecordMeterReading
        @EquipmentId = @EquipmentId,
        @ReadingAt = @FueledAt,
        @HourMeter = @HourMeter,
        @OdometerKm = @OdometerKm,
        @SourceName = @SourceName,
        @RecordedByEmployeeId = @EmployeeId,
        @Notes = N'Created from fuel log.';

    COMMIT TRANSACTION;
END;
GO

CREATE OR ALTER PROCEDURE mining.usp_CreateWorkOrder
    @WorkOrderNumber nvarchar(40),
    @EquipmentId int,
    @WorkOrderType nvarchar(30),
    @ProblemDescription nvarchar(1000),
    @PriorityName nvarchar(20) = N'Normal',
    @MaintenancePlanId int = NULL,
    @CreatedByEmployeeId int = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @OpenHourMeter decimal(12,2);
    SELECT @OpenHourMeter = CurrentHourMeter FROM mining.Equipment WHERE EquipmentId = @EquipmentId;

    IF @OpenHourMeter IS NULL
        THROW 52201, 'Equipment was not found.', 1;

    BEGIN TRANSACTION;

    INSERT INTO mining.WorkOrder
        (WorkOrderNumber, EquipmentId, MaintenancePlanId, PriorityName, WorkOrderType, Status, OpenHourMeter, ProblemDescription, CreatedByEmployeeId)
    VALUES
        (@WorkOrderNumber, @EquipmentId, @MaintenancePlanId, @PriorityName, @WorkOrderType, N'Open', @OpenHourMeter, @ProblemDescription, @CreatedByEmployeeId);

    IF @PriorityName = N'Critical'
    BEGIN
        UPDATE mining.Equipment
           SET Status = N'Down'
         WHERE EquipmentId = @EquipmentId
           AND Status <> N'Retired';
    END;

    COMMIT TRANSACTION;
END;
GO

CREATE OR ALTER PROCEDURE mining.usp_CloseWorkOrder
    @WorkOrderId bigint,
    @ClosedAt datetime2(0),
    @CloseHourMeter decimal(12,2),
    @CorrectiveAction nvarchar(1000),
    @ActualCost decimal(18,2) = NULL,
    @LaborHours decimal(10,2) = 0,
    @ClosedByEmployeeId int = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @EquipmentId int;
    DECLARE @OpenHourMeter decimal(12,2);

    SELECT @EquipmentId = EquipmentId, @OpenHourMeter = OpenHourMeter
    FROM mining.WorkOrder
    WHERE WorkOrderId = @WorkOrderId
      AND Status NOT IN (N'Closed', N'Cancelled');

    IF @EquipmentId IS NULL
        THROW 52301, 'Open work order was not found.', 1;

    IF @CloseHourMeter < @OpenHourMeter
        THROW 52302, 'Close hour meter cannot be lower than open hour meter.', 1;

    BEGIN TRANSACTION;

    UPDATE mining.WorkOrder
       SET ClosedAt = @ClosedAt,
           CloseHourMeter = @CloseHourMeter,
           CorrectiveAction = @CorrectiveAction,
           ActualCost = @ActualCost,
           LaborHours = @LaborHours,
           ClosedByEmployeeId = @ClosedByEmployeeId,
           Status = N'Closed'
     WHERE WorkOrderId = @WorkOrderId;

    UPDATE mining.DowntimeEvent
       SET EndedAt = @ClosedAt
     WHERE WorkOrderId = @WorkOrderId
       AND EndedAt IS NULL;

    IF NOT EXISTS (
        SELECT 1 FROM mining.WorkOrder
        WHERE EquipmentId = @EquipmentId
          AND Status IN (N'Open', N'Planned', N'In Progress', N'Waiting Parts')
          AND PriorityName = N'Critical'
    )
    BEGIN
        UPDATE mining.Equipment
           SET Status = N'Available',
               CurrentHourMeter = CASE WHEN CurrentHourMeter < @CloseHourMeter THEN @CloseHourMeter ELSE CurrentHourMeter END
         WHERE EquipmentId = @EquipmentId
           AND Status <> N'Retired';
    END;

    EXEC mining.usp_RecordMeterReading
        @EquipmentId = @EquipmentId,
        @ReadingAt = @ClosedAt,
        @HourMeter = @CloseHourMeter,
        @SourceName = N'WorkOrder',
        @RecordedByEmployeeId = @ClosedByEmployeeId,
        @Notes = N'Created from work order closure.';

    COMMIT TRANSACTION;
END;
GO

CREATE OR ALTER PROCEDURE mining.usp_RecordFluidService
    @EquipmentId int,
    @FluidTypeId int,
    @ServicedAt datetime2(0),
    @HourMeter decimal(12,2),
    @LitersChanged decimal(12,3),
    @FilterChanged bit = 0,
    @WorkOrderId bigint = NULL,
    @TechnicianEmployeeId int = NULL,
    @Notes nvarchar(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @IntervalHours decimal(10,2);
    SELECT @IntervalHours = DefaultIntervalHours FROM mining.FluidType WHERE FluidTypeId = @FluidTypeId;

    INSERT INTO mining.FluidService
        (EquipmentId, FluidTypeId, ServicedAt, HourMeter, LitersChanged, FilterChanged, WorkOrderId, TechnicianEmployeeId, NextDueHourMeter, Notes)
    VALUES
        (@EquipmentId, @FluidTypeId, @ServicedAt, @HourMeter, @LitersChanged, @FilterChanged, @WorkOrderId, @TechnicianEmployeeId,
         CASE WHEN @IntervalHours IS NULL THEN NULL ELSE @HourMeter + @IntervalHours END, @Notes);
END;
GO

MERGE mining.Site AS target
USING (VALUES
    (N'MINE-01', N'Demo Open Pit Mine', N'United States', N'Nevada', N'America/Los_Angeles')
) AS source (SiteCode, SiteName, Country, Region, TimeZoneName)
ON target.SiteCode = source.SiteCode
WHEN NOT MATCHED THEN
    INSERT (SiteCode, SiteName, Country, Region, TimeZoneName)
    VALUES (source.SiteCode, source.SiteName, source.Country, source.Region, source.TimeZoneName);
GO

MERGE mining.FuelType AS target
USING (VALUES
    (N'DIESEL', N'Ultra-low sulfur diesel', 35.800, 2.6800),
    (N'RENEW-DIESEL', N'Renewable diesel', 34.400, 0.7000)
) AS source (FuelCode, FuelName, EnergyDensityMjPerL, Co2KgPerL)
ON target.FuelCode = source.FuelCode
WHEN NOT MATCHED THEN
    INSERT (FuelCode, FuelName, EnergyDensityMjPerL, Co2KgPerL)
    VALUES (source.FuelCode, source.FuelName, source.EnergyDensityMjPerL, source.Co2KgPerL);
GO

MERGE mining.FluidType AS target
USING (VALUES
    (N'ENG-15W40', N'15W-40 engine oil', N'Engine Oil', 500.00),
    (N'HYD-AW68', N'AW 68 hydraulic oil', N'Hydraulic Oil', 2000.00),
    (N'GEAR-80W90', N'80W-90 gear oil', N'Gear Oil', 1000.00),
    (N'COOL-ELC', N'Extended-life coolant', N'Coolant', 4000.00)
) AS source (FluidCode, FluidName, FluidCategory, DefaultIntervalHours)
ON target.FluidCode = source.FluidCode
WHEN NOT MATCHED THEN
    INSERT (FluidCode, FluidName, FluidCategory, DefaultIntervalHours)
    VALUES (source.FluidCode, source.FluidName, source.FluidCategory, source.DefaultIntervalHours);
GO

MERGE mining.Material AS target
USING (VALUES
    (N'ORE', N'Ore', 2.650, 1),
    (N'WASTE', N'Waste rock', 2.400, 0),
    (N'ROM', N'Run-of-mine blend', 2.550, 1)
) AS source (MaterialCode, MaterialName, DensityTonnesPerM3, IsOre)
ON target.MaterialCode = source.MaterialCode
WHEN NOT MATCHED THEN
    INSERT (MaterialCode, MaterialName, DensityTonnesPerM3, IsOre)
    VALUES (source.MaterialCode, source.MaterialName, source.DensityTonnesPerM3, source.IsOre);
GO

MERGE mining.EquipmentClass AS target
USING (VALUES
    (N'HT-240T', N'240 tonne haul truck', N'Haul Truck', 240.00, 185.00, 250.00, 500.00),
    (N'EX-550T', N'550 tonne hydraulic excavator', N'Excavator', NULL, 145.00, 250.00, 500.00),
    (N'DZ-D10', N'D10 class dozer', N'Dozer', NULL, 75.00, 250.00, 500.00)
) AS source (ClassCode, ClassName, CategoryName, TypicalPayloadTonnes, DefaultFuelBurnLph, MaintenanceIntervalHours, OilIntervalHours)
ON target.ClassCode = source.ClassCode
WHEN NOT MATCHED THEN
    INSERT (ClassCode, ClassName, CategoryName, TypicalPayloadTonnes, DefaultFuelBurnLph, MaintenanceIntervalHours, OilIntervalHours)
    VALUES (source.ClassCode, source.ClassName, source.CategoryName, source.TypicalPayloadTonnes, source.DefaultFuelBurnLph, source.MaintenanceIntervalHours, source.OilIntervalHours);
GO

DECLARE @SiteId int = (SELECT SiteId FROM mining.Site WHERE SiteCode = N'MINE-01');
DECLARE @DieselId int = (SELECT FuelTypeId FROM mining.FuelType WHERE FuelCode = N'DIESEL');
DECLARE @TruckClassId int = (SELECT EquipmentClassId FROM mining.EquipmentClass WHERE ClassCode = N'HT-240T');

MERGE mining.Pit AS target
USING (VALUES
    (@SiteId, N'PIT-A', N'North Pit', 1340.00),
    (@SiteId, N'PIT-B', N'South Pit', 1295.00)
) AS source (SiteId, PitCode, PitName, BenchElevationM)
ON target.SiteId = source.SiteId AND target.PitCode = source.PitCode
WHEN NOT MATCHED THEN
    INSERT (SiteId, PitCode, PitName, BenchElevationM)
    VALUES (source.SiteId, source.PitCode, source.PitName, source.BenchElevationM);

MERGE mining.Shift AS target
USING (VALUES
    (@SiteId, N'DAY', N'Day Shift', CONVERT(time(0), '06:00'), CONVERT(time(0), '18:00'), 12.00),
    (@SiteId, N'NIGHT', N'Night Shift', CONVERT(time(0), '18:00'), CONVERT(time(0), '06:00'), 12.00)
) AS source (SiteId, ShiftCode, ShiftName, StartTime, EndTime, PlannedHours)
ON target.SiteId = source.SiteId AND target.ShiftCode = source.ShiftCode
WHEN NOT MATCHED THEN
    INSERT (SiteId, ShiftCode, ShiftName, StartTime, EndTime, PlannedHours)
    VALUES (source.SiteId, source.ShiftCode, source.ShiftName, source.StartTime, source.EndTime, source.PlannedHours);

MERGE mining.Employee AS target
USING (VALUES
    (@SiteId, N'OP-1001', N'Alex Rivera', N'Operator', N'Heavy Vehicle'),
    (@SiteId, N'MT-2001', N'Jordan Lee', N'Maintenance Technician', NULL)
) AS source (SiteId, EmployeeCode, FullName, RoleName, LicenseClass)
ON target.SiteId = source.SiteId AND target.EmployeeCode = source.EmployeeCode
WHEN NOT MATCHED THEN
    INSERT (SiteId, EmployeeCode, FullName, RoleName, LicenseClass)
    VALUES (source.SiteId, source.EmployeeCode, source.FullName, source.RoleName, source.LicenseClass);

MERGE mining.Equipment AS target
USING (VALUES
    (@SiteId, @TruckClassId, N'HT-001', N'SN-HT001', N'Caterpillar', N'793F', CONVERT(date, '2023-01-15'), @DieselId, 5000.00, 1250.00, 18400.00, N'Available')
) AS source (SiteId, EquipmentClassId, AssetTag, SerialNumber, Manufacturer, Model, CommissionDate, FuelTypeId, TankCapacityL, CurrentHourMeter, CurrentOdometerKm, Status)
ON target.SiteId = source.SiteId AND target.AssetTag = source.AssetTag
WHEN NOT MATCHED THEN
    INSERT (SiteId, EquipmentClassId, AssetTag, SerialNumber, Manufacturer, Model, CommissionDate, FuelTypeId, TankCapacityL, CurrentHourMeter, CurrentOdometerKm, Status)
    VALUES (source.SiteId, source.EquipmentClassId, source.AssetTag, source.SerialNumber, source.Manufacturer, source.Model, source.CommissionDate, source.FuelTypeId, source.TankCapacityL, source.CurrentHourMeter, source.CurrentOdometerKm, source.Status);

MERGE mining.MaintenancePlan AS target
USING (VALUES
    (@TruckClassId, N'PM-250', N'250 hour preventive maintenance', 250.00, NULL, 6.00),
    (@TruckClassId, N'PM-500', N'500 hour service with engine oil', 500.00, NULL, 10.00)
) AS source (EquipmentClassId, PlanCode, PlanName, IntervalHours, IntervalDays, EstimatedDurationHours)
ON target.EquipmentClassId = source.EquipmentClassId AND target.PlanCode = source.PlanCode
WHEN NOT MATCHED THEN
    INSERT (EquipmentClassId, PlanCode, PlanName, IntervalHours, IntervalDays, EstimatedDurationHours)
    VALUES (source.EquipmentClassId, source.PlanCode, source.PlanName, source.IntervalHours, source.IntervalDays, source.EstimatedDurationHours);
GO

PRINT 'MiningFleetOpsDB schema deployed successfully.';
GO
