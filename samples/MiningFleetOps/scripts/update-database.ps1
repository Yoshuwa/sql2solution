$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

dotnet tool restore
dotnet ef database update `
    --project "src/MiningFleetOps.Infrastructure/MiningFleetOps.Infrastructure.csproj" `
    --startup-project "src/MiningFleetOps.Api/MiningFleetOps.Api.csproj" `
    --context "AppDbContext"