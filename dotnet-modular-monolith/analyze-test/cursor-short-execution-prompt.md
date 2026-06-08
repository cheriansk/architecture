# cursor-short-execution-prompt.md
Create a .NET 10 solution named ModularMonolith.Enforcer.sln with these projects:
- src/ModularMonolith.Enforcer.Analyzers
- src/ModularMonolith.Enforcer.ArchTests
- tests/ModularMonolith.Enforcer.Analyzers.Tests
- tests/ModularMonolith.Enforcer.ArchTests.Tests
- samples/SampleApiConsumer
- samples/SampleBlazorConsumer

Assume the consuming solution contains:
- Company.Product.Api
- Company.Product.Blazor
- Company.Product.Contracts

Goal: produce 2 reusable NuGet packages only:
1. ModularMonolith.Enforcer.Analyzers
2. ModularMonolith.Enforcer.ArchTests

These packages must support both API and Blazor and must also explicitly support a third cross-app Contracts project shared by both hosts. Implement configurable enforcement for:
- API modular monolith rules
- Blazor UI boundary rules
- Contracts purity rules

Add analyzer and architecture-test rules so Contracts can be used by both API and Blazor, but Contracts must not reference API modules, API infrastructure, or Blazor UI internals, and must not contain DbContext, repositories, domain entities, middleware, UI components, or host-specific business logic.

Use Roslyn symbol analysis and NetArchTest/FluentAssertions. Provide docs, samples, pack metadata, and commands to build/test/pack.

Tip: You can save each of the two sections above as separate .md files and attach them directly into Cursor AI.
