# EF Core Migrations

Run these commands from the solution root.

```powershell
dotnet tool restore
dotnet ef migrations add InitialCreate --project src/AdventureWorksLT2017Api.Infrastructure/AdventureWorksLT2017Api.Infrastructure.csproj --startup-project src/AdventureWorksLT2017Api.Api/AdventureWorksLT2017Api.Api.csproj --context AppDbContext --output-dir Persistence/Migrations
dotnet ef database update --project src/AdventureWorksLT2017Api.Infrastructure/AdventureWorksLT2017Api.Infrastructure.csproj --startup-project src/AdventureWorksLT2017Api.Api/AdventureWorksLT2017Api.Api.csproj --context AppDbContext
dotnet ef migrations script --idempotent --project src/AdventureWorksLT2017Api.Infrastructure/AdventureWorksLT2017Api.Infrastructure.csproj --startup-project src/AdventureWorksLT2017Api.Api/AdventureWorksLT2017Api.Api.csproj --context AppDbContext --output database/update-database.sql
```