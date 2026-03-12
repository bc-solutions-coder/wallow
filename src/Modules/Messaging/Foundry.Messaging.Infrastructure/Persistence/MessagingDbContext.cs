using Foundry.Messaging.Domain.Conversations.Entities;
using Foundry.Shared.Infrastructure.Core.Persistence;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Messaging.Infrastructure.Persistence;

public sealed class MessagingDbContext : TenantAwareDbContext<MessagingDbContext>
{
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Participant> Participants => Set<Participant>();

    public MessagingDbContext(DbContextOptions<MessagingDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("messaging");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MessagingDbContext).Assembly);

        ApplyTenantQueryFilters(modelBuilder);
    }
}
