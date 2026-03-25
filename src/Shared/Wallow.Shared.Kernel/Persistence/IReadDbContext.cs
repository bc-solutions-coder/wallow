using Microsoft.EntityFrameworkCore;

namespace Wallow.Shared.Kernel.Persistence;

public interface IReadDbContext<out TContext> where TContext : DbContext
{
    TContext Context { get; }
}
