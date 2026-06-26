using MiningFleetOps.Domain.Auditing;
using MiningFleetOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MiningFleetOps.Infrastructure.Persistence;

public sealed partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<DowntimeEvent> DowntimeEvents => Set<DowntimeEvent>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Equipment> Equipments => Set<Equipment>();
    public DbSet<EquipmentClass> EquipmentClass => Set<EquipmentClass>();
    public DbSet<FluidSample> FluidSamples => Set<FluidSample>();
    public DbSet<FluidService> FluidServices => Set<FluidService>();
    public DbSet<FluidType> FluidTypes => Set<FluidType>();
    public DbSet<FuelLog> FuelLogs => Set<FuelLog>();
    public DbSet<FuelType> FuelTypes => Set<FuelType>();
    public DbSet<HaulCycle> HaulCycles => Set<HaulCycle>();
    public DbSet<MaintenancePlan> MaintenancePlans => Set<MaintenancePlan>();
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<MeterReading> MeterReadings => Set<MeterReading>();
    public DbSet<Part> Parts => Set<Part>();
    public DbSet<Pit> Pits => Set<Pit>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<TireInspection> TireInspections => Set<TireInspection>();
    public DbSet<TireInstallation> TireInstallations => Set<TireInstallation>();
    public DbSet<TireInventory> TireInventories => Set<TireInventory>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<WorkOrderPart> WorkOrderParts => Set<WorkOrderPart>();
    public DbSet<WorkOrderTask> WorkOrderTasks => Set<WorkOrderTask>();
    public DbSet<AuditTrailEntry> AuditTrailEntries => Set<AuditTrailEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        modelBuilder.Entity<AuditTrailEntry>().ToTable("AuditTrailEntries");
        modelBuilder.Entity<AuditTrailEntry>().HasIndex(x => new { x.Resource, x.ResourceKey, x.OccurredAtUtc });


        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
