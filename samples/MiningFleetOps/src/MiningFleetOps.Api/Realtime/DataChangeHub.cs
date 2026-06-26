
using Microsoft.AspNetCore.SignalR;

namespace MiningFleetOps.Api.Realtime;


public sealed class DataChangeHub : Hub
{
    public const string Route = "/hubs/data-changes";
    public const string DataChangedMethod = "DataChanged";
}

public sealed record DataChangeNotification(
    string Resource,
    string Action,
    string? ResourceKey,
    DateTimeOffset OccurredAtUtc);