# EF Core Migrations

Run these commands from the solution root.

```powershell
dotnet tool restore
dotnet ef migrations add InitialCreate --project src/MiningFleetOps.Infrastructure/MiningFleetOps.Infrastructure.csproj --startup-project src/MiningFleetOps.Api/MiningFleetOps.Api.csproj --context AppDbContext --output-dir Persistence/Migrations
dotnet ef database update --project src/MiningFleetOps.Infrastructure/MiningFleetOps.Infrastructure.csproj --startup-project src/MiningFleetOps.Api/MiningFleetOps.Api.csproj --context AppDbContext
dotnet ef migrations script --idempotent --project src/MiningFleetOps.Infrastructure/MiningFleetOps.Infrastructure.csproj --startup-project src/MiningFleetOps.Api/MiningFleetOps.Api.csproj --context AppDbContext --output database/update-database.sql
```