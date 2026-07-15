using OpenIddict.Abstractions;
using Wallow.Identity.Infrastructure.Extensions;

namespace Wallow.Identity.Tests.Infrastructure;

public sealed class OpenIddictApplicationExtensionsTests
{
    [Fact]
    public void SetTenantId_ThenGetTenantId_ReturnsRoundTrippedValue()
    {
        OpenIddictApplicationDescriptor descriptor = new();

        descriptor.SetTenantId("tenant-abc");

        string? result = descriptor.GetTenantId();
        result.Should().Be("tenant-abc");
    }

    [Fact]
    public void GetTenantId_WhenNotSet_ReturnsNull()
    {
        OpenIddictApplicationDescriptor descriptor = new();

        string? result = descriptor.GetTenantId();

        result.Should().BeNull();
    }

    [Fact]
    public void SetTenantId_CalledTwice_OverwritesPreviousValue()
    {
        OpenIddictApplicationDescriptor descriptor = new();

        descriptor.SetTenantId("first");
        descriptor.SetTenantId("second");

        string? result = descriptor.GetTenantId();
        result.Should().Be("second");
    }

    [Fact]
    public void SetTenantId_WithEmptyString_RoundTripsEmptyString()
    {
        OpenIddictApplicationDescriptor descriptor = new();

        descriptor.SetTenantId(string.Empty);

        string? result = descriptor.GetTenantId();
        result.Should().BeEmpty();
    }
}
