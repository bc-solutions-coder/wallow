using Wallow.Notifications.Domain.Channels.Push.Entities;
using Wallow.Notifications.Domain.Channels.Push.Enums;
using Wallow.Notifications.Infrastructure.Persistence.Repositories;

namespace Wallow.Notifications.Tests.Infrastructure.Persistence;

public sealed class TenantPushConfigurationRepositoryTests : RepositoryTestBase
{
    private readonly TenantPushConfigurationRepository _repository;

    public TenantPushConfigurationRepositoryTests()
    {
        _repository = new TenantPushConfigurationRepository(Context);
    }

    private static TenantPushConfiguration CreateConfiguration(
        PushPlatform platform = PushPlatform.Fcm,
        string credentials = "encrypted-creds")
    {
        TenantPushConfiguration config = TenantPushConfiguration.Create(
            TestTenantId,
            platform,
            credentials,
            TimeProvider.System);
        return config;
    }

    [Fact]
    public async Task UpsertAsync_AddsNewConfiguration()
    {
        TenantPushConfiguration config = CreateConfiguration(platform: PushPlatform.Fcm);

        await _repository.UpsertAsync(config);

        TenantPushConfiguration? result = await _repository.GetAsync();
        result.Should().NotBeNull();
        result!.Platform.Should().Be(PushPlatform.Fcm);
    }

    [Fact]
    public async Task UpsertAsync_UpdatesExistingConfiguration()
    {
        TenantPushConfiguration config = CreateConfiguration(credentials: "original");
        await _repository.UpsertAsync(config);

        config.UpdateCredentials("updated", TimeProvider.System);
        await _repository.UpsertAsync(config);

        TenantPushConfiguration? result = await _repository.GetAsync();
        result.Should().NotBeNull();
        result!.EncryptedCredentials.Should().Be("updated");
    }

    [Fact]
    public async Task GetAsync_WhenNoConfigurations_ReturnsNull()
    {
        TenantPushConfiguration? result = await _repository.GetAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByPlatformAsync_ReturnsMatchingPlatform()
    {
        await _repository.UpsertAsync(CreateConfiguration(platform: PushPlatform.Fcm, credentials: "fcm-creds"));
        await _repository.UpsertAsync(CreateConfiguration(platform: PushPlatform.Apns, credentials: "apns-creds"));

        TenantPushConfiguration? result = await _repository.GetByPlatformAsync(PushPlatform.Apns);

        result.Should().NotBeNull();
        result!.Platform.Should().Be(PushPlatform.Apns);
        result.EncryptedCredentials.Should().Be("apns-creds");
    }

    [Fact]
    public async Task GetByPlatformAsync_WhenNotFound_ReturnsNull()
    {
        TenantPushConfiguration? result = await _repository.GetByPlatformAsync(PushPlatform.Fcm);

        result.Should().BeNull();
    }

    // DeleteByPlatformAsync uses ExecuteDeleteAsync which is not supported by InMemory provider.
    // These methods require integration tests with a real database.
    [Fact(Skip = "ExecuteDeleteAsync is not supported by EF Core InMemory provider")]
    public async Task DeleteByPlatformAsync_RemovesConfiguration()
    {
        await _repository.UpsertAsync(CreateConfiguration(platform: PushPlatform.Fcm));
        await _repository.UpsertAsync(CreateConfiguration(platform: PushPlatform.Apns));

        await _repository.DeleteByPlatformAsync(PushPlatform.Fcm);

        TenantPushConfiguration? deleted = await _repository.GetByPlatformAsync(PushPlatform.Fcm);
        TenantPushConfiguration? remaining = await _repository.GetByPlatformAsync(PushPlatform.Apns);

        deleted.Should().BeNull();
        remaining.Should().NotBeNull();
    }

    [Fact(Skip = "ExecuteDeleteAsync is not supported by EF Core InMemory provider")]
    public async Task DeleteByPlatformAsync_WhenNotExists_DoesNotThrow()
    {
        Func<Task> act = async () => await _repository.DeleteByPlatformAsync(PushPlatform.Fcm);

        await act.Should().NotThrowAsync();
    }
}
