#if TRIGGER_VIOLATIONS
using System;
using Microsoft.EntityFrameworkCore;
using Monolith.Modules.Catalog.Domain; // For cross-module access violation (MONO001)
using Monolith.Modules.Ordering.Domain;

namespace Monolith.Modules.Ordering.Infrastructure;

// 1. Violation of MONO001: Ordering module directly accessing Catalog's internal Domain (Product)
public class CrossModuleBoundaryViolation
{
    public void Exploit()
    {
        var product = new Product(); // MONO001: Catalog's internal Product referenced in Ordering
        Console.WriteLine(product.Name);
    }
}

// 2. Violation of MONO002 & MONO003: Public DbContext (MONO003), missing ITenantDbContext (MONO003),
// and missing schema in ToTable + missing prefix (MONO002)
public class BadOrderingDbContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // MONO002: Calling ToTable without tenant schema, and table name doesn't start with ordering_
        modelBuilder.Entity<Order>().ToTable("bad_orders");
    }
}
#endif
