using System.Linq.Expressions;
using System.Reflection;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Wallow.Shared.Infrastructure.Core.Persistence;

public abstract class TenantAwareDbContext<TContext> : DbContext, ITenantAwareContext where TContext : DbContext
{
    // ReSharper disable once InconsistentNaming — Field must be protected for expression tree access in subclasses
#pragma warning disable SA1401, CA1051, IDE1006
    protected TenantId _tenantId;
#pragma warning restore SA1401, CA1051, IDE1006

    protected TenantAwareDbContext(DbContextOptions<TContext> options) : base(options)
    {
        _tenantId = default;
    }

    public TenantId CurrentTenantId => _tenantId;

    public void SetTenant(TenantId tenantId)
    {
        _tenantId = tenantId;
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
