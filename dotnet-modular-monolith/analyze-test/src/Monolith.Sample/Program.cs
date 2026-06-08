using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Monolith.Shared.Domain;
using Monolith.Shared.Infrastructure;

namespace Monolith.Sample
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Sample Monolith Startup");
        }
    }
}

namespace Monolith.Shared.Domain
{
    public interface ITenantProvider
    {
        string GetTenantSchema();
    }

    public class MockTenantProvider : ITenantProvider
    {
        public string GetTenantSchema() => "tenant_123";
    }
}

namespace Monolith.Shared.Infrastructure
{
    public interface ITenantDbContext
    {
        string TenantSchema { get; }
    }

    public class TenantModelCacheKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime)
        {
            if (context is ITenantDbContext tenantContext)
            {
                return (context.GetType(), tenantContext.TenantSchema, designTime);
            }
            return (context.GetType(), designTime);
        }
    }
}
