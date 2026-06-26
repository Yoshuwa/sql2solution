/*
     demo data load for MiningFleetOpsDB.

    Run with:
      sqlcmd -S "localhost\SQLEXPRESS" -E -b -i .\mining_fleet_ops_seed_data.sql
*/

USE MiningFleetOpsDB;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;

DECLARE @SiteId int = (SELECT SiteId FROM mining.Site WHERE SiteCode = N'MINE-01');
DECLARE @DieselId int = (SELECT FuelTypeId FROM mining.FuelType WHERE FuelCode = N'DIESEL');
DECLARE @TruckClassId int = (SELECT EquipmentClassId FROM mining.EquipmentClass WHERE ClassCode = N'HT-240T');
DECLARE @ExcavatorClassId int = (SELECT EquipmentClassId FROM mining.EquipmentClass WHERE ClassCode = N'EX-550T');
DECLARE @DozerClassId int = (SELECT EquipmentClassId FROM mining.EquipmentClass WHERE ClassCode = N'DZ-D10');
DECLARE @DayShiftId int = (SELECT ShiftId FROM mining.Shift WHERE SiteId = @SiteId AND ShiftCode = N'DAY');
DECLARE @NightShiftId int = (SELECT ShiftId FROM mining.Shift WHERE SiteId = @SiteId AND ShiftCode = N'NIGHT');
DECLARE @PitAId int = (SELECT PitId FROM mining.Pit WHERE SiteId = @SiteId AND PitCode = N'PIT-A');
DECLARE @PitBId int = (SELECT PitId FROM mining.Pit WHERE SiteId = @SiteId AND PitCode = N'PIT-B');
DECLARE @OreId int = (SELECT MaterialId FROM mining.Material WHERE MaterialCode = N'ORE');
DECLARE @WasteId int = (SELECT MaterialId FROM mining.Material WHERE MaterialCode = N'WASTE');
DECLARE @EngineOilId int = (SELECT FluidTypeId FROM mining.FluidType WHERE FluidCode = N'ENG-15W40');
DECLARE @HydOilId int = (SELECT FluidTypeId FROM mining.FluidType WHERE FluidCode = N'HYD-AW68');
DECLARE @GearOilId int = (SELECT FluidTypeId FROM mining.FluidType WHERE FluidCode = N'GEAR-80W90');

IF @SiteId IS NULL OR @DieselId IS NULL OR @TruckClassId IS NULL OR @ExcavatorClassId IS NULL OR @DozerClassId IS NULL
BEGIN
    THROW 53000, 'Required MiningFleetOpsDB reference data is missing. Run mining_fleet_ops_schema.sql first.', 1;
END;

DECLARE @DemoAssets TABLE (AssetTag nvarchar(40) NOT NULL PRIMARY KEY);
INSERT INTO @DemoAssets (AssetTag)
VALUES
    (N'HT-001'), (N'HT-002'), (N'HT-003'), (N'HT-004'), (N'HT-005'), (N'HT-006'),
    (N'EX-001'), (N'EX-002'),
    (N'DZ-001'), (N'DZ-002');

DECLARE @DemoEquipment TABLE (EquipmentId int NOT NULL PRIMARY KEY);
INSERT INTO @DemoEquipment (EquipmentId)
SELECT EquipmentId
FROM mining.Equipment
WHERE SiteId = @SiteId
  AND AssetTag IN (SELECT AssetTag FROM @DemoAssets);

DELETE ti
FROM mining.TireInspection ti
INNER JOIN mining.TireInstallation inst ON inst.TireInstallationId = ti.TireInstallationId
INNER JOIN @DemoEquipment de ON de.EquipmentId = inst.EquipmentId;

DELETE inst
FROM mining.TireInstallation inst
INNER JOIN @DemoEquipment de ON de.EquipmentId = inst.EquipmentId;

DELETE FROM mining.TireInventory WHERE TireSerialNumber LIKE N'DEMO-TIRE-%';

DELETE wop
FROM mining.WorkOrderPart wop
INNER JOIN mining.WorkOrder wo ON wo.WorkOrderId = wop.WorkOrderId
WHERE wo.WorkOrderNumber LIKE N'DEMO-WO-%';

DELETE wot
FROM mining.WorkOrderTask wot
INNER JOIN mining.WorkOrder wo ON wo.WorkOrderId = wot.WorkOrderId
WHERE wo.WorkOrderNumber LIKE N'DEMO-WO-%';

DELETE fs
FROM mining.FluidSample fs
INNER JOIN @DemoEquipment de ON de.EquipmentId = fs.EquipmentId;

DELETE fs
FROM mining.FluidService fs
INNER JOIN @DemoEquipment de ON de.EquipmentId = fs.EquipmentId;

