using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Identity.Tests.Infrastructure;

public class ScimGroupServiceTests
{
    private readonly IOrganizationService _organizationService;
    private readonly IScimConfigurationRepository _scimRepository;
    private readonly IScimSyncLogRepository _syncLogRepository;
    private readonly ITenantContext _tenantContext;
    private readonly FakeTimeProvider _timeProvider;
    private readonly ScimGroupService _sut;
    private readonly TenantId _tenantId = new TenantId(Guid.NewGuid());

    public ScimGroupServiceTests()
    {
        _organizationService = Substitute.For<IOrganizationService>();
        _scimRepository = Substitute.For<IScimConfigurationRepository>();
        _syncLogRepository = Substitute.For<IScimSyncLogRepository>();
        _tenantContext = Substitute.For<ITenantContext>();
        _tenantContext.TenantId.Returns(_tenantId);
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 3, 23, 12, 0, 0, TimeSpan.Zero));

        _sut = new ScimGroupService(
            _organizationService,
            _scimRepository,
            _syncLogRepository,
            _tenantContext,
            NullLogger<ScimGroupService>.Instance,
            _timeProvider);
    }

    [Fact]
    public async Task CreateGroupAsync_WithMembers_CreatesOrgAndAddsMembers()
    {
        Guid orgId = Guid.NewGuid();
        Guid memberId = Guid.NewGuid();
        _organizationService.CreateOrganizationAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(orgId);
        _organizationService.GetOrganizationByIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(new OrganizationDto(orgId, "Test Group", null, 0));
        _organizationService.GetMembersAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(new List<UserDto> { new UserDto(memberId, "member@example.com", "Member", "One", true, []) });

        ScimGroupRequest request = new ScimGroupRequest
        {
            DisplayName = "Test Group",
            Members = [new ScimMember { Value = memberId.ToString() }]
        };

        ScimGroup result = await _sut.CreateGroupAsync(request);

        result.DisplayName.Should().Be("Test Group");
        result.Id.Should().Be(orgId.ToString());
        await _organizationService.Received(1).AddMemberAsync(orgId, memberId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateGroupAsync_WithNoMembers_CreatesOrgOnly()
    {
        Guid orgId = Guid.NewGuid();
        _organizationService.CreateOrganizationAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(orgId);
        _organizationService.GetOrganizationByIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(new OrganizationDto(orgId, "Test Group", null, 0));
        _organizationService.GetMembersAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(new List<UserDto>());

        ScimGroupRequest request = new ScimGroupRequest { DisplayName = "Test Group" };

        ScimGroup result = await _sut.CreateGroupAsync(request);

        result.DisplayName.Should().Be("Test Group");
        await _organizationService.DidNotReceive().AddMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetGroupAsync_InvalidGuid_ReturnsNull()
    {
        ScimGroup? result = await _sut.GetGroupAsync("not-a-guid", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetGroupAsync_OrgNotFound_ReturnsNull()
    {
        Guid orgId = Guid.NewGuid();
        _organizationService.GetOrganizationByIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns((OrganizationDto?)null);

        ScimGroup? result = await _sut.GetGroupAsync(orgId.ToString(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetGroupAsync_ValidOrg_ReturnsMappedGroup()
    {
        Guid orgId = Guid.NewGuid();
        Guid memberId = Guid.NewGuid();
        _organizationService.GetOrganizationByIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(new OrganizationDto(orgId, "Engineers", null, 0));
        _organizationService.GetMembersAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(new List<UserDto> { new UserDto(memberId, "user@test.com", "Test", "User", true, []) });

        ScimGroup? result = await _sut.GetGroupAsync(orgId.ToString(), CancellationToken.None);

        result.Should().NotBeNull();
        result!.DisplayName.Should().Be("Engineers");
        result.Members.Should().HaveCount(1);
        result.Members![0].Value.Should().Be(memberId.ToString());
        result.Meta!.ResourceType.Should().Be("Group");
        result.Meta.Location.Should().Be($"/scim/v2/Groups/{orgId}");
    }

    [Fact]
    public async Task UpdateGroupAsync_InvalidGuid_ThrowsInvalidOperationException()
    {
        ScimGroupRequest request = new ScimGroupRequest { DisplayName = "Updated" };

        Func<Task> act = () => _sut.UpdateGroupAsync("not-a-guid", request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Invalid group ID*");
    }

    [Fact]
    public async Task UpdateGroupAsync_OrgNotFound_ThrowsInvalidOperationException()
    {
        Guid orgId = Guid.NewGuid();
        _organizationService.GetOrganizationByIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns((OrganizationDto?)null);

        ScimGroupRequest request = new ScimGroupRequest { DisplayName = "Updated" };

        Func<Task> act = () => _sut.UpdateGroupAsync(orgId.ToString(), request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Group*not found");
    }

    [Fact]
    public async Task UpdateGroupAsync_WithMemberChanges_AddsAndRemovesMembers()
    {
        Guid orgId = Guid.NewGuid();
        Guid existingMemberId = Guid.NewGuid();
        Guid newMemberId = Guid.NewGuid();

        _organizationService.GetOrganizationByIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(new OrganizationDto(orgId, "Group", null, 0));
        _organizationService.GetMembersAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(
                new List<UserDto> { new UserDto(existingMemberId, "old@test.com", "Old", "Member", true, []) },
                new List<UserDto> { new UserDto(newMemberId, "new@test.com", "New", "Member", true, []) });

        ScimGroupRequest request = new ScimGroupRequest
        {
            DisplayName = "Group",
            Members = [new ScimMember { Value = newMemberId.ToString() }]
        };

        await _sut.UpdateGroupAsync(orgId.ToString(), request);

        await _organizationService.Received(1).RemoveMemberAsync(orgId, existingMemberId, Arg.Any<CancellationToken>());
        await _organizationService.Received(1).AddMemberAsync(orgId, newMemberId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteGroupAsync_InvalidGuid_ThrowsInvalidOperationException()
    {
        Func<Task> act = () => _sut.DeleteGroupAsync("not-a-guid");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Invalid group ID*");
    }

    [Fact]
    public async Task DeleteGroupAsync_ValidGroup_RemovesAllMembers()
    {
        Guid orgId = Guid.NewGuid();
        Guid member1 = Guid.NewGuid();
        Guid member2 = Guid.NewGuid();

        _organizationService.GetMembersAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(new List<UserDto>
            {
                new UserDto(member1, "a@test.com", "A", "B", true, []),
                new UserDto(member2, "c@test.com", "C", "D", true, [])
            });

        await _sut.DeleteGroupAsync(orgId.ToString());

        await _organizationService.Received(1).RemoveMemberAsync(orgId, member1, Arg.Any<CancellationToken>());
        await _organizationService.Received(1).RemoveMemberAsync(orgId, member2, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListGroupsAsync_ReturnsMappedGroups()
    {
        Guid orgId = Guid.NewGuid();
        _organizationService.GetOrganizationsAsync(
                Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<OrganizationDto> { new OrganizationDto(orgId, "Team A", null, 0) });
        _organizationService.GetOrganizationByIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(new OrganizationDto(orgId, "Team A", null, 0));
        _organizationService.GetMembersAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(new List<UserDto>());

        ScimListRequest request = new ScimListRequest();

        ScimListResponse<ScimGroup> result = await _sut.ListGroupsAsync(request);

        result.Resources.Should().HaveCount(1);
        result.Resources[0].DisplayName.Should().Be("Team A");
        result.TotalResults.Should().Be(1);
    }

    [Fact]
    public async Task ListGroupsAsync_EmptyOrganizations_ReturnsEmptyList()
    {
        _organizationService.GetOrganizationsAsync(
                Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<OrganizationDto>());

        ScimListRequest request = new ScimListRequest();

        ScimListResponse<ScimGroup> result = await _sut.ListGroupsAsync(request);

        result.Resources.Should().BeEmpty();
        result.TotalResults.Should().Be(0);
    }
}
