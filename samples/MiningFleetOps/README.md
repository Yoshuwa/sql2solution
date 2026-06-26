# MiningFleetOps

Generated ASP.NET Core Web API solution for a mining fleet operations domain, including API, application, domain, infrastructure, Razor Pages client, Blazor client, tests, scripts, and screenshots.

This sample was generated with SQL2Solution, which was used to turn the database model into the complete .NET solution structure shown here.

## Screenshots

- [Screenshot 1](<Screenshots/SQL to Code Generator aspcore api crud swagger multitenant saas (1).png>)
- [Screenshot 2](<Screenshots/SQL to Code Generator aspcore api crud swagger multitenant saas (2).png>)
- [Screenshot 3](<Screenshots/SQL to Code Generator aspcore api crud swagger multitenant saas (3).png>)
- [Screenshot 4](<Screenshots/SQL to Code Generator aspcore api crud swagger multitenant saas (4).png>)
- [Screenshot 5](<Screenshots/SQL to Code Generator aspcore api crud swagger multitenant saas (5).png>)
- [Screenshot 6](<Screenshots/SQL to Code Generator aspcore api crud swagger multitenant saas (6).png>)

## Run

```powershell
dotnet restore
dotnet run --project src/MiningFleetOps.Api/MiningFleetOps.Api.csproj
```

In another terminal, point the generated client appsettings to the API URL, then run one of the generated clients:

```powershell
dotnet run --project src/MiningFleetOps.Client/MiningFleetOps.Client.csproj
dotnet run --project src/MiningFleetOps.Blazor/MiningFleetOps.Blazor.csproj
```

Leave `ConnectionStrings:DefaultConnection` empty to use the built-in in-memory database, or set it to a SQL Server connection string.

## Projects

- `MiningFleetOps.Api`: controllers and startup
- `MiningFleetOps.Application`: DTOs and shared contracts
- `MiningFleetOps.Domain`: generated entities
- `MiningFleetOps.Infrastructure`: EF Core DbContext in `Persistence/AppDbContext.cs`
- `MiningFleetOps.Client`: generated Razor Pages UI that consumes the API with dynamic resource menus and auth/tenant screens when enabled
- `MiningFleetOps.Blazor`: generated Blazor Web App client for browsing resources and calling the API

## Extending The Starter

This solution is intended to be a starter, not a closed generated artifact. See `docs/extending-the-starter.md` for the recommended places to add custom features, services, controllers, pages, integrations, and EF model configuration.

Generated entities, DTOs, controllers, and `AppDbContext` are partial so you can add companion files without editing every generated file directly. Register custom services from `src/MiningFleetOps.Api/CompositionRoot/CustomServiceRegistration.cs`.

Generated entities: 23