DELETE d
FROM mining.DowntimeEvent d
INNER JOIN @DemoEquipment de ON de.EquipmentId = d.EquipmentId;

DELETE h
FROM mining.HaulCycle h
INNER JOIN @DemoEquipment de ON de.EquipmentId = h.EquipmentId;

DELETE fl
FROM mining.FuelLog fl
INNER JOIN @DemoEquipment de ON de.EquipmentId = fl.EquipmentId;

DELETE mr
FROM mining.MeterReading mr
INNER JOIN @DemoEquipment de ON de.EquipmentId = mr.EquipmentId;

DELETE FROM mining.WorkOrder WHERE WorkOrderNumber LIKE N'DEMO-WO-%';

MERGE mining.Employee AS target
USING (VALUES
    (@SiteId, N'OP-1002', N'Maria Santos', N'Operator', N'Heavy Vehicle'),
    (@SiteId, N'OP-1003', N'Noah Carter', N'Operator', N'Heavy Vehicle'),
    (@SiteId, N'OP-1004', N'Priya Nair', N'Operator', N'Heavy Vehicle'),
    (@SiteId, N'OP-1005', N'Luis Almeida', N'Operator', N'Heavy Vehicle'),
    (@SiteId, N'OP-1006', N'Emma Novak', N'Operator', N'Heavy Vehicle'),
    (@SiteId, N'OP-1007', N'Chen Wei', N'Operator', N'Excavator'),
    (@SiteId, N'OP-1008', N'Sofia Martins', N'Operator', N'Dozer'),
    (@SiteId, N'MT-2002', N'Casey Morgan', N'Maintenance Technician', NULL),
    (@SiteId, N'MT-2003', N'Ana Oliveira', N'Maintenance Technician', NULL),
    (@SiteId, N'MT-2004', N'Diego Costa', N'Tire Technician', NULL),
    (@SiteId, N'SUP-3001', N'Riley Brooks', N'Shift Supervisor', NULL),
    (@SiteId, N'PLN-4001', N'Taylor Kim', N'Maintenance Planner', NULL)
) AS source (SiteId, EmployeeCode, FullName, RoleName, LicenseClass)
ON target.SiteId = source.SiteId AND target.EmployeeCode = source.EmployeeCode
WHEN MATCHED THEN
    UPDATE SET FullName = source.FullName, RoleName = source.RoleName, LicenseClass = source.LicenseClass, IsActive = 1
WHEN NOT MATCHED THEN
    INSERT (SiteId, EmployeeCode, FullName, RoleName, LicenseClass)
    VALUES (source.SiteId, source.EmployeeCode, source.FullName, source.RoleName, source.LicenseClass);

MERGE mining.Part AS target
USING (VALUES
    (N'FILT-ENG-793', N'Engine oil filter kit - haul truck', N'Filter', N'KIT', 420.00, 4.00, 18.00),
    (N'FILT-FUEL-793', N'Primary fuel filter - haul truck', N'Filter', N'EA', 115.00, 8.00, 40.00),
    (N'PAD-BRAKE-793', N'Brake pad set - haul truck', N'Brake', N'SET', 1850.00, 2.00, 7.00),
    (N'SENSOR-TEMP-CAT', N'Engine coolant temperature sensor', N'Electrical', N'EA', 220.00, 4.00, 12.00),
    (N'HOSE-HYD-2IN', N'2 inch hydraulic hose assembly', N'Hydraulic', N'EA', 315.00, 6.00, 22.00),
    (N'BUCKET-TOOTH-EX', N'Excavator bucket tooth', N'Ground Engaging Tool', N'EA', 95.00, 30.00, 145.00)
) AS source (PartNumber, PartName, PartCategory, UnitOfMeasure, StandardCost, ReorderPoint, OnHandQuantity)
ON target.PartNumber = source.PartNumber
WHEN MATCHED THEN
    UPDATE SET PartName = source.PartName, PartCategory = source.PartCategory, UnitOfMeasure = source.UnitOfMeasure,
               StandardCost = source.StandardCost, ReorderPoint = source.ReorderPoint, OnHandQuantity = source.OnHandQuantity
WHEN NOT MATCHED THEN
    INSERT (PartNumber, PartName, PartCategory, UnitOfMeasure, StandardCost, ReorderPoint, OnHandQuantity)
    VALUES (source.PartNumber, source.PartName, source.PartCategory, source.UnitOfMeasure, source.StandardCost, source.ReorderPoint, source.OnHandQuantity);

MERGE mining.Equipment AS target
USING (VALUES
    (@SiteId, @TruckClassId, N'HT-001', N'SN-HT001', N'Caterpillar', N'793F', CONVERT(date, '2023-01-15'), @DieselId, 5000.00, 1250.00, 18400.00, N'Available'),
    (@SiteId, @TruckClassId, N'HT-002', N'SN-HT002', N'Caterpillar', N'793F', CONVERT(date, '2023-03-02'), @DieselId, 5000.00, 1365.00, 19120.00, N'Available'),
    (@SiteId, @TruckClassId, N'HT-003', N'SN-HT003', N'Komatsu', N'830E-5', CONVERT(date, '2022-11-18'), @DieselId, 4700.00, 2108.00, 24680.00, N'Operating'),
    (@SiteId, @TruckClassId, N'HT-004', N'SN-HT004', N'Komatsu', N'830E-5', CONVERT(date, '2024-02-21'), @DieselId, 4700.00, 875.00, 12840.00, N'Available'),
    (@SiteId, @TruckClassId, N'HT-005', N'SN-HT005', N'Liebherr', N'T 264', CONVERT(date, '2023-08-07'), @DieselId, 4900.00, 1582.00, 20350.00, N'Available'),
    (@SiteId, @TruckClassId, N'HT-006', N'SN-HT006', N'Liebherr', N'T 264', CONVERT(date, '2024-04-14'), @DieselId, 4900.00, 620.00, 7820.00, N'Maintenance'),
    (@SiteId, @ExcavatorClassId, N'EX-001', N'SN-EX001', N'Komatsu', N'PC5500-11', CONVERT(date, '2022-06-20'), @DieselId, 3600.00, 3980.00, NULL, N'Operating'),
    (@SiteId, @ExcavatorClassId, N'EX-002', N'SN-EX002', N'Hitachi', N'EX5600-7', CONVERT(date, '2023-10-04'), @DieselId, 3600.00, 1710.00, NULL, N'Available'),
    (@SiteId, @DozerClassId, N'DZ-001', N'SN-DZ001', N'Caterpillar', N'D10T2', CONVERT(date, '2021-09-10'), @DieselId, 1200.00, 5125.00, 8420.00, N'Available'),
    (@SiteId, @DozerClassId, N'DZ-002', N'SN-DZ002', N'Komatsu', N'D475A-8', CONVERT(date, '2022-12-01'), @DieselId, 1300.00, 3035.00, 4930.00, N'Available')
) AS source (SiteId, EquipmentClassId, AssetTag, SerialNumber, Manufacturer, Model, CommissionDate, FuelTypeId, TankCapacityL, CurrentHourMeter, CurrentOdometerKm, Status)
ON target.SiteId = source.SiteId AND target.AssetTag = source.AssetTag
WHEN MATCHED THEN
    UPDATE SET EquipmentClassId = source.EquipmentClassId, SerialNumber = source.SerialNumber, Manufacturer = source.Manufacturer,
               Model = source.Model, CommissionDate = source.CommissionDate, FuelTypeId = source.FuelTypeId,
               TankCapacityL = source.TankCapacityL, CurrentHourMeter = source.CurrentHourMeter,
               CurrentOdometerKm = source.CurrentOdometerKm, Status = source.Status, IsActive = 1
WHEN NOT MATCHED THEN
    INSERT (SiteId, EquipmentClassId, AssetTag, SerialNumber, Manufacturer, Model, CommissionDate, FuelTypeId, TankCapacityL, CurrentHourMeter, CurrentOdometerKm, Status)
    VALUES (source.SiteId, source.EquipmentClassId, source.AssetTag, source.SerialNumber, source.Manufacturer, source.Model, source.CommissionDate, source.FuelTypeId, source.TankCapacityL, source.CurrentHourMeter, source.CurrentOdometerKm, source.Status);

DELETE FROM @DemoEquipment;
INSERT INTO @DemoEquipment (EquipmentId)
SELECT EquipmentId
FROM mining.Equipment
WHERE SiteId = @SiteId
  AND AssetTag IN (SELECT AssetTag FROM @DemoAssets);

DECLARE @StartDate date = DATEADD(day, -29, CONVERT(date, SYSUTCDATETIME()));
DECLARE @Day int = 0;
DECLARE @AssetTag nvarchar(40);
DECLARE @EquipmentId int;
DECLARE @Category nvarchar(80);
DECLARE @BaseHour decimal(12,2);
DECLARE @BaseOdo decimal(12,2);
DECLARE @LoadTonnes decimal(12,3);
DECLARE @DistanceKm decimal(12,3);
DECLARE @CycleCount int;
DECLARE @ShiftLoop int;
DECLARE @CycleLoop int;
DECLARE @OperatorId int;
DECLARE @ShiftId int;
DECLARE @PitId int;
DECLARE @MaterialId int;
DECLARE @CycleStart datetime2(0);
DECLARE @CycleEnd datetime2(0);
DECLARE @FuelAt datetime2(0);
DECLARE @HourMeter decimal(12,2);
DECLARE @OdometerKm decimal(12,2);
DECLARE @Liters decimal(12,3);
DECLARE @UnitCost decimal(12,4);

