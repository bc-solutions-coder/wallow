using Microsoft.Extensions.DependencyInjection;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Tests.Common.Bases;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests;

/// <summary>
/// Collection definition that ensures all Identity integration test classes share
/// a single WallowApiFactory instance (and its containers).
/// </summary>
[CollectionDefinition(Name)]
public class IdentityTestCollection : ICollectionFixture<WallowApiFactory>
{
    public const string Name = "Identity";
}

/// <summary>
/// Base class for Identity integration tests that use WallowApiFactory with TestAuthHandler.
/// Seeds identity data (users, OAuth2 clients) via IdentityFixture on first use.
/// </summary>
[Collection(IdentityTestCollection.Name)]
[Trait("Category", "Integration")]
public class IdentityIntegrationTestBase : WallowIntegrationTestBase
{
    private static readonly IdentityFixture _identityFixture = new();
    private static bool _seeded;
    private static readonly SemaphoreSlim _seedLock = new(1, 1);

    protected IdentityFixture IdentityFixture => _identityFixture;

    public IdentityIntegrationTestBase(WallowApiFactory factory) : base(factory)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        IdentityDbContext dbContext = ScopedServices.GetRequiredService<IdentityDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        await _seedLock.WaitAsync();
        try
        {
            if (!_seeded)
            {
                await _identityFixture.SeedAsync(ScopedServices);
                _seeded = true;
            }
        }
        finally
        {
            _seedLock.Release();
        }
    }
}
