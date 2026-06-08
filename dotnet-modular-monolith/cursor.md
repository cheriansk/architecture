# PostgreSQL Schema-per-Tenant Modular Monolith Guide

In a **Schema-per-Tenant** architecture using PostgreSQL and EF Core, each tenant gets their own dedicated database schema (e.g. `tenant_a`, `tenant_b`). Within each tenant's schema, tables are isolated by module using table prefixes (e.g. `catalog_products`, `ordering_orders`).

To ensure total data isolation and avoid connection setup complexity, we map all tables using dynamic schema-qualified mappings: `ToTable("table_name", schema)`.

---

## 1. Architectural Blueprint (Dynamic Schema Mapping)

Every table is mapped directly to a specific schema at runtime. To prevent EF Core from caching a single schema mapping globally (which would cause tenant data leakage), we implement a custom **Model Cache Key Factory** that instructs EF Core to cache separate models for each tenant.

```mermaid
graph TD
    %% Request Flow
    Request[HTTP Request] -->|Headers: X-Tenant-Id| TenantMiddleware[Tenant Middleware]
    TenantMiddleware -->|Resolves Schema 'tenant_a'| TenantProvider[ITenantProvider]
    
    %% EF Core Scoped Context
    TenantProvider -->|Injects tenant_a| DbContext[CatalogDbContext]
    DbContext -->|Assembles model using tenant_a| OnModelCreating[OnModelCreating: ToTable('catalog_products', 'tenant_a')]
    
    %% Model Caching
    OnModelCreating -->|Consults Cache Key| ModelCache[TenantModelCacheKeyFactory]
    ModelCache -->|Returns cache key with tenant_a| EFModelCache[(EF Core Model Cache)]
    
    %% Database routing
    DbContext -->|Generates SQL| SQL[SELECT * FROM tenant_a.catalog_products]
    SQL --> PG[(PostgreSQL Database)]
```

### Key Architectural Guidelines:
1. **Dynamic Schema Mapping**: Use `ToTable("table_name", schema)` inside `OnModelCreating`.
2. **Model Cache Key Separation**: Create a custom `IModelCacheKeyFactory` to generate cache keys that contain the active tenant schema.
3. **No `tenant_id` Column**: Physical schema isolation makes tenant ID columns redundant.
4. **Per-Schema Migrations History**: Place the `__EFMigrationsHistory` table inside the tenant's schema to track migrations independently.

---

## 2. Directory Structure

```
Monolith/
├── Modules/
│   ├── Catalog/
│   │   ├── Domain/                 # Entities, Value Objects, Domain Events (all internal)
│   │   ├── Application/            # MediatR Handlers, public DTOs, interfaces
│   │   ├── Infrastructure/         # CatalogDbContext, Repositories (all internal)
│   │   └── UI/                     # Blazor Pages, Catalog endpoints
│   └── Ordering/
│       ├── Domain/
│       ├── ...
├── Shared/
│   ├── Domain/                     # Common DDD base classes & ITenantProvider
│   ├── Infrastructure/             # TenantModelCacheKeyFactory, TenantProvisioningService
│   └── UI/                         # Layouts and global CSS
├── Program.cs                      # Main entrypoint: registers TenantProvider, DbContexts, maps UI
└── Monolith.csproj                 # Single project file
```

---

## 3. Key Implementation Patterns

### Pattern A: Tenant Provider (In `Monolith/Shared/Domain`)

```csharp
namespace Monolith.Shared.Domain;

public interface ITenantProvider
{
    string GetTenantSchema(); // e.g. returns "tenant_a" or "tenant_b"
}
```

---

### Pattern B: EF Core Model Cache Key Factory (In `Monolith/Shared/Infrastructure`)

This is critical. Without this, EF Core will only execute queries against the schema of the first tenant that booted the application.

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Monolith.Shared.Infrastructure;

public class TenantModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
    {
        // Generate a cache key combining the DbContext type and the current tenant schema
        if (context is ITenantDbContext tenantContext)
        {
            return (context.GetType(), tenantContext.TenantSchema, designTime);
        }
        
        return (context.GetType(), designTime);
    }
}