WHILE @Day < 30
BEGIN
    DECLARE asset_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT e.AssetTag, e.EquipmentId, ec.CategoryName, e.CurrentHourMeter, ISNULL(e.CurrentOdometerKm, 0)
        FROM mining.Equipment e
        INNER JOIN mining.EquipmentClass ec ON ec.EquipmentClassId = e.EquipmentClassId
        WHERE e.SiteId = @SiteId
          AND e.AssetTag IN (SELECT AssetTag FROM @DemoAssets)
        ORDER BY e.AssetTag;

    OPEN asset_cursor;
    FETCH NEXT FROM asset_cursor INTO @AssetTag, @EquipmentId, @Category, @BaseHour, @BaseOdo;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @ShiftLoop = 0;
        WHILE @ShiftLoop < 2
        BEGIN
            SET @ShiftId = CASE WHEN @ShiftLoop = 0 THEN @DayShiftId ELSE @NightShiftId END;
            SET @OperatorId = (
                SELECT EmployeeId
                FROM mining.Employee
                WHERE SiteId = @SiteId
                  AND EmployeeCode = CONCAT(N'OP-', 1001 + ((@Day + @EquipmentId + @ShiftLoop) % 8))
            );
            SET @PitId = CASE WHEN (@Day + @EquipmentId + @ShiftLoop) % 3 = 0 THEN @PitBId ELSE @PitAId END;
            SET @MaterialId = CASE WHEN (@Day + @EquipmentId + @ShiftLoop) % 4 = 0 THEN @WasteId ELSE @OreId END;

            SET @FuelAt = DATEADD(hour, CASE WHEN @ShiftLoop = 0 THEN 6 ELSE 18 END, CONVERT(datetime2(0), DATEADD(day, @Day, @StartDate)));
            SET @HourMeter = @BaseHour + (@Day * 17.6) + (@ShiftLoop * 8.4) + (@EquipmentId % 7) * 0.3;
            SET @OdometerKm = CASE WHEN @Category = N'Excavator' THEN NULL ELSE @BaseOdo + (@Day * 82.0) + (@ShiftLoop * 38.0) + (@EquipmentId % 5) * 2.5 END;
            SET @Liters = CASE
                WHEN @Category = N'Haul Truck' THEN 1420 + ((@Day * 37 + @EquipmentId * 19 + @ShiftLoop * 71) % 430)
                WHEN @Category = N'Excavator' THEN 880 + ((@Day * 29 + @EquipmentId * 13 + @ShiftLoop * 47) % 310)
                ELSE 390 + ((@Day * 17 + @EquipmentId * 11 + @ShiftLoop * 23) % 170)
            END;
            SET @UnitCost = 1.1900 + (((@Day + @EquipmentId) % 9) * 0.0175);

            EXEC mining.usp_RecordFueling
                @EquipmentId = @EquipmentId,
                @FuelTypeId = @DieselId,
                @FueledAt = @FuelAt,
                @HourMeter = @HourMeter,
                @Liters = @Liters,
                @UnitCost = @UnitCost,
                @OdometerKm = @OdometerKm,
                @ShiftId = @ShiftId,
                @EmployeeId = @OperatorId,
                @PitId = @PitId,
                @SourceName = N'SeedData',
                @Notes = N'Realistic demo fueling load.';

            IF @Category = N'Haul Truck'
            BEGIN
                SET @CycleCount = 9 + ((@Day + @EquipmentId + @ShiftLoop) % 5);
                SET @CycleLoop = 0;
                WHILE @CycleLoop < @CycleCount
                BEGIN
                    SET @CycleStart = DATEADD(minute, CASE WHEN @ShiftLoop = 0 THEN 390 ELSE 1110 END + (@CycleLoop * 48), CONVERT(datetime2(0), DATEADD(day, @Day, @StartDate)));
                    SET @CycleEnd = DATEADD(minute, 31 + ((@CycleLoop + @EquipmentId + @Day) % 10), @CycleStart);
                    SET @LoadTonnes = 218 + ((@EquipmentId * 5 + @Day * 3 + @CycleLoop * 7) % 28);
                    SET @DistanceKm = CONVERT(decimal(12,3), 3.2 + (((@Day + @CycleLoop + @EquipmentId) % 17) * 0.18));

                    INSERT INTO mining.HaulCycle
                        (EquipmentId, OperatorEmployeeId, ShiftId, PitId, MaterialId, CycleStartedAt, CycleEndedAt, LoadedTonnes, DistanceKm, FuelLitersEstimated)
                    VALUES
                        (@EquipmentId, @OperatorId, @ShiftId, @PitId, @MaterialId, @CycleStart, @CycleEnd, @LoadTonnes, @DistanceKm,
                         CONVERT(decimal(12,3), @DistanceKm * 17.5 + (@LoadTonnes * 0.09)));

                    SET @CycleLoop += 1;
                END;
            END;

            SET @ShiftLoop += 1;
        END;

        FETCH NEXT FROM asset_cursor INTO @AssetTag, @EquipmentId, @Category, @BaseHour, @BaseOdo;
    END;

    CLOSE asset_cursor;
    DEALLOCATE asset_cursor;

    SET @Day += 1;
