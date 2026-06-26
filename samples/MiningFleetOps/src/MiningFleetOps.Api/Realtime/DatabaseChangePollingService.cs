using MiningFleetOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;

namespace MiningFleetOps.Api.Realtime;

public sealed class DatabaseChangePollingService : BackgroundService
{
    private static readonly IReadOnlyList<WatchedTable> Tables = new WatchedTable[]
    {
        new WatchedTable("[mining].[DowntimeEvent]", "DowntimeEvent"),
        new WatchedTable("[mining].[Employee]", "Employee"),
        new WatchedTable("[mining].[Equipment]", "Equipment"),
        new WatchedTable("[mining].[EquipmentClass]", "EquipmentClass"),
        new WatchedTable("[mining].[FluidSample]", "FluidSample"),
        new WatchedTable("[mining].[FluidService]", "FluidService"),
        new WatchedTable("[mining].[FluidType]", "FluidType"),
        new WatchedTable("[mining].[FuelLog]", "FuelLog"),
        new WatchedTable("[mining].[FuelType]", "FuelType"),
        new WatchedTable("[mining].[HaulCycle]", "HaulCycle"),
        new WatchedTable("[mining].[MaintenancePlan]", "MaintenancePlan"),
        new WatchedTable("[mining].[Material]", "Material"),
        new WatchedTable("[mining].[MeterReading]", "MeterReading"),
        new WatchedTable("[mining].[Part]", "Part"),
        new WatchedTable("[mining].[Pit]", "Pit"),
        new WatchedTable("[mining].[Shift]", "Shift"),
        new WatchedTable("[mining].[Site]", "Site"),
        new WatchedTable("[mining].[TireInspection]", "TireInspection"),
        new WatchedTable("[mining].[TireInstallation]", "TireInstallation"),
        new WatchedTable("[mining].[TireInventory]", "TireInventory"),
        new WatchedTable("[mining].[WorkOrder]", "WorkOrder"),
        new WatchedTable("[mining].[WorkOrderPart]", "WorkOrderPart"),
        new WatchedTable("[mining].[WorkOrderTask]", "WorkOrderTask"),
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<DataChangeHub> _changes;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseChangePollingService> _logger;
    private readonly Dictionary<string, string> _signatures = new(StringComparer.OrdinalIgnoreCase);
    private bool _providerWarningLogged;

    public DatabaseChangePollingService(
        IServiceScopeFactory scopeFactory,
        IHubContext<DataChangeHub> changes,
        IConfiguration configuration,
        ILogger<DatabaseChangePollingService> logger)
    {
        _scopeFactory = scopeFactory;
        _changes = changes;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (Tables.Count == 0)
            return;

        var configuredSeconds = _configuration.GetValue<int?>("SignalR:DatabasePollingSeconds") ?? 5;
        var interval = TimeSpan.FromSeconds(Math.Clamp(configuredSeconds, 1, 300));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SignalR database change polling failed.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var provider = db.Database.ProviderName ?? string.Empty;
        if (!provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            if (!_providerWarningLogged)
            {
                _providerWarningLogged = true;
                _logger.LogInformation("SignalR database polling is enabled only for SQL Server providers. Current provider: {Provider}", provider);
            }

            return;
        }

        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(ct);

        foreach (var table in Tables)
        {
            string signature;
            try
            {
                signature = await ReadTableSignatureAsync(connection, table, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "Could not read realtime signature for {Table}.", table.SqlName);
                continue;
            }

            if (!_signatures.TryGetValue(table.SqlName, out var previous))
            {
                _signatures[table.SqlName] = signature;
                continue;
            }

            if (string.Equals(previous, signature, StringComparison.Ordinal))
                continue;

            _signatures[table.SqlName] = signature;
            await _changes.Clients.All.SendAsync(
                DataChangeHub.DataChangedMethod,
                new DataChangeNotification(table.Resource, "DatabaseChanged", null, DateTimeOffset.UtcNow),
                ct);
        }
    }

    private static async Task<string> ReadTableSignatureAsync(DbConnection connection, WatchedTable table, CancellationToken ct)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT CONVERT(nvarchar(50), COUNT_BIG(*)) + ':' + " +
            "COALESCE(CONVERT(nvarchar(50), CHECKSUM_AGG(BINARY_CHECKSUM(*))), '0') " +
            $"FROM {table.SqlName} WITH (NOLOCK);";
        var value = await command.ExecuteScalarAsync(ct);
        return Convert.ToString(value) ?? string.Empty;
    }

    private sealed record WatchedTable(string SqlName, string Resource);
}