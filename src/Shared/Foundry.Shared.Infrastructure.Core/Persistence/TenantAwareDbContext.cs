using System.Linq.Expressions;
using System.Reflection;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Foundry.Shared.Infrastructure.Persistence;

public abstract class TenantAwareDbContext<TContext> : DbContext where TContext : DbContext
{
#pragma warning disable SA1401, CA1051, IDE1006 // Field must be protected for expression tree access in subclasses
    protected readonly TenantId _tenantId;
#pragma warning restore SA1401, CA1051, IDE1006

    protected TenantAwareDbContext(DbContextOptions<TContext> options, ITenantContext tenantContext) : base(options)
    {
        _tenantId = tenantContext.TenantId;
    }

    protected void ApplyTenantQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                ParameterExpression parameter = Expression.Parameter(entityType.ClrType, "e");
                MemberExpression property = Expression.Property(parameter, nameof(ITenantScoped.TenantId));

                ConstantExpression contextExpression = Expression.Constant(this);
                MemberExpression tenantIdField = Expression.Field(
                    contextExpression,
                    typeof(TenantAwareDbContext<TContext>).GetField("_tenantId",
                        BindingFlags.NonPublic | BindingFlags.Instance)!);

                BinaryExpression equals = Expression.Equal(property, tenantIdField);
                LambdaExpression lambda = Expression.Lambda(equals, parameter);

                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }
    }
}