END;

DECLARE @TechId int = (SELECT EmployeeId FROM mining.Employee WHERE SiteId = @SiteId AND EmployeeCode = N'MT-2002');
DECLARE @TireTechId int = (SELECT EmployeeId FROM mining.Employee WHERE SiteId = @SiteId AND EmployeeCode = N'MT-2004');
DECLARE @PlannerId int = (SELECT EmployeeId FROM mining.Employee WHERE SiteId = @SiteId AND EmployeeCode = N'PLN-4001');
DECLARE @Pm250Id int = (SELECT MaintenancePlanId FROM mining.MaintenancePlan WHERE EquipmentClassId = @TruckClassId AND PlanCode = N'PM-250');
DECLARE @Pm500Id int = (SELECT MaintenancePlanId FROM mining.MaintenancePlan WHERE EquipmentClassId = @TruckClassId AND PlanCode = N'PM-500');

DECLARE @ServiceDate datetime2(0);
DECLARE @ServiceHour decimal(12,2);
DECLARE @ServiceLiters decimal(12,3);
DECLARE @HydServiceDate datetime2(0);
DECLARE @HydServiceHour decimal(12,2);
DECLARE @HydServiceLiters decimal(12,3);

DECLARE service_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT e.EquipmentId, e.AssetTag, e.CurrentHourMeter, ec.CategoryName
    FROM mining.Equipment e
    INNER JOIN mining.EquipmentClass ec ON ec.EquipmentClassId = e.EquipmentClassId
    WHERE e.SiteId = @SiteId
      AND e.AssetTag IN (SELECT AssetTag FROM @DemoAssets)
    ORDER BY e.AssetTag;

OPEN service_cursor;
FETCH NEXT FROM service_cursor INTO @EquipmentId, @AssetTag, @BaseHour, @Category;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @ServiceDate = DATEADD(day, -24 + (@EquipmentId % 12), SYSUTCDATETIME());
    SET @ServiceHour = @BaseHour + 62 + (@EquipmentId % 5) * 8;
    SET @ServiceLiters = CASE WHEN @Category = N'Haul Truck' THEN 180.000 WHEN @Category = N'Excavator' THEN 155.000 ELSE 72.000 END;

    EXEC mining.usp_RecordFluidService
        @EquipmentId = @EquipmentId,
        @FluidTypeId = @EngineOilId,
        @ServicedAt = @ServiceDate,
        @HourMeter = @ServiceHour,
        @LitersChanged = @ServiceLiters,
        @FilterChanged = 1,
        @TechnicianEmployeeId = @TechId,
        @Notes = N'Scheduled oil and filter service.';

    IF @EquipmentId % 3 = 0
    BEGIN
        SET @HydServiceDate = DATEADD(day, 4, @ServiceDate);
        SET @HydServiceHour = @ServiceHour + 85;
        SET @HydServiceLiters = CASE WHEN @Category = N'Excavator' THEN 480.000 ELSE 220.000 END;

        EXEC mining.usp_RecordFluidService
            @EquipmentId = @EquipmentId,
            @FluidTypeId = @HydOilId,
            @ServicedAt = @HydServiceDate,
            @HourMeter = @HydServiceHour,
            @LitersChanged = @HydServiceLiters,
            @FilterChanged = 1,
            @TechnicianEmployeeId = @TechId,
            @Notes = N'Hydraulic service after sample recommendation.';
    END;

    INSERT INTO mining.FluidSample
        (EquipmentId, FluidTypeId, SampledAt, HourMeter, LabReference, IronPpm, CopperPpm, SiliconPpm, ViscosityCst, WaterPercent, Severity, Recommendation)
    VALUES
        (@EquipmentId, @EngineOilId, DATEADD(day, 10, @ServiceDate), @ServiceHour + 175,
         CONCAT(N'LAB-', @AssetTag, N'-', FORMAT(@EquipmentId, '000')),
         35 + (@EquipmentId % 6) * 8, 5 + (@EquipmentId % 4) * 2, 12 + (@EquipmentId % 5) * 4, 14.2 + (@EquipmentId % 5) * 0.3,
         CASE WHEN @EquipmentId % 4 = 0 THEN 0.0800 ELSE 0.0200 END,
         CASE WHEN @EquipmentId % 7 = 0 THEN N'Action' WHEN @EquipmentId % 4 = 0 THEN N'Watch' ELSE N'Normal' END,
         CASE WHEN @EquipmentId % 7 = 0 THEN N'Resample in 50 hours and inspect filtration.' WHEN @EquipmentId % 4 = 0 THEN N'Monitor water contamination trend.' ELSE N'Continue normal interval.' END);

    FETCH NEXT FROM service_cursor INTO @EquipmentId, @AssetTag, @BaseHour, @Category;
