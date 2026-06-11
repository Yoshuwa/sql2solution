<div align="center">

# sql2solution

### 🚀 SQL Server to professional ASP.NET Core API solutions, faster.

[![Website](https://img.shields.io/badge/Website-sql2solution.com-0f766e?style=for-the-badge)](https://sql2solution.com/)
[![.NET](https://img.shields.io/badge/.NET-8%20%7C%209%20%7C%2010-512BD4?style=for-the-badge&logo=dotnet)](https://sql2solution.com/)
[![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-API%20Generator-2563eb?style=for-the-badge)](https://sql2solution.com/)
[![SQL Server](https://img.shields.io/badge/SQL%20Server-to%20API-cc2927?style=for-the-badge)](https://sql2solution.com/)

**sql2solution** helps developers turn SQL database structure into clean, repeatable, production-minded .NET solutions with APIs, client UI, Swagger documentation, security options, and validation workflows.

Visit the official website: **[https://sql2solution.com/](https://sql2solution.com/)**

</div>

---

## ✨ What Is sql2solution?

sql2solution is a deterministic SQL-to-solution generator for developers and teams building SQL-backed .NET products. It is designed for professional workflows where repetitive API plumbing, Swagger setup, DTO wiring, client screen generation, and validation checks slow down delivery.

Instead of relying on AI-generated source code, sql2solution uses handcrafted deterministic templates so output stays predictable, readable, and easier for developers to extend.

## 🔎 Built For Developers Searching For

- .NET Core API generator
- ASP.NET Core API generator
- SQL Server to API generator
- SQL database to .NET API
- SQL to Swagger / OpenAPI
- SQL Server to Blazor app
- SQL Server to Razor Pages
- Multi-tenant .NET API generator
- Clean architecture .NET generator
- CRUD API generator for .NET

## 🧰 What sql2solution Helps Generate

| Area | Details |
| --- | --- |
| ASP.NET Core APIs | Generate endpoint structure, API contracts, DTO patterns, and documented API surfaces from SQL metadata. |
| SQL Server Workflows | Start from database tables, columns, relationships, keys, indexes, and real schema behavior. |
| Swagger / OpenAPI | Produce API documentation that developers and client teams can inspect immediately. |
| Razor Pages | Generate API-connected Razor client screens for practical admin and business workflows. |
| Blazor UI | Generate Blazor client UI patterns connected to generated API endpoints. |
| Single-Tenant Apps | Build simpler SQL-backed business apps with straightforward identity and data access assumptions. |
| Multi-Tenant Apps | Generate tenant-aware project structure, tenant defaults, authorization patterns, and cross-tenant safety checks. |
| Security Controls | Support authentication, authorization, JWT bearer settings, roles, policies, permissions, and related configuration. |
| Operations | Generate patterns for health checks, structured logging, correlation IDs, problem details, and runtime readiness. |
| Validation | Use smoke-test and back-to-back validation points to confirm APIs, clients, Swagger, and generated output still align. |

## 🛠️ Solution Configuration

The Solution Configuration workflow is where sql2solution turns a SQL Server schema into a controlled .NET generation plan. These walkthroughs show how developers can load a project, validate database metadata, choose architecture, configure API styles, manage DTO rules, and set solution-wide defaults before generating code.

Use the toggles below to explore each configuration area.

<details open>
<summary><strong>🧭 Solution Configuration Overview</strong></summary>

Configure the core solution settings before generation begins: project shape, naming, target framework, API behavior, client output, and generation conventions. This gives teams a repeatable setup for SQL-backed ASP.NET Core API projects instead of rebuilding the same decisions by hand for every database.

**✅ Benefit:** Developers can standardize the generated solution early, reduce setup mistakes, and keep project structure consistent across client work, internal tools, and SaaS products.

![sql2solution .NET Core API generator solution configuration walkthrough](assets/solution-configuration/solution-configuration-dotnet-core-api-generator-sql2solution.gif)

</details>

<details>
<summary><strong>📂 Load An Existing sql2solution Project</strong></summary>

Open an existing sql2solution configuration and continue working from saved project settings. This is useful when a schema changes, a client requests new behavior, or a team wants to regenerate parts of a solution without starting from scratch.

**✅ Benefit:** Saved configurations make the generator practical for real projects that evolve over time, not just one-off demos.

![load existing sql2solution project configuration for SQL Server to .NET API generation](assets/solution-configuration/load-existing-sql2solution-project-configuration.gif)

</details>

<details>
<summary><strong>🧾 SQL Table Entity Loading And Validation</strong></summary>

Load table metadata and validate entities before code generation. sql2solution can surface table structure, fields, keys, relationships, and schema issues so developers can make generation decisions from real database metadata.

**✅ Benefit:** Early validation helps avoid broken generated endpoints, missing relationships, incorrect DTOs, and late surprises after code has already been produced.

![SQL table entity validation before ASP.NET Core API code generation in sql2solution](assets/solution-configuration/sql-table-entity-validation-before-code-generation-sql2solution.gif)

</details>

<details>
<summary><strong>🔌 Manage SQL Server Connection Strings By Environment</strong></summary>

Create and organize multiple SQL Server connection strings for different environments, clients, databases, or deployment targets. Environment-aware configuration keeps development, staging, test, and production settings easier to manage.

**✅ Benefit:** Teams can switch contexts quickly and reduce connection-string mistakes when generating APIs from multiple SQL Server databases.

![manage SQL Server connection strings and environments in sql2solution](assets/solution-configuration/manage-sql-server-connection-strings-environments-sql2solution.gif)

</details>

<details>
<summary><strong>🏗️ Supported Architecture Options</strong></summary>

Choose the generated architecture style, including controller-based APIs, service-layer patterns, clean architecture, and minimal API approaches. The generator can support different project styles instead of forcing every solution into one shape.

**✅ Benefit:** Developers can match generated output to the architecture their team already uses, which makes adoption easier and keeps generated code aligned with existing engineering standards.

![supported architecture options for controller service clean architecture and minimal API in sql2solution](assets/solution-configuration/supported-architecture-controller-service-clean-minimal-api-sql2solution.gif)

</details>

<details>
<summary><strong>🧱 Include Or Exclude .NET Solution Project Layers</strong></summary>

Decide which .NET project layers should be included in the generated solution. Teams can create leaner projects for simple tools or fuller layered solutions for production systems with API, application, domain, infrastructure, tests, and client needs.

**✅ Benefit:** The generated solution stays proportional to the project instead of adding unnecessary layers or leaving out structure the team needs later.

![include or exclude .NET solution project layers in sql2solution](assets/solution-configuration/include-exclude-dotnet-solution-project-layers-sql2solution.gif)

</details>

<details>
<summary><strong>⚙️ API And DTO Styles: CQRS, MediatR, Controllers, Minimal APIs</strong></summary>

Configure API style and DTO generation choices, including CQRS, MediatR, controllers, and minimal API patterns. This lets developers generate code that fits their preferred ASP.NET Core conventions and application complexity.

**✅ Benefit:** Teams can avoid hand-converting generated output to match their stack because the generator starts closer to the desired architecture.

![API and DTO style options for CQRS MediatR controllers and minimal APIs in sql2solution](assets/solution-configuration/api-dto-styles-cqrs-mediatr-controllers-minimal-api-sql2solution.gif)

</details>

<details>
<summary><strong>📦 DTO Policy: Request, Response, Entity, Command, And Query DTOs</strong></summary>

Define how sql2solution creates DTOs for API requests, responses, entities, commands, and queries. Clear DTO policy helps separate external API contracts from internal persistence and domain behavior.

**✅ Benefit:** Better DTO control improves maintainability, reduces accidental data exposure, and gives client applications cleaner contracts to consume.

![DTO policy for request response entity command and query DTOs in sql2solution](assets/solution-configuration/dto-policy-request-response-entity-command-query-dtos-sql2solution.gif)

</details>

<details>
<summary><strong>🗄️ Entity Framework Core Configuration</strong></summary>

Configure Entity Framework Core generation behavior for SQL-backed .NET projects. This helps shape how database metadata becomes C# persistence configuration, entity mapping, and data access conventions.

**✅ Benefit:** Consistent EF Core configuration reduces repetitive mapping work and makes generated infrastructure easier to review, test, and extend.

![Entity Framework Core configuration for SQL Server to .NET API generation in sql2solution](assets/solution-configuration/entity-framework-core-configuration-sql2solution.gif)

</details>

<details>
<summary><strong>🌐 Global Solution Endpoint Defaults</strong></summary>

Set solution-wide endpoint defaults once and apply them consistently across generated APIs. Global defaults can cover repeated API behavior that should remain predictable across tables and entities.

**✅ Benefit:** Teams get consistent generated endpoints, fewer manual edits, and less risk that each entity behaves like a separate one-off implementation.

![global solution endpoint defaults for ASP.NET Core API generation in sql2solution](assets/solution-configuration/global-solution-endpoint-defaults-aspnet-core-api-sql2solution.gif)

</details>

<details>
<summary><strong>🎛️ Advanced Endpoint Defaults</strong></summary>

Fine-tune advanced endpoint behavior for generated ASP.NET Core APIs. These settings help teams control API behavior beyond the basic CRUD path, especially when projects need stronger conventions around generated operations.

**✅ Benefit:** Advanced defaults let developers make deeper API decisions before generation, saving manual endpoint cleanup after the solution is created.

![advanced endpoint defaults for ASP.NET Core API generator in sql2solution](assets/solution-configuration/advanced-endpoint-defaults-aspnet-core-api-generator-sql2solution.gif)

</details>

<details>
<summary><strong>🎯 Entity-Specific Endpoint Defaults</strong></summary>

Override global endpoint defaults for a specific entity when one table needs different API behavior. This is useful when most endpoints should follow the same convention, but important entities need special handling.

**✅ Benefit:** Developers get the speed of global conventions while keeping the flexibility to model exceptions without hand-editing generated code later.

![entity specific endpoint defaults overriding global settings in sql2solution](assets/solution-configuration/entity-specific-endpoint-defaults-override-global-settings-sql2solution.png)

</details>

<details>
<summary><strong>🧩 Endpoint Field Configuration Per Entity</strong></summary>

Configure fields for each generated entity endpoint, including whether a field should be searchable, filterable, editable, visible, or assigned a default value. This lets sql2solution generate APIs and client behavior that better match the real purpose of each SQL table.

**✅ Benefit:** Developers can prevent accidental field exposure, reduce repetitive manual DTO edits, and produce cleaner entity-specific APIs from the start.

![endpoint field configuration for search filter editable fields and default values in sql2solution](assets/solution-configuration/endpoint-field-configuration-search-filter-default-values-sql2solution.png)

</details>

<details>
<summary><strong>🕵️ Audit Fields With Defaults And Validation</strong></summary>

Seed standard audit fields or create and modify custom audit fields with validation rules. Teams can define lifecycle fields, soft-delete fields, required values, read-only behavior, and defaults before generating the solution.

**✅ Benefit:** Consistent audit fields make generated .NET APIs easier to govern, easier to debug, and safer for business workflows that need traceability.

![audit fields validation defaults for ASP.NET Core API generation in sql2solution](assets/solution-configuration/audit-fields-validation-defaults-aspnet-core-api-generator-sql2solution.png)

</details>

<details>
<summary><strong>🏢 Per-Entity Multi-Tenant, Cache, Claim, Delete, And Pagination Controls</strong></summary>

Control entity-specific behavior for multi-tenant solutions, including caching, query variation, validate-on-write behavior, claims, soft delete or hard delete, and pagination. This gives each generated entity the right runtime behavior instead of forcing one global pattern everywhere.

**✅ Benefit:** Teams can generate safer tenant-aware APIs, tune performance-sensitive entities, and keep delete and pagination behavior aligned with real business rules.

![per entity multi tenant caching claims soft delete and pagination configuration in sql2solution](assets/solution-configuration/per-entity-multi-tenant-caching-claims-soft-delete-pagination-sql2solution.png)

</details>

<details>
<summary><strong>🕒 Entity Configuration Version History And Revert</strong></summary>

Keep versions of each entity configuration so developers can compare changes and revert to an earlier setup when needed. This is useful when testing generation options, changing endpoint behavior, or recovering a known-good configuration after experimentation.

**✅ Benefit:** Version history reduces risk while tuning generated APIs, because teams can iterate on configuration without losing a working baseline.

![entity configuration version history and revert for sql2solution API generator](assets/solution-configuration/entity-configuration-version-history-revert-sql2solution.png)

</details>

<details>
<summary><strong>🔁 Generate Different API Versions</strong></summary>

Configure API versioning so generated ASP.NET Core endpoints can support versioned contracts. Versioning is important when clients depend on stable APIs while the database and product continue to evolve.

**✅ Benefit:** API version generation helps teams plan for backward compatibility, client migrations, and cleaner long-term API lifecycle management.

![ASP.NET Core API versioning configuration in sql2solution](assets/solution-configuration/aspnet-core-api-versioning-sql2solution.png)

</details>

## 🚦 Product Workflow Walkthroughs

These walkthroughs show the parts of sql2solution that help teams move beyond basic CRUD generation: client apps, security, runtime operations, realtime integrations, workflow automation, test generation, output management, and production-readiness checks.

<details open>
<summary><strong>🎨 Optional Client Generation: Razor Pages And Blazor Web App</strong></summary>

Generate an optional client project alongside the backend API. sql2solution can create client-facing project structure for Razor Pages or Blazor-style web app workflows so the generated backend has a practical UI path from the beginning.

**✅ Benefit:** Teams can validate generated APIs faster with real screens, reduce frontend setup time, and give stakeholders something useful to review earlier in the project.

![sql2solution client generation for Razor Pages Blazor web app and ASP.NET Core API projects](assets/product-workflows/client-generation-razor-pages-blazor-web-app-aspnet-core-api-sql2solution.png)

</details>

<details>
<summary><strong>🧬 Default Audit, Multi-Tenant, Pagination, Caching, And Query Behaviors</strong></summary>

Configure default generation behavior for audit fields, tenant-aware data access, pagination, caching, and query rules. These defaults help shape how generated APIs behave across the solution before individual entities are customized.

**✅ Benefit:** Teams can define consistent data access and API behavior once, then generate cleaner endpoints with fewer manual corrections after code creation.

![default audit multi tenant pagination caching and query behavior configuration in sql2solution](assets/product-workflows/default-audit-multi-tenant-pagination-caching-query-behaviors-sql2solution.gif)

![sql2solution default audit multi tenant pagination caching query behavior screenshot](assets/product-workflows/default-audit-multi-tenant-pagination-caching-query-behaviors-sql2solution.png)

</details>

<details>
<summary><strong>🔐 Security: Authentication, Authorization, Roles, Policies, And RBAC</strong></summary>

Enable authentication, choose providers, configure authorization modes, define roles and policies, and prepare enterprise-style access controls with RBAC bootstrap options for generated ASP.NET Core APIs.

**✅ Benefit:** Security becomes part of the generated foundation instead of a late project add-on, helping teams build safer APIs with clearer access rules from the start.

![ASP.NET Core API security authentication authorization roles policies and RBAC configuration in sql2solution](assets/product-workflows/security-authentication-authorization-roles-policies-rbac-aspnet-core-api-sql2solution.gif)

</details>

<details>
<summary><strong>🛡️ Operations: Runtime Resilience, Observability, And Rate Limiting</strong></summary>

Configure runtime operation features such as resilience patterns, observability, structured runtime readiness, and rate limiting. These settings help generated APIs behave more like production services instead of simple demo endpoints.

**✅ Benefit:** Teams can prepare generated APIs for monitoring, reliability, and controlled traffic earlier in the development lifecycle.

![sql2solution operations runtime resilience observability and rate limiting for ASP.NET Core APIs](assets/product-workflows/operations-runtime-resilience-observability-rate-limiting-sql2solution.gif)

</details>

<details>
<summary><strong>⚡ SignalR Realtime Integration For Client UI Refresh</strong></summary>

Enable SignalR integration so generated client screens can respond to backend data changes without requiring users to refresh the page. This creates a stronger foundation for realtime dashboards, admin tools, and collaborative business workflows.

**✅ Benefit:** Generated apps can feel more responsive and modern, while developers get a starting point for richer realtime features.

![SignalR realtime integration for Blazor client UI refresh in sql2solution](assets/product-workflows/signalr-realtime-integration-blazor-client-ui-refresh-sql2solution.gif)

</details>

<details>
<summary><strong>🔄 Workflow And State Machine Generation</strong></summary>

Configure workflow and state machine behavior for generated entities. This helps teams model lifecycle transitions, status-driven actions, and structured business processes directly in the generated solution plan.

**✅ Benefit:** Developers can generate more than simple CRUD endpoints by adding business-state behavior that mirrors real operational workflows.

![workflow and state machine generation for ASP.NET Core APIs in sql2solution](assets/product-workflows/workflow-state-machine-generated-aspnet-core-api-sql2solution.gif)

</details>

<details>
<summary><strong>🧪 Test Section And Smoke Tests</strong></summary>

Include tests in the generated solution and let sql2solution prepare smoke-test workflows after the solution is created. This helps verify that generated APIs, configuration, and expected runtime behavior are still aligned.

**✅ Benefit:** Teams can catch broken generation assumptions earlier and ship with more confidence because validation is part of the generated project workflow.

![test section and smoke tests for generated .NET API solution in sql2solution](assets/product-workflows/test-section-smoke-tests-generated-dotnet-api-solution-sql2solution.gif)

</details>

<details>
<summary><strong>📤 Output Management: CI Pipeline, Postman, EF Core Migrations, API Validation, And Smoke Tests</strong></summary>

Manage generated solution outputs before or after creation, including CI pipeline assets, Postman collections, EF Core migration management, API validation, and smoke-test execution.

**✅ Benefit:** sql2solution helps teams move from code generation to project delivery by preparing the supporting artifacts needed for testing, integration, automation, and handoff.

![sql2solution output section for CI pipeline Postman collection EF Core migrations API validation and smoke tests](assets/product-workflows/output-section-ci-pipeline-postman-ef-core-migrations-api-validation-smoke-tests-sql2solution.gif)

</details>

## 💡 Why It Exists

Building SQL-backed .NET applications often means repeating the same work:

- Create controllers and endpoints
- Map entities, DTOs, and request models
- Configure Swagger and OpenAPI
- Build Razor or Blazor client screens
- Add tenant-aware security rules
- Wire audit fields and lifecycle behavior
- Create tests and smoke checks
- Keep generated layers consistent as the schema changes

sql2solution exists to make that repeatable. Developers still own the final product, architecture decisions, business rules, and extensions, but they start from a generated foundation instead of a blank project.

## ⭐ Key Product Capabilities

### 🟣 .NET 8, .NET 9, and .NET 10 Targets

Generate modern .NET solution structures for current and upcoming application stacks.

### 🗃️ SQL-First Generation

Use SQL Server schema metadata as the source for generating APIs, clients, fields, relationships, and validation behavior.

### 🧼 Clean, Readable C# Output

Generated code is intended to be understandable, reviewable, and extensible by real developers.

### 📘 API Documentation Included

Generated APIs can be shaped for Swagger and OpenAPI discoverability, making endpoint testing and client integration easier.

### 🖥️ Client UI Generation

Generate Razor Pages and Blazor client experiences connected to the API layer.

### 🏢 Tenant-Aware Options

Support both single-tenant and multi-tenant application patterns, including tenant context and cross-tenant access validation.

### ✅ Quality And Validation

Use smoke-test coverage points and back-to-back checks to confirm API startup, Swagger availability, endpoint responses, and client route behavior.

## 🎯 Example Use Cases

- Internal admin systems backed by SQL Server
- SaaS products that need tenant-aware data access
- CRUD-heavy business applications
- Client portals and operational dashboards
- Data management tools
- API-first modernization of existing SQL databases
- Fast prototypes that need to become maintainable production code

## 🌍 Official Website

The public website and product details live at:

**[https://sql2solution.com/](https://sql2solution.com/)**

This GitHub repository is a public discovery page for the product. It intentionally does not expose the private website source code or production assets.

## 🏷️ Search Keywords

`sql2solution` · `.NET Core API generator` · `ASP.NET Core API generator` · `SQL Server API generator` · `SQL to API` · `SQL to .NET Core API` · `.NET CRUD generator` · `Swagger API generator` · `OpenAPI generator` · `Blazor generator` · `Razor Pages generator` · `multi tenant .NET API` · `database to API generator`