// Marker interface for Tenant DbContexts
public interface ITenantDbContext
{
    string TenantSchema { get; }
}
```

---

### Pattern C: DbContext with Dynamic Schema Mapping (In `Monolith/Modules/Catalog/Infrastructure`)

Map tables using the injected schema name.

```csharp
using Microsoft.EntityFrameworkCore;
using Monolith.Shared.Domain;
using Monolith.Shared.Infrastructure;
using Monolith.Modules.Catalog.Domain;

namespace Monolith.Modules.Catalog.Infrastructure;

internal class CatalogDbContext : DbContext, ITenantDbContext
{
    private readonly string _tenantSchema;

    public CatalogDbContext(
        DbContextOptions<CatalogDbContext> options,
        ITenantProvider tenantProvider) : base(options)
    {
        _tenantSchema = tenantProvider.GetTenantSchema();
    }

    public string TenantSchema => _tenantSchema;

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Dynamically map tables to the tenant schema
        modelBuilder.Entity<Product>(builder =>
        {
            builder.ToTable("catalog_products", _tenantSchema); // table, schema
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        });
    }
}
```

---

### Pattern D: Dynamic DB Registration & Per-Schema Migrations (In `Monolith/Program.cs`)

Configure the DB Context options to write the migrations history table dynamically inside the tenant's schema:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Monolith.Shared.Domain;
using Monolith.Shared.Infrastructure;
using Monolith.Modules.Catalog.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// 1. Register Tenant Provider
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantProvider, HttpTenantProvider>();

// 2. Register Custom Model Cache Key Factory
builder.Services.AddSingleton<IModelCacheKeyFactory, TenantModelCacheKeyFactory>();

// 3. Register DbContext with dynamic options
builder.Services.AddDbContext<CatalogDbContext>((sp, options) =>
{
    var tenantProvider = sp.GetRequiredService<ITenantProvider>();
    var schema = tenantProvider.GetTenantSchema();
    
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions => 
        {
            // Directs migration history to live in the tenant's schema
            npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", schema);
        }
    );
});
```

---

## 4. Tenant Provisioning & Dynamic Migrations

To onboard a new tenant, dynamically create the schema and execute migrations. Because migrations history is per-schema, migrations run independently for each tenant.

```csharp
using Microsoft.EntityFrameworkCore;

namespace Monolith.Shared.Infrastructure;

public class TenantProvisioningService(CatalogDbContext context)
{
    public async Task ProvisionTenantAsync(string tenantSchema)
    {
        // 1. Sanitize schema name to prevent SQL injection
        if (string.IsNullOrWhiteSpace(tenantSchema) || !tenantSchema.All(c => char.IsLetterOrDigit(c) || c == '_'))
        {
            throw new ArgumentException("Invalid tenant schema name.");
        }

        // 2. Create the schema in PostgreSQL
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = $"CREATE SCHEMA IF NOT EXISTS \"{tenantSchema}\";";
            await command.ExecuteNonQueryAsync();
        }

        // 3. Run migrations.
        // EF Core maps tables to the tenantSchema via OnModelCreating,
        // and records execution in {tenantSchema}.__EFMigrationsHistory.
        await context.Database.MigrateAsync();
    }
}
```

---

## 5. Architectural Verification via `NetArchTest`

Maintain namespace separation rules in `Monolith.Tests/`:

```csharp
using NetArchTest.Rules;
using Xunit;

namespace Monolith.Tests;

public class ArchitectureTests
{
    [Fact]
    public void Catalog_Should_Not_Depend_On_Ordering_Infrastructure()
    {
        var result = Types.InAssembly(typeof(Monolith.Program).Assembly)
            .That()
            .ResideInNamespace("Monolith.Modules.Catalog")
            .ShouldNot()
            .HaveDependencyOn("Monolith.Modules.Ordering.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, "Catalog cannot depend on Ordering Infrastructure!");
    }
}
```
