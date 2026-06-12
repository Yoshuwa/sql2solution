param(
    [string]$MigrationName = "InitialCreate"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

dotnet tool restore
dotnet ef migrations add $MigrationName `
    --project "src/AdventureWorksLT2017Api.Infrastructure/AdventureWorksLT2017Api.Infrastructure.csproj" `
    --startup-project "src/AdventureWorksLT2017Api.Api/AdventureWorksLT2017Api.Api.csproj" `
    --context "AppDbContext" `
    --output-dir "Persistence/Migrations"