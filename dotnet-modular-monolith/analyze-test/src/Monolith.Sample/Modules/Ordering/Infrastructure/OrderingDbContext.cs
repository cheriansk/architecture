using Microsoft.EntityFrameworkCore;
using Monolith.Shared.Domain;
using Monolith.Shared.Infrastructure;
using Monolith.Modules.Ordering.Domain;

namespace Monolith.Modules.Ordering.Infrastructure;

internal class OrderingDbContext : DbContext, ITenantDbContext
{
    private readonly string _tenantSchema;

    public OrderingDbContext(
        DbContextOptions<OrderingDbContext> options,
        ITenantProvider tenantProvider) : base(options)
    {
        _tenantSchema = tenantProvider.GetTenantSchema();
    }

    public string TenantSchema => _tenantSchema;

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Order>(builder =>
        {
            builder.ToTable("ordering_orders", _tenantSchema);
            builder.HasKey(o => o.Id);
        });
    }
}
