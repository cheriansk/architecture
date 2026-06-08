# Purpose: Full detailed Cursor AI prompt to generate two reusable .NET 10 NuGet packages for a solution that includes an API project, a Blazor project, and a third shared contracts/foundation project used by both hosts.
# cursor-full-instructions.md
## Objective
Create two separate .NET 10 projects that can be packed and published as NuGet packages to enforce a modular monolith architecture for a solution containing:
1. .NET 10 Web API project
2. .NET 10 Blazor project (Server, WebAssembly hosted, or Web App interactive modes)
3. A third shared cross-app project used by both API and Blazor, such as Contracts / SharedKernel / Foundation

The two packages must be:
1. ModularMonolith.Enforcer.Analyzers
2. ModularMonolith.Enforcer.ArchTests

Do NOT create separate API-only and Blazor-only packages. Keep one analyzer package for both API and Blazor, and one architecture-test package for both API and Blazor. The rule system must also explicitly support a third host-neutral shared project such as Company.Product.Contracts.

## Architecture model to support
Namespace patterns must be configurable and not hardcoded. The default architecture model is now:
- Root.Api.Modules.{ModuleName}
- Root.Api.Shared
- Root.Api.Common
- Root.Api.Infrastructure
- Root.Blazor
- Root.Contracts (or Root.SharedKernel / Root.Foundation)
- Optional UI roots under Blazor or module UI namespaces

Rules to enforce:
- API modules must not directly reference other API modules
- API shared must not depend on API modules
- API common must not depend on API modules (unless explicitly configured)
- API infrastructure must not depend on API modules
- application layer must not directly use DbContext
- handlers must live in Modules.{Name}.Application
- domain entities should generally remain internal and live in Modules.{Name}.Domain
- pipeline behaviors must live in Common.Behaviors
- middleware must live in Common.Middleware
- DI registration must stay inside module registration classes
- one module must not register another module’s implementation types
- Blazor components/pages must not directly use another module’s domain/internal implementation types
- modules must not cross persistence boundaries by directly querying/updating another module’s entities/tables/mappings
- Contracts project may be referenced by both API and Blazor
- Contracts must not reference API modules, API infrastructure, or Blazor UI implementation
- Contracts must contain only DTOs, request/response models, enums, records, lightweight validation metadata, and framework-neutral helper code

## Required solution structure
ModularMonolith.Enforcer.sln
  /src
    /ModularMonolith.Enforcer.Analyzers
    /ModularMonolith.Enforcer.ArchTests
  /tests
    /ModularMonolith.Enforcer.Analyzers.Tests
    /ModularMonolith.Enforcer.ArchTests.Tests