END;

CLOSE service_cursor;
DEALLOCATE service_cursor;

DECLARE @WoSeq int = 1;
DECLARE @WorkOrderId bigint;
DECLARE @BrakePartId int = (SELECT PartId FROM mining.Part WHERE PartNumber = N'PAD-BRAKE-793');
DECLARE @FilterPartId int = (SELECT PartId FROM mining.Part WHERE PartNumber = N'FILT-ENG-793');
DECLARE @HosePartId int = (SELECT PartId FROM mining.Part WHERE PartNumber = N'HOSE-HYD-2IN');
DECLARE @SensorPartId int = (SELECT PartId FROM mining.Part WHERE PartNumber = N'SENSOR-TEMP-CAT');
DECLARE @ProblemDescription nvarchar(1000);
DECLARE @ClosedAt datetime2(0);
DECLARE @CloseHourMeter decimal(12,2);
DECLARE @ActualCost decimal(18,2);
DECLARE @LaborHours decimal(10,2);

DECLARE wo_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT TOP (10) e.EquipmentId, e.AssetTag, e.CurrentHourMeter
    FROM mining.Equipment e
    WHERE e.SiteId = @SiteId
      AND e.AssetTag IN (SELECT AssetTag FROM @DemoAssets)
    ORDER BY e.AssetTag;

OPEN wo_cursor;
FETCH NEXT FROM wo_cursor INTO @EquipmentId, @AssetTag, @BaseHour;

