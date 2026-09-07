using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Wallow.Branding.Application.Exceptions;
using Wallow.Branding.Domain.Entities;
using Wallow.Branding.Infrastructure.Persistence;
using Wallow.Branding.Infrastructure.Repositories;
using Wallow.Shared.Contracts;
using Wallow.Shared.Contracts.Branding.Events;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wolverine.EntityFrameworkCore;

namespace Wallow.Branding.Tests.Infrastructure;

public sealed class ClientBrandingRepositoryTests : IDisposable
{
    private readonly string _databaseName = Guid.NewGuid().ToString();
    private readonly BrandingDbContext _dbContext;
    private readonly IDbContextOutbox _outbox = Substitute.For<IDbContextOutbox>();
    private readonly ClientBrandingRepository _sut;

    public ClientBrandingRepositoryTests()
    {
        _dbContext = CreateDbContextForTenant(TenantId.New());
        _sut = new ClientBrandingRepository(_dbContext, _outbox);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    /// <summary>
    /// Opens a second context over the same in-memory store, acting as <paramref name="tenantId"/>.
    /// Pass <c>default</c> to model a request where no tenant resolved at all, which is what the
    /// anonymous branding endpoint does.
    /// </summary>
    private BrandingDbContext CreateDbContextForTenant(TenantId tenantId)
    {
        TenantContext tenantContext = new();
        if (tenantId != default)
        {
            tenantContext.SetTenant(tenantId);
        }

        TenantSaveChangesInterceptor tenantInterceptor = new(tenantContext);

        DbContextOptions<BrandingDbContext> options = new DbContextOptionsBuilder<BrandingDbContext>()
            .UseInMemoryDatabase(_databaseName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(tenantInterceptor)
            .Options;

        BrandingDbContext context = new(options);
        context.SetTenant(tenantId);
        return context;
    }

    [Fact]
    public async Task GetByClientIdAsync_WhenExists_ReturnsBranding()
    {
        ClientBranding branding = ClientBranding.Create("client-1", "My App", "Tagline");
        _dbContext.ClientBrandings.Add(branding);
        await _dbContext.SaveChangesAsync();

        ClientBranding? result = await _sut.GetByClientIdAsync("client-1");

        result.Should().NotBeNull();
        result!.ClientId.Should().Be("client-1");
        result.DisplayName.Should().Be("My App");
    }

    [Fact]
    public async Task GetByClientIdAsync_WhenNotExists_ReturnsNull()
    {
        ClientBranding? result = await _sut.GetByClientIdAsync("nonexistent");

        result.Should().BeNull();
    }

    /// <summary>
    /// Synchronization must read the committed display name even when this context tracks an older row.
    /// </summary>
    [Fact]
    public async Task FindDisplayNameAsync_ReadsTheCommittedValue_NotAnAlreadyTrackedInstance()
    {
        ClientBranding branding = ClientBranding.Create("client-1", "Old Name");
        _dbContext.ClientBrandings.Add(branding);
        await _dbContext.SaveChangesAsync();
        (await _sut.GetByClientIdAsync("client-1")).Should().NotBeNull(); // now tracked in this scope

        await using BrandingDbContext otherDbContext = CreateDbContextForTenant(TenantId.New());
        ClientBrandingRepository concurrentWriter = new(otherDbContext, Substitute.For<IDbContextOutbox>());
        ClientBranding winner = (await concurrentWriter.GetByClientIdAsync("client-1"))!;
        winner.Update("New Name", null, null, null);
        await concurrentWriter.SaveChangesAsync();

        string? displayName = await _sut.FindDisplayNameAsync("client-1");

        displayName.Should().Be("New Name");
    }

    [Fact]
    public async Task FindDisplayNameAsync_WhenNoRowExists_ReturnsNull()
    {
        (await _sut.FindDisplayNameAsync("nonexistent")).Should().BeNull();
    }

    [Fact]
    public async Task Add_PersistsBranding()
    {
        ClientBranding branding = ClientBranding.Create("client-1", "My App");

        _sut.Add(branding);
        await _sut.SaveChangesAsync();

        ClientBranding? found = await _sut.GetByClientIdAsync("client-1");
        found.Should().NotBeNull();
        found!.DisplayName.Should().Be("My App");
    }

    [Fact]
    public async Task Remove_DeletesBranding()
    {
        ClientBranding branding = ClientBranding.Create("client-1", "My App");
        _dbContext.ClientBrandings.Add(branding);
        await _dbContext.SaveChangesAsync();

        _sut.Remove(branding);
        await _sut.SaveChangesAsync();

        ClientBranding? found = await _sut.GetByClientIdAsync("client-1");
        found.Should().BeNull();
    }

    [Fact]
    public async Task GetByClientIdAsync_WithMultipleBrandings_ReturnsCorrectOne()
    {
        ClientBranding branding1 = ClientBranding.Create("client-1", "App One");
        ClientBranding branding2 = ClientBranding.Create("client-2", "App Two");
        _dbContext.ClientBrandings.Add(branding1);
        _dbContext.ClientBrandings.Add(branding2);
        await _dbContext.SaveChangesAsync();

        ClientBranding? result = await _sut.GetByClientIdAsync("client-2");

        result.Should().NotBeNull();
        result!.DisplayName.Should().Be("App Two");
    }

    /// <summary>
    /// Client IDs are globally unique; the repository read must find other tenants' branding.
    /// Write authorization belongs to the controller's organization client-directory check.
    /// </summary>
    [Fact]
    public async Task GetByClientIdAsync_WhenBrandingBelongsToAnotherTenant_ReturnsBranding()
    {
        await using BrandingDbContext otherDbContext = CreateDbContextForTenant(TenantId.New());
        ClientBrandingRepository otherRepository = new(otherDbContext, Substitute.For<IDbContextOutbox>());

        otherRepository.Add(ClientBranding.Create("cross-tenant-client", "Cross Tenant"));
        await otherRepository.SaveChangesAsync();

        ClientBranding? result = await _sut.GetByClientIdAsync("cross-tenant-client");

        result.Should().NotBeNull();
        result!.DisplayName.Should().Be("Cross Tenant");
    }

    /// <summary>
    /// Anonymous branding reads have no resolved tenant; tenant filtering would hide this row.
    /// </summary>
    [Fact]
    public async Task GetByClientIdAsync_WhenNoTenantResolved_ReturnsBranding()
    {
        _dbContext.ClientBrandings.Add(ClientBranding.Create("client-1", "My App"));
        await _dbContext.SaveChangesAsync();

        await using BrandingDbContext anonymousDbContext = CreateDbContextForTenant(default);
        ClientBrandingRepository anonymousRepository = new(anonymousDbContext, Substitute.For<IDbContextOutbox>());

        ClientBranding? result = await anonymousRepository.GetByClientIdAsync("client-1");

        result.Should().NotBeNull();
        result!.DisplayName.Should().Be("My App");
    }

    /// <summary>
    /// A concurrent insert must produce a typed exception and detach the losing entry
    /// so the caller can retry as an update.
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_OnAUniqueViolation_ThrowsTyped_AndDetachesTheLosingInsert()
    {
        PostgresException violation = new(
            "duplicate key value violates unique constraint",
            "ERROR",
            "ERROR",
            PostgresErrorCodes.UniqueViolation);
        await using BrandingDbContext context = CreateThrowingDbContext(
            new DbUpdateException("An error occurred while saving the entity changes.", violation));
        ClientBrandingRepository sut = new(context, _outbox);
        ClientBranding losing = ClientBranding.Create("client-1", "My App");
        sut.Add(losing);

        Func<Task> act = () => sut.SaveChangesAsync();

        DuplicateClientBrandingException thrown =
            (await act.Should().ThrowAsync<DuplicateClientBrandingException>()).Which;
        thrown.ClientId.Should().Be("client-1");
        context.Entry(losing).State.Should().Be(EntityState.Detached);
    }

    [Fact]
    public async Task SaveChangesAsync_OnAnyOtherSaveFailure_Rethrows_AndKeepsTheEntry()
    {
        PostgresException violation = new(
            "insert or update violates foreign key constraint",
            "ERROR",
            "ERROR",
            PostgresErrorCodes.ForeignKeyViolation);
        await using BrandingDbContext context = CreateThrowingDbContext(
            new DbUpdateException("An error occurred while saving the entity changes.", violation));
        ClientBrandingRepository sut = new(context, _outbox);
        ClientBranding branding = ClientBranding.Create("client-1", "My App");
        sut.Add(branding);

        Func<Task> act = () => sut.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
        context.Entry(branding).State.Should().Be(EntityState.Added);
    }

    /// <summary>
    /// Without a pending insert to detach, preserve the original unique-violation exception.
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_OnAUniqueViolationWithNoPendingInsert_RethrowsTheOriginal()
    {
        PostgresException violation = new(
            "duplicate key value violates unique constraint",
            "ERROR",
            "ERROR",
            PostgresErrorCodes.UniqueViolation);
        await using BrandingDbContext context = CreateThrowingDbContext(
            new DbUpdateException("An error occurred while saving the entity changes.", violation));
        ClientBrandingRepository sut = new(context, _outbox);

        Func<Task> act = () => sut.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    /// <summary>
    /// Concurrent row deletion must produce a typed exception and detach the stale entry.
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenTheRowWasDeletedUnderneath_ThrowsTyped_AndDetachesTheStaleEntry()
    {
        // Modifying an absent row simulates an update after concurrent deletion.
        ClientBranding stale = ClientBranding.Create("client-1", "My App");
        _dbContext.ClientBrandings.Attach(stale);
        _dbContext.Entry(stale).State = EntityState.Modified;

        Func<Task> act = () => _sut.SaveChangesAsync();

        ClientBrandingConcurrentlyDeletedException thrown =
            (await act.Should().ThrowAsync<ClientBrandingConcurrentlyDeletedException>()).Which;
        thrown.ClientId.Should().Be("client-1");
        _dbContext.Entry(stale).State.Should().Be(EntityState.Detached);
    }

    /// <summary>
    /// The save must enroll its context before publishing through the outbox, then flush delivery.
    /// </summary>
    [Fact]
    public async Task SaveChangesAndPublishAsync_PersistsTheRow_AndPublishesThroughTheEnrolledOutbox()
    {
        IIntegrationEvent @event = UpdatedEvent();
        _sut.Add(ClientBranding.Create("client-1", "My App"));

        await _sut.SaveChangesAndPublishAsync(@event);

        (await _sut.GetByClientIdAsync("client-1")).Should().NotBeNull();
        Received.InOrder(() =>
        {
            _outbox.Enroll(_dbContext);
            // AsTask() consumes the ValueTask (CA2012); InOrder only records the call.
            _outbox.PublishAsync(@event).AsTask();
            _outbox.FlushOutgoingMessagesAsync();
        });
    }

    [Fact]
    public async Task SaveChangesAndPublishAsync_WhenTheInsertLosesTheRace_ThrowsTyped_AndPublishesNothing()
    {
        PostgresException violation = new(
            "duplicate key value violates unique constraint",
            "ERROR",
            "ERROR",
            PostgresErrorCodes.UniqueViolation);
        await using BrandingDbContext context = CreateThrowingDbContext(
            new DbUpdateException("An error occurred while saving the entity changes.", violation));
        ClientBrandingRepository sut = new(context, _outbox);
        ClientBranding losing = ClientBranding.Create("client-1", "My App");
        sut.Add(losing);

        Func<Task> act = () => sut.SaveChangesAndPublishAsync(UpdatedEvent());

        DuplicateClientBrandingException thrown =
            (await act.Should().ThrowAsync<DuplicateClientBrandingException>()).Which;
        thrown.ClientId.Should().Be("client-1");
        context.Entry(losing).State.Should().Be(EntityState.Detached);
        await _outbox.DidNotReceiveWithAnyArgs().PublishAsync(default(IIntegrationEvent)!);
        await _outbox.DidNotReceive().FlushOutgoingMessagesAsync();
    }

    [Fact]
    public async Task SaveChangesAndPublishAsync_WhenTheRowWasDeletedUnderneath_ThrowsTyped_AndPublishesNothing()
    {
        ClientBranding stale = ClientBranding.Create("client-1", "My App");
        _dbContext.ClientBrandings.Attach(stale);
        _dbContext.Entry(stale).State = EntityState.Modified;

        Func<Task> act = () => _sut.SaveChangesAndPublishAsync(UpdatedEvent());

        await act.Should().ThrowAsync<ClientBrandingConcurrentlyDeletedException>();
        _dbContext.Entry(stale).State.Should().Be(EntityState.Detached);
        await _outbox.DidNotReceiveWithAnyArgs().PublishAsync(default(IIntegrationEvent)!);
        await _outbox.DidNotReceive().FlushOutgoingMessagesAsync();
    }

    private static ClientBrandingUpdatedEvent UpdatedEvent() => new()
    {
        ClientId = "client-1",
        OrganizationId = Guid.NewGuid(),
        ActorId = Guid.NewGuid(),
        DisplayName = "My App",
    };

    private BrandingDbContext CreateThrowingDbContext(Exception exception)
    {
        DbContextOptions<BrandingDbContext> options = new DbContextOptionsBuilder<BrandingDbContext>()
            .UseInMemoryDatabase(_databaseName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(new ThrowingSaveChangesInterceptor(exception))
            .Options;

        BrandingDbContext context = new(options);
        context.SetTenant(TenantId.New());
        return context;
    }

    private sealed class ThrowingSaveChangesInterceptor(Exception exception) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
            => throw exception;
    }
}
