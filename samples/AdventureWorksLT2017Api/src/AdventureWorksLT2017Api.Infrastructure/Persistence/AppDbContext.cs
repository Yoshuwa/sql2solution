using AdventureWorksLT2017Api.Domain.Auditing;
using AdventureWorksLT2017Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AdventureWorksLT2017Api.Infrastructure.Persistence;

public sealed partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();
    public DbSet<Address> Address => Set<Address>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerAddress> CustomerAddress => Set<CustomerAddress>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<ProductDescription> ProductDescriptions => Set<ProductDescription>();
    public DbSet<ProductModel> ProductModels => Set<ProductModel>();
    public DbSet<ProductModelProductDescription> ProductModelProductDescriptions => Set<ProductModelProductDescription>();
    public DbSet<SalesOrderDetail> SalesOrderDetails => Set<SalesOrderDetail>();
    public DbSet<SalesOrderHeader> SalesOrderHeaders => Set<SalesOrderHeader>();
    public DbSet<AuditTrailEntry> AuditTrailEntries => Set<AuditTrailEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        modelBuilder.Entity<AuditTrailEntry>().ToTable("AuditTrailEntries");
        modelBuilder.Entity<AuditTrailEntry>().HasIndex(x => new { x.Resource, x.ResourceKey, x.OccurredAtUtc });

        modelBuilder.Entity<CustomerAddress>().HasKey(x => new { x.CustomerID, x.AddressID });
        modelBuilder.Entity<ProductModelProductDescription>().HasKey(x => new { x.ProductModelID, x.ProductDescriptionID, x.Culture });
        modelBuilder.Entity<SalesOrderDetail>().HasKey(x => new { x.SalesOrderID, x.SalesOrderDetailID });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
