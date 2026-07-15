using Microsoft.EntityFrameworkCore;

namespace Wallow.Shared.Infrastructure.Core.Persistence;

public sealed class ReadDbContextFactory<TContext> where TContext : DbContext
{
    private readonly DbContextOptions<TContext> _options;

    public ReadDbContextFactory(string connectionString)
    {
        DbContextOptionsBuilder<TContext> builder = new();
        builder.UseNpgsql(connectionString);
        _options = builder.Options;
    }

    public ReadDbContext<TContext> CreateReadDbContext()
    {
        TContext context = (TContext)Activator.CreateInstance(typeof(TContext), _options)!;
        return new ReadDbContext<TContext>(context, blockWrites: true);
    }
}
