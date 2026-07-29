using Wallow.Shared.Kernel.Identity;
using Wallow.Storage.Domain.Entities;
using Wallow.Storage.Infrastructure.Persistence;
using Wallow.Storage.Infrastructure.Persistence.Repositories;
using Wallow.Tests.Common.Bases;
using Wallow.Tests.Common.Fixtures;

namespace Wallow.Storage.Tests.Integration;

/// <summary>
/// Security regression suite for the multi-tenant query filter on statically compiled EF Core
/// queries.
/// </summary>
/// <remarks>
/// <para>
/// <c>StoredFileRepository._getByIdQuery</c> is a <b>static</b> <c>EF.CompileAsyncQuery</c>, so the
/// query — including the global tenant filter that
/// <c>TenantAwareDbContext.ApplyTenantQueryFilters</c> installs — is compiled once per process and
/// then reused by every <c>StorageDbContext</c> instance, for every tenant.
/// </para>
/// <para>
/// The filter is built as <c>e.TenantId == ((TenantAwareDbContext)constantContextInstance)._tenantId</c>,
/// closing over the <i>specific context instance</i> that happened to trigger model building.
/// EF Core's model cache is keyed on the context type, so every later instance reuses that model.
/// The comment in <c>ApplyTenantQueryFilters</c> asserts that EF's
/// <c>QueryFilterRewritingExpressionVisitor</c> rebinds that captured instance to the executing
/// context. These tests prove that claim rather than trusting it: if the rebinding did not happen,
/// every tenant would silently be filtered against — and therefore able to read — the rows of
/// whichever tenant warmed the compiled query first.
/// </para>
/// </remarks>
[Collection("PostgresDatabase")]
[Trait("Category", "Integration")]
[Trait("Category", "CrossTenant")]
public sealed class CompiledQueryTenantFilterTests(PostgresContainerFixture fixture)
    : DbContextIntegrationTestBase<StorageDbContext>(fixture)
{
    private StoredFileRepository _repository = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _repository = new StoredFileRepository(DbContext);
    }

    [Fact]
    public async Task GetByIdAsync_CompiledQuery_DoesNotLeakToTheTenantThatWarmedIt()
    {
        StoredFile ownFile = await SeedFileAsync(DbContext, _repository, "warming-tenant.txt");

        TenantId otherTenantId = TenantId.New();
        await using StorageDbContext otherDbContext = CreateDbContextForTenant(otherTenantId);
        StoredFileRepository otherRepository = new(otherDbContext);
        StoredFile otherFile = await SeedFileAsync(otherDbContext, otherRepository, "other-tenant.txt");

        // Warm the static compiled query from this test's tenant first, so any tenant id baked in
        // at compile time would be this one.
        StoredFile? ownLookup = await _repository.GetByIdAsync(ownFile.Id);
        ownLookup.Should().NotBeNull();

        StoredFile? crossTenantLookup = await _repository.GetByIdAsync(otherFile.Id);

        crossTenantLookup.Should().BeNull(
            "the compiled GetByIdAsync query must filter on the executing context's tenant, not leak another tenant's file");
    }

    [Fact]
    public async Task GetByIdAsync_CompiledQuery_RebindsForASecondTenantExecutingIt()
    {
        StoredFile ownFile = await SeedFileAsync(DbContext, _repository, "first-tenant.txt");

        TenantId otherTenantId = TenantId.New();
        await using StorageDbContext otherDbContext = CreateDbContextForTenant(otherTenantId);
        StoredFileRepository otherRepository = new(otherDbContext);
        StoredFile otherFile = await SeedFileAsync(otherDbContext, otherRepository, "second-tenant.txt");

        await _repository.GetByIdAsync(ownFile.Id);

        StoredFile? leakedIntoSecondTenant = await otherRepository.GetByIdAsync(ownFile.Id);
        StoredFile? secondTenantsOwnFile = await otherRepository.GetByIdAsync(otherFile.Id);

        leakedIntoSecondTenant.Should().BeNull(
            "a second tenant reusing the already-compiled query must not inherit the first tenant's filter value");
        secondTenantsOwnFile.Should().NotBeNull(
            "the rebound filter must still match the second tenant's own rows");
    }

    [Fact]
    public async Task GetByIdAsync_CompiledQuery_ReturnsNothingForAnUninvolvedThirdTenant()
    {
        StoredFile firstFile = await SeedFileAsync(DbContext, _repository, "tenant-a.txt");

        TenantId secondTenantId = TenantId.New();
        await using StorageDbContext secondDbContext = CreateDbContextForTenant(secondTenantId);
        StoredFileRepository secondRepository = new(secondDbContext);
        StoredFile secondFile = await SeedFileAsync(secondDbContext, secondRepository, "tenant-b.txt");

        await _repository.GetByIdAsync(firstFile.Id);
        await secondRepository.GetByIdAsync(secondFile.Id);

        await using StorageDbContext thirdDbContext = CreateDbContextForTenant(TenantId.New(), "ThirdTenant");
        StoredFileRepository thirdRepository = new(thirdDbContext);

        StoredFile? firstLeak = await thirdRepository.GetByIdAsync(firstFile.Id);
        StoredFile? secondLeak = await thirdRepository.GetByIdAsync(secondFile.Id);

        firstLeak.Should().BeNull("a tenant with no data must not see the first tenant's file");
        secondLeak.Should().BeNull("a tenant with no data must not see the second tenant's file");
    }

    [Fact]
    public async Task GetByIdAsync_CompiledQuery_TracksSetTenantOnAReusedContextInstance()
    {
        // Models the pooled DbContextFactory path the ApplyTenantQueryFilters comment calls out:
        // one context instance is leased, used, and re-leased under a different tenant.
        StoredFile firstTenantFile = await SeedFileAsync(DbContext, _repository, "lease-one.txt");

        StoredFile? beforeRelease = await _repository.GetByIdAsync(firstTenantFile.Id);
        beforeRelease.Should().NotBeNull();

        TenantId secondTenantId = TenantId.New();
        DbContext.ChangeTracker.Clear();
        DbContext.SetTenant(secondTenantId);

        StoredFile secondTenantFile = await SeedFileAsync(DbContext, _repository, "lease-two.txt");

        StoredFile? staleLookup = await _repository.GetByIdAsync(firstTenantFile.Id);
        StoredFile? currentLookup = await _repository.GetByIdAsync(secondTenantFile.Id);

        staleLookup.Should().BeNull(
            "after SetTenant the compiled query must read the context's current tenant id, not the one captured at first execution");
        currentLookup.Should().NotBeNull(
            "the re-leased context must see the rows belonging to its new tenant");
    }

    private static async Task<StoredFile> SeedFileAsync(
        StorageDbContext context,
        StoredFileRepository repository,
        string fileName)
    {
        StorageBucket bucket = StorageBucket.Create(context.CurrentTenantId, $"bucket-{Guid.NewGuid():N}");
        context.Buckets.Add(bucket);
        await context.SaveChangesAsync();

        StoredFile file = StoredFile.Create(
            context.CurrentTenantId,
            bucket.Id,
            fileName,
            "text/plain",
            128,
            $"s3://bucket/{Guid.NewGuid()}/{fileName}",
            Guid.NewGuid());

        repository.Add(file);
        await repository.SaveChangesAsync();
        context.ChangeTracker.Clear();

        return file;
    }
}