WHILE @@FETCH_STATUS = 0
BEGIN
    DECLARE @WoNumber nvarchar(40) = CONCAT(N'DEMO-WO-', FORMAT(@WoSeq, '0000'));
    DECLARE @WoType nvarchar(30) = CASE WHEN @WoSeq % 5 = 0 THEN N'Emergency' WHEN @WoSeq % 2 = 0 THEN N'Preventive' ELSE N'Corrective' END;
    DECLARE @Priority nvarchar(20) = CASE WHEN @WoSeq IN (6, 10) THEN N'Critical' WHEN @WoSeq % 3 = 0 THEN N'High' ELSE N'Normal' END;
    DECLARE @PlanId int = CASE WHEN @WoType = N'Preventive' THEN CASE WHEN @WoSeq % 4 = 0 THEN @Pm500Id ELSE @Pm250Id END ELSE NULL END;
    SET @ProblemDescription = CASE
        WHEN @WoType = N'Preventive' THEN N'Scheduled preventive service including inspection, lubrication, and filters.'
        WHEN @WoType = N'Emergency' THEN N'Unplanned stoppage due to hydraulic leak and elevated temperature alarm.'
        ELSE N'Operator reported abnormal vibration, slow response, or fuel burn deviation.'
    END;

    EXEC mining.usp_CreateWorkOrder
        @WorkOrderNumber = @WoNumber,
        @EquipmentId = @EquipmentId,
        @WorkOrderType = @WoType,
        @ProblemDescription = @ProblemDescription,
        @PriorityName = @Priority,
        @MaintenancePlanId = @PlanId,
        @CreatedByEmployeeId = @PlannerId;

    SET @WorkOrderId = (SELECT WorkOrderId FROM mining.WorkOrder WHERE WorkOrderNumber = @WoNumber);

    INSERT INTO mining.WorkOrderTask (WorkOrderId, TaskSequence, TaskDescription, IsCompleted, CompletedAt, CompletedByEmployeeId)
    VALUES
        (@WorkOrderId, 1, N'Lock out equipment and complete safety inspection.', CASE WHEN @WoSeq <= 8 THEN 1 ELSE 0 END, CASE WHEN @WoSeq <= 8 THEN DATEADD(day, -8 + @WoSeq, SYSUTCDATETIME()) ELSE NULL END, CASE WHEN @WoSeq <= 8 THEN @TechId ELSE NULL END),
        (@WorkOrderId, 2, N'Inspect fluid levels, filters, hoses, tires, and braking systems.', CASE WHEN @WoSeq <= 8 THEN 1 ELSE 0 END, CASE WHEN @WoSeq <= 8 THEN DATEADD(day, -8 + @WoSeq, SYSUTCDATETIME()) ELSE NULL END, CASE WHEN @WoSeq <= 8 THEN @TechId ELSE NULL END),
        (@WorkOrderId, 3, N'Return to service after operational test.', CASE WHEN @WoSeq <= 7 THEN 1 ELSE 0 END, CASE WHEN @WoSeq <= 7 THEN DATEADD(day, -7 + @WoSeq, SYSUTCDATETIME()) ELSE NULL END, CASE WHEN @WoSeq <= 7 THEN @TechId ELSE NULL END);

    INSERT INTO mining.WorkOrderPart (WorkOrderId, PartId, QuantityUsed, UnitCost)
    VALUES
        (@WorkOrderId, CASE WHEN @WoSeq % 4 = 0 THEN @BrakePartId WHEN @WoSeq % 3 = 0 THEN @HosePartId WHEN @WoSeq % 5 = 0 THEN @SensorPartId ELSE @FilterPartId END,
         CASE WHEN @WoSeq % 4 = 0 THEN 1.000 ELSE 2.000 END,
         CASE WHEN @WoSeq % 4 = 0 THEN 1850.0000 WHEN @WoSeq % 3 = 0 THEN 315.0000 WHEN @WoSeq % 5 = 0 THEN 220.0000 ELSE 420.0000 END);

    IF @WoSeq <= 8
    BEGIN
        SET @ClosedAt = DATEADD(day, -7 + @WoSeq, SYSUTCDATETIME());
        SET @CloseHourMeter = @BaseHour + 620 + @WoSeq;
        SET @ActualCost = 1250.00 + (@WoSeq * 415.00);
        SET @LaborHours = 4.5 + (@WoSeq % 5) * 1.25;

        EXEC mining.usp_CloseWorkOrder
            @WorkOrderId = @WorkOrderId,
            @ClosedAt = @ClosedAt,
            @CloseHourMeter = @CloseHourMeter,
            @CorrectiveAction = N'Completed inspection, replaced worn components, tested under load, and returned equipment to dispatch.',
            @ActualCost = @ActualCost,
            @LaborHours = @LaborHours,
            @ClosedByEmployeeId = @TechId;
    END
    ELSE
    BEGIN
        UPDATE mining.WorkOrder
           SET Status = CASE WHEN @WoSeq = 9 THEN N'In Progress' ELSE N'Waiting Parts' END,
               LaborHours = 2.5,
               EstimatedCost = CASE WHEN @WoSeq = 9 THEN 2800.00 ELSE 6400.00 END
         WHERE WorkOrderId = @WorkOrderId;

        INSERT INTO mining.DowntimeEvent (EquipmentId, WorkOrderId, StartedAt, ReasonCategory, ReasonDetail, IsPlanned)
        VALUES (@EquipmentId, @WorkOrderId, DATEADD(hour, -18 - @WoSeq, SYSUTCDATETIME()), N'Maintenance', N'Open work order awaiting repair completion.', 0);
    END;

    SET @WoSeq += 1;
    FETCH NEXT FROM wo_cursor INTO @EquipmentId, @AssetTag, @BaseHour;
END;

CLOSE wo_cursor;
DEALLOCATE wo_cursor;

DECLARE @Position TABLE (PositionCode nvarchar(20) NOT NULL PRIMARY KEY, PositionOrdinal int NOT NULL);
INSERT INTO @Position (PositionCode, PositionOrdinal)
VALUES (N'LF', 1), (N'RF', 2), (N'LM', 3), (N'RM', 4), (N'LR', 5), (N'RR', 6);

DECLARE @PositionCode nvarchar(20);
DECLARE @PositionOrdinal int;
DECLARE @TireId int;
DECLARE @InstallId bigint;

DECLARE tire_asset_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT e.EquipmentId, e.AssetTag, e.CurrentHourMeter
    FROM mining.Equipment e
    INNER JOIN mining.EquipmentClass ec ON ec.EquipmentClassId = e.EquipmentClassId
    WHERE e.SiteId = @SiteId
      AND ec.CategoryName = N'Haul Truck'
      AND e.AssetTag IN (SELECT AssetTag FROM @DemoAssets)
    ORDER BY e.AssetTag;

OPEN tire_asset_cursor;
FETCH NEXT FROM tire_asset_cursor INTO @EquipmentId, @AssetTag, @BaseHour;

