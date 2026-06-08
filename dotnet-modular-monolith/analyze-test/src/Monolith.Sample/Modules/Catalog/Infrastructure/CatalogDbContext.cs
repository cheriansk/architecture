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

        modelBuilder.Entity<Product>(builder =>
        {
            builder.ToTable("catalog_products", _tenantSchema);
            builder.HasKey(p => p.Id);
        });
    }
}
