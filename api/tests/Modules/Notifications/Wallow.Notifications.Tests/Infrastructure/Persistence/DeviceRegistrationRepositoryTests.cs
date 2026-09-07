using Wallow.Notifications.Domain.Channels.Push;
using Wallow.Notifications.Domain.Channels.Push.Enums;
using Wallow.Notifications.Domain.Channels.Push.Identity;
using Wallow.Notifications.Infrastructure.Persistence.Repositories;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Notifications.Tests.Infrastructure.Persistence;

public sealed class DeviceRegistrationRepositoryTests : RepositoryTestBase
{
    private readonly DeviceRegistrationRepository _repository;

    public DeviceRegistrationRepositoryTests()
    {
        _repository = new DeviceRegistrationRepository(Context);
    }

    private static DeviceRegistration CreateRegistration(
        UserId? userId = null,
        PushPlatform platform = PushPlatform.Fcm,
        string token = "test-token",
        bool isActive = true)
    {
        DeviceRegistration registration = DeviceRegistration.Register(
            userId ?? UserId.New(),
            TestTenantId,
            platform,
            token,
            DateTimeOffset.UtcNow);

        if (!isActive)
        {
            registration.Deactivate();
        }

        return registration;
    }

    [Fact]
    public async Task Add_And_GetByIdAsync_ReturnsRegistration()
    {
        DeviceRegistration registration = CreateRegistration(token: "device-123");

        Context.DeviceRegistrations.Add(registration);
        await Context.SaveChangesAsync();

        DeviceRegistration? result = await _repository.GetByIdAsync(registration.Id);

        result.Should().NotBeNull();
        result!.Token.Should().Be("device-123");
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        DeviceRegistration? result = await _repository.GetByIdAsync(DeviceRegistrationId.New());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveByUserAsync_ReturnsOnlyActiveRegistrations()
    {
        UserId userId = UserId.New();
        Context.DeviceRegistrations.Add(CreateRegistration(userId: userId, token: "active1"));
        Context.DeviceRegistrations.Add(CreateRegistration(userId: userId, token: "active2"));
        Context.DeviceRegistrations.Add(CreateRegistration(userId: userId, token: "inactive", isActive: false));
        await Context.SaveChangesAsync();

        IReadOnlyList<DeviceRegistration> result = await _repository.GetActiveByUserAsync(userId);

        result.Should().HaveCount(2);
        result.Should().OnlyContain(d => d.IsActive);
    }

    [Fact]
    public async Task GetActiveByUserAsync_DoesNotReturnOtherUsersRegistrations()
    {
        UserId userId = UserId.New();
        UserId otherUserId = UserId.New();
        Context.DeviceRegistrations.Add(CreateRegistration(userId: userId, token: "mine"));
        Context.DeviceRegistrations.Add(CreateRegistration(userId: otherUserId, token: "theirs"));
        await Context.SaveChangesAsync();

        IReadOnlyList<DeviceRegistration> result = await _repository.GetActiveByUserAsync(userId);

        result.Should().HaveCount(1);
        result[0].Token.Should().Be("mine");
    }

    [Fact]
    public async Task GetActiveByUserAsync_WhenNoRegistrations_ReturnsEmpty()
    {
        IReadOnlyList<DeviceRegistration> result = await _repository.GetActiveByUserAsync(UserId.New());

        result.Should().BeEmpty();
    }

}
