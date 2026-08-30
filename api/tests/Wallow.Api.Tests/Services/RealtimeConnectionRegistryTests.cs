using Microsoft.AspNetCore.SignalR;
using Wallow.Api.Services;

namespace Wallow.Api.Tests.Services;

public sealed class RealtimeConnectionRegistryTests
{
    private static readonly Guid _tenantId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private readonly RealtimeConnectionRegistry _sut = new();

    [Fact]
    public void AbortConnectionsForClient_HangsUpOnlyConnectionsOpenedThroughThatClient()
    {
        HubCallerContext throughClient = Substitute.For<HubCallerContext>();
        HubCallerContext throughOtherClient = Substitute.For<HubCallerContext>();
        HubCallerContext withoutClient = Substitute.For<HubCallerContext>();
        _sut.Register("conn-app", "user-1", _tenantId, throughClient, clientId: "app-one");
        _sut.Register("conn-other-app", "user-2", _tenantId, throughOtherClient, clientId: "app-two");
        _sut.Register("conn-no-client", "user-1", _tenantId, withoutClient);

        _sut.AbortConnectionsForClient("app-one");

        throughClient.Received(1).Abort();
        throughOtherClient.DidNotReceive().Abort();
        withoutClient.DidNotReceive().Abort();
    }

    [Fact]
    public void AbortConnectionsForClient_ForgetsTheConnection_SoASecondRevocationIsSilent()
    {
        HubCallerContext context = Substitute.For<HubCallerContext>();
        _sut.Register("conn-app", "user-1", _tenantId, context, clientId: "app-one");

        _sut.AbortConnectionsForClient("app-one");
        _sut.AbortConnectionsForClient("app-one");

        context.Received(1).Abort();
    }

    [Fact]
    public void AbortConnectionsForUser_HangsUpThePersonInThatTenantOnly()
    {
        HubCallerContext inTenant = Substitute.For<HubCallerContext>();
        HubCallerContext elsewhere = Substitute.For<HubCallerContext>();
        _sut.Register("conn-here", "user-1", _tenantId, inTenant);
        _sut.Register("conn-there", "user-1", Guid.NewGuid(), elsewhere);

        _sut.AbortConnectionsForUser("user-1", _tenantId);

        inTenant.Received(1).Abort();
        elsewhere.DidNotReceive().Abort();
    }
}