WHILE @@FETCH_STATUS = 0
BEGIN
    DECLARE position_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT PositionCode, PositionOrdinal FROM @Position ORDER BY PositionOrdinal;
    OPEN position_cursor;
    FETCH NEXT FROM position_cursor INTO @PositionCode, @PositionOrdinal;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        INSERT INTO mining.TireInventory
            (TireSerialNumber, Manufacturer, TireSize, TireType, PurchaseDate, PurchaseCost, OriginalTreadDepthMm, Status)
        VALUES
            (CONCAT(N'DEMO-TIRE-', @AssetTag, N'-', @PositionCode),
             CASE WHEN @PositionOrdinal % 2 = 0 THEN N'Michelin' ELSE N'Bridgestone' END,
             N'40.00R57',
             CASE WHEN @PositionOrdinal <= 2 THEN N'Steer' ELSE N'Drive' END,
             DATEADD(day, -180 - (@PositionOrdinal * 8), CONVERT(date, SYSUTCDATETIME())),
             46500.00 + (@PositionOrdinal * 650.00),
             82.00,
             N'Installed');

        SET @TireId = SCOPE_IDENTITY();

        INSERT INTO mining.TireInstallation
            (TireId, EquipmentId, PositionCode, InstalledAt, InstallHourMeter)
        VALUES
            (@TireId, @EquipmentId, @PositionCode, DATEADD(day, -92 - @PositionOrdinal, SYSUTCDATETIME()), @BaseHour - 520 - (@PositionOrdinal * 12));

        SET @InstallId = SCOPE_IDENTITY();

        INSERT INTO mining.TireInspection
            (TireInstallationId, InspectedAt, HourMeter, TreadDepthMm, PressureKpa, TemperatureC, ConditionRating, Notes)
        VALUES
            (@InstallId, DATEADD(day, -21, SYSUTCDATETIME()), @BaseHour - 170, 72.0 - (@PositionOrdinal * 1.1) - (@EquipmentId % 3), 695.0 + (@PositionOrdinal * 8), 63.0 + (@PositionOrdinal * 1.5), N'Good', N'Routine inspection.'),
            (@InstallId, DATEADD(day, -3, SYSUTCDATETIME()), @BaseHour - 15, 66.0 - (@PositionOrdinal * 1.4) - (@EquipmentId % 4), 690.0 + (@PositionOrdinal * 7), 68.0 + (@PositionOrdinal * 1.6),
             CASE WHEN @PositionOrdinal = 6 AND @EquipmentId % 2 = 0 THEN N'Watch' ELSE N'Good' END,
             CASE WHEN @PositionOrdinal = 6 AND @EquipmentId % 2 = 0 THEN N'Outer shoulder wear trend, rotate at next service.' ELSE N'Tread wear normal.' END);

        FETCH NEXT FROM position_cursor INTO @PositionCode, @PositionOrdinal;
    END;

    CLOSE position_cursor;
    DEALLOCATE position_cursor;

    FETCH NEXT FROM tire_asset_cursor INTO @EquipmentId, @AssetTag, @BaseHour;
END;

CLOSE tire_asset_cursor;
DEALLOCATE tire_asset_cursor;

UPDATE mining.Equipment
   SET Status = N'Maintenance'
 WHERE SiteId = @SiteId
   AND AssetTag = N'HT-006';

UPDATE mining.Equipment
   SET Status = N'Down'
 WHERE SiteId = @SiteId
   AND AssetTag = N'DZ-002';

COMMIT TRANSACTION;

SELECT 'Equipment' AS Entity, COUNT(*) AS CountRows FROM mining.Equipment WHERE SiteId = @SiteId AND AssetTag IN (SELECT AssetTag FROM @DemoAssets)
UNION ALL SELECT 'Employees', COUNT(*) FROM mining.Employee WHERE SiteId = @SiteId
UNION ALL SELECT 'FuelLogs', COUNT(*) FROM mining.FuelLog fl INNER JOIN @DemoEquipment de ON de.EquipmentId = fl.EquipmentId
UNION ALL SELECT 'HaulCycles', COUNT(*) FROM mining.HaulCycle h INNER JOIN @DemoEquipment de ON de.EquipmentId = h.EquipmentId
UNION ALL SELECT 'FluidServices', COUNT(*) FROM mining.FluidService fs INNER JOIN @DemoEquipment de ON de.EquipmentId = fs.EquipmentId
UNION ALL SELECT 'FluidSamples', COUNT(*) FROM mining.FluidSample fs INNER JOIN @DemoEquipment de ON de.EquipmentId = fs.EquipmentId
UNION ALL SELECT 'WorkOrders', COUNT(*) FROM mining.WorkOrder WHERE WorkOrderNumber LIKE N'DEMO-WO-%'
UNION ALL SELECT 'TiresInstalled', COUNT(*) FROM mining.TireInventory WHERE TireSerialNumber LIKE N'DEMO-TIRE-%';
GO

PRINT 'MiningFleetOpsDB realistic demo data loaded successfully.';
GO
