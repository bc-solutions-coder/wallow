using Microsoft.EntityFrameworkCore;
using Wallow.Notifications.Infrastructure.Persistence;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Notifications.Tests.Infrastructure.Persistence;

public abstract class RepositoryTestBase : IDisposable
{
    private static readonly TenantId _testTenantId = TenantId.New();
    private bool _disposed;

    protected static TenantId TestTenantId => _testTenantId;

    protected NotificationsDbContext Context { get; }

    protected RepositoryTestBase()
    {
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(_testTenantId);

        DbContextOptions<NotificationsDbContext> options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        Context = new NotificationsDbContext(options);
        Context.SetTenant(_testTenantId);
    }

    protected void SetTenantId<TEntity>(TEntity entity) where TEntity : class, ITenantScoped
    {
        Context.Entry(entity).Property(nameof(ITenantScoped.TenantId)).CurrentValue = _testTenantId;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                Context.Dispose();
            }

            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
