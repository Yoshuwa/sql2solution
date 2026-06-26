# Extending The Starter

This generated solution is meant to give developers a strong starting point while leaving clear room for custom code. Treat the generated CRUD surface as scaffolding for the first version of the application.

## Recommended Custom-Code Areas

- `src/MiningFleetOps.Api/Controllers/Custom`: workflow-specific controllers and endpoints.
- `src/MiningFleetOps.Application/Features`: commands, queries, validators, orchestration services, and use cases.
- `src/MiningFleetOps.Domain/Services`: domain rules that span multiple entities.
- `src/MiningFleetOps.Infrastructure/Integrations`: external APIs, messaging, storage, background jobs, and adapters.
- `src/MiningFleetOps.Api/CompositionRoot/CustomServiceRegistration.cs`: dependency injection for custom services.

## Partial Extension Points

- Entity classes are generated as `partial`, so add business helpers in companion files beside or near the generated entity.
- DTO records are generated as `partial`, so add validation helpers or metadata without changing constructor shape.
- CRUD controllers are generated as `partial` and include `OnBeforeCreate`, `OnAfterCreate`, `OnBeforeUpdate`, and `OnBeforeDelete` partial hooks.
- `AppDbContext` is generated as `partial` and calls `OnModelCreatingPartial(modelBuilder)` for custom EF configuration.

## Regeneration Guidance

Keep custom features in the folders above when possible. If you intentionally change a generated file, consider moving the custom behavior into a partial companion file first. That keeps future regeneration simpler and makes the starter easier to evolve.

## Client Customization

- Put bespoke Razor Pages in `src/MiningFleetOps.Client/Pages/Custom`.
- Put hand-written UI API clients and adapters in `src/MiningFleetOps.Client/Services/Custom`.
- The Razor Pages client menus come from `Models/ApiCatalog.cs`, so new API resources can be added to the catalog without rewriting layout code.
- Put bespoke Blazor pages and components in `src/MiningFleetOps.Blazor/Components/Pages/Custom`.
- Put hand-written Blazor API clients and adapters in `src/MiningFleetOps.Blazor/Services/Custom`.
- The Blazor client shares the generated `Models/ApiCatalog.cs` shape with the Razor Pages template.

## Next Good Steps

- Add feature services for important business workflows instead of putting all logic in controllers.
- Add validation at the application boundary before data reaches EF Core.
- Replace generated CRUD endpoints with custom endpoints only where the workflow needs richer behavior.
- Add tests around custom features as soon as they become business-critical.

Generated entity count: 23