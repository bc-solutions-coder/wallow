using Microsoft.EntityFrameworkCore;
using Wallow.Shared.Kernel.Persistence;

namespace Wallow.Shared.Infrastructure.Core.Persistence;

public sealed class ReadDbContext<TContext> : IReadDbContext<TContext> where TContext : DbContext
{
    public ReadDbContext(TContext context, bool blockWrites = false)
    {
        Context = context;
        Context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

        if (blockWrites)
        {
            Context.SavingChanges += OnSavingChanges;
        }
    }

    public TContext Context { get; }

    private static void OnSavingChanges(object? sender, SavingChangesEventArgs e)
    {
        throw new InvalidOperationException("Read context does not allow writes.");
    }
}
