using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Infrastructure.Services;

namespace Wallow.Identity.Tests.Infrastructure;

public class OrganizationMfaPolicyServiceTests
{
    private readonly OrganizationMfaPolicyService _sut = new();

    [Fact]
    public async Task CheckAsync_WithAnyUserId_ReturnsFalseRequiresMfa()
    {
        OrgMfaPolicyResult result = await _sut.CheckAsync(Guid.NewGuid(), CancellationToken.None);

        result.RequiresMfa.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_WithAnyUserId_ReturnsFalseIsInGracePeriod()
    {
        OrgMfaPolicyResult result = await _sut.CheckAsync(Guid.NewGuid(), CancellationToken.None);

        result.IsInGracePeriod.Should().BeFalse();
    }
}
