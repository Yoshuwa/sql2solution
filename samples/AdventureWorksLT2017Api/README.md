# AdventureWorksLT2017Api

Generated ASP.NET Core Web API solution.

## Run

```powershell
dotnet restore
dotnet run --project src/AdventureWorksLT2017Api.Api/AdventureWorksLT2017Api.Api.csproj
```

In another terminal, point the generated client appsettings to the API URL, then run one of the generated clients:

```powershell
dotnet run --project src/AdventureWorksLT2017Api.Client/AdventureWorksLT2017Api.Client.csproj
dotnet run --project src/AdventureWorksLT2017Api.Blazor/AdventureWorksLT2017Api.Blazor.csproj
```

Leave `ConnectionStrings:DefaultConnection` empty to use the built-in in-memory database, or set it to a SQL Server connection string.

## Projects

- `AdventureWorksLT2017Api.Api`: controllers and startup
- `AdventureWorksLT2017Api.Application`: DTOs and shared contracts
- `AdventureWorksLT2017Api.Domain`: generated entities
- `AdventureWorksLT2017Api.Infrastructure`: EF Core DbContext in `Persistence/AppDbContext.cs`
- `AdventureWorksLT2017Api.Client`: generated Razor Pages UI that consumes the API with dynamic resource menus and auth/tenant screens when enabled
- `AdventureWorksLT2017Api.Blazor`: generated Blazor Web App client for browsing resources and calling the API

## Extending The Starter

This solution is intended to be a starter, not a closed generated artifact. See `docs/extending-the-starter.md` for the recommended places to add custom features, services, controllers, pages, integrations, and EF model configuration.

Generated entities, DTOs, controllers, and `AppDbContext` are partial so you can add companion files without editing every generated file directly. Register custom services from `src/AdventureWorksLT2017Api.Api/CompositionRoot/CustomServiceRegistration.cs`.

Generated entities: 11