param(
    [string]$Output = "database/update-database.sql"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Output) | Out-Null

dotnet tool restore
dotnet ef migrations script --idempotent `
    --project "src/MiningFleetOps.Infrastructure/MiningFleetOps.Infrastructure.csproj" `
    --startup-project "src/MiningFleetOps.Api/MiningFleetOps.Api.csproj" `
    --context "AppDbContext" `
    --output $Output