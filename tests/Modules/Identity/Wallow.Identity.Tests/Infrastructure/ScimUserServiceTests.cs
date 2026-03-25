using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Identity.Tests.Infrastructure;

public class ScimUserServiceTests
{
    private readonly UserManager<WallowUser> _userManager;
    private readonly RoleManager<WallowRole> _roleManager;
    private readonly IOrganizationService _organizationService;
    private readonly IScimConfigurationRepository _scimRepository;
    private readonly IScimSyncLogRepository _syncLogRepository;
    private readonly ITenantContext _tenantContext;
    private readonly FakeTimeProvider _timeProvider;
    private readonly ScimUserService _sut;
    private readonly TenantId _tenantId = new(Guid.NewGuid());

    public ScimUserServiceTests()
    {
        _userManager = Substitute.For<UserManager<WallowUser>>(
            Substitute.For<IUserStore<WallowUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);
        _roleManager = Substitute.For<RoleManager<WallowRole>>(
            Substitute.For<IRoleStore<WallowRole>>(), null!, null!, null!, null!);
        _organizationService = Substitute.For<IOrganizationService>();
        _scimRepository = Substitute.For<IScimConfigurationRepository>();
        _syncLogRepository = Substitute.For<IScimSyncLogRepository>();
        _tenantContext = Substitute.For<ITenantContext>();
        _tenantContext.TenantId.Returns(_tenantId);
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 3, 24, 12, 0, 0, TimeSpan.Zero));

        _sut = new ScimUserService(
            _userManager, _roleManager, _organizationService,
            _scimRepository, _syncLogRepository, _tenantContext,
            NullLogger<ScimUserService>.Instance, _timeProvider);
    }

    #region CreateUserAsync

    [Fact]
    public async Task CreateUserAsync_Success_ReturnsScimUser()
    {
        ScimUserRequest request = new()
        {
            UserName = "jdoe",
            ExternalId = "ext-123",
            Name = new ScimName { GivenName = "John", FamilyName = "Doe" },
            Emails = [new ScimEmail { Value = "jdoe@test.com", Primary = true }]
        };

        _userManager.CreateAsync(Arg.Any<WallowUser>()).Returns(IdentityResult.Success);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns((ScimConfiguration?)null);

        // FindByIdAsync is called by GetUserAsync after creation
        WallowUser createdUser = WallowUser.Create(_tenantId.Value, "John", "Doe", "jdoe@test.com", _timeProvider);
        createdUser.UserName = "jdoe";
        _userManager.FindByIdAsync(Arg.Any<string>()).Returns(createdUser);

        ScimUser result = await _sut.CreateUserAsync(request);

        result.UserName.Should().Be("jdoe");
        result.Name!.GivenName.Should().Be("John");
        result.Name!.FamilyName.Should().Be("Doe");
        await _userManager.Received(1).CreateAsync(Arg.Any<WallowUser>());
    }

    [Fact]
    public async Task CreateUserAsync_DuplicateUserName_ThrowsWithAlreadyExists()
    {
        ScimUserRequest request = new() { UserName = "jdoe" };

        IdentityResult failResult = IdentityResult.Failed(new IdentityError { Code = "DuplicateUserName", Description = "Duplicate" });
        _userManager.CreateAsync(Arg.Any<WallowUser>()).Returns(failResult);

        Func<Task> act = () => _sut.CreateUserAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task CreateUserAsync_DuplicateEmail_ThrowsWithAlreadyExists()
    {
        ScimUserRequest request = new() { UserName = "jdoe" };

        IdentityResult failResult = IdentityResult.Failed(new IdentityError { Code = "DuplicateEmail", Description = "Duplicate email" });
        _userManager.CreateAsync(Arg.Any<WallowUser>()).Returns(failResult);

        Func<Task> act = () => _sut.CreateUserAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task CreateUserAsync_OtherFailure_ThrowsWithFailedToCreate()
    {
        ScimUserRequest request = new() { UserName = "jdoe" };

        IdentityResult failResult = IdentityResult.Failed(new IdentityError { Code = "Unknown", Description = "Something went wrong" });
        _userManager.CreateAsync(Arg.Any<WallowUser>()).Returns(failResult);

        Func<Task> act = () => _sut.CreateUserAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Failed to create user*");
    }

    [Fact]
    public async Task CreateUserAsync_NoExternalId_GeneratesGuid()
    {
        ScimUserRequest request = new() { UserName = "jdoe", ExternalId = null };

        _userManager.CreateAsync(Arg.Any<WallowUser>()).Returns(IdentityResult.Success);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns((ScimConfiguration?)null);

        WallowUser createdUser = WallowUser.Create(_tenantId.Value, "SCIM", "User", "jdoe", _timeProvider);
        createdUser.UserName = "jdoe";
        _userManager.FindByIdAsync(Arg.Any<string>()).Returns(createdUser);

        ScimUser result = await _sut.CreateUserAsync(request);

        result.Should().NotBeNull();
        _syncLogRepository.Received().Add(Arg.Any<ScimSyncLog>());
    }

    [Fact]
    public async Task CreateUserAsync_NoName_DefaultsToScimUser()
    {
        ScimUserRequest request = new() { UserName = "jdoe", Name = null };

        _userManager.CreateAsync(Arg.Any<WallowUser>()).Returns(IdentityResult.Success);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns((ScimConfiguration?)null);

        WallowUser createdUser = WallowUser.Create(_tenantId.Value, "SCIM", "User", "jdoe", _timeProvider);
        createdUser.UserName = "jdoe";
        _userManager.FindByIdAsync(Arg.Any<string>()).Returns(createdUser);

        ScimUser result = await _sut.CreateUserAsync(request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateUserAsync_WithDefaultRole_AssignsRole()
    {
        ScimUserRequest request = new() { UserName = "jdoe" };

        _userManager.CreateAsync(Arg.Any<WallowUser>()).Returns(IdentityResult.Success);

        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, Guid.Empty, _timeProvider);
        config.UpdateSettings(true, "member", false, Guid.Empty, _timeProvider);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        _roleManager.RoleExistsAsync("member").Returns(true);

        WallowUser createdUser = WallowUser.Create(_tenantId.Value, "SCIM", "User", "jdoe", _timeProvider);
        createdUser.UserName = "jdoe";
        _userManager.FindByIdAsync(Arg.Any<string>()).Returns(createdUser);
        _userManager.AddToRoleAsync(Arg.Any<WallowUser>(), "member").Returns(IdentityResult.Success);

        await _sut.CreateUserAsync(request);

        await _userManager.Received().AddToRoleAsync(Arg.Any<WallowUser>(), "member");
    }

    [Fact]
    public async Task CreateUserAsync_DefaultRoleNotFound_DoesNotAssign()
    {
        ScimUserRequest request = new() { UserName = "jdoe" };

        _userManager.CreateAsync(Arg.Any<WallowUser>()).Returns(IdentityResult.Success);

        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, Guid.Empty, _timeProvider);
        config.UpdateSettings(true, "nonexistent", false, Guid.Empty, _timeProvider);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        _roleManager.RoleExistsAsync("nonexistent").Returns(false);

        WallowUser createdUser = WallowUser.Create(_tenantId.Value, "SCIM", "User", "jdoe", _timeProvider);
        createdUser.UserName = "jdoe";
        _userManager.FindByIdAsync(Arg.Any<string>()).Returns(createdUser);

        await _sut.CreateUserAsync(request);

        await _userManager.DidNotReceive().AddToRoleAsync(Arg.Any<WallowUser>(), Arg.Any<string>());
    }

    [Fact]
    public async Task CreateUserAsync_AssignRoleFails_DoesNotThrow()
    {
        ScimUserRequest request = new() { UserName = "jdoe" };

        _userManager.CreateAsync(Arg.Any<WallowUser>()).Returns(IdentityResult.Success);

        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, Guid.Empty, _timeProvider);
        config.UpdateSettings(true, "member", false, Guid.Empty, _timeProvider);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        _roleManager.RoleExistsAsync("member").Returns(true);
        _userManager.AddToRoleAsync(Arg.Any<WallowUser>(), "member")
            .Returns(IdentityResult.Failed(new IdentityError { Code = "Err", Description = "Fail" }));

        WallowUser createdUser = WallowUser.Create(_tenantId.Value, "SCIM", "User", "jdoe", _timeProvider);
        createdUser.UserName = "jdoe";
        _userManager.FindByIdAsync(Arg.Any<string>()).Returns(createdUser);

        // Should not throw even though role assignment failed
        ScimUser result = await _sut.CreateUserAsync(request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateUserAsync_OrgAddFails_DoesNotThrow()
    {
        ScimUserRequest request = new() { UserName = "jdoe" };

        _userManager.CreateAsync(Arg.Any<WallowUser>()).Returns(IdentityResult.Success);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns((ScimConfiguration?)null);
        _organizationService.AddMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns<Task>(x => throw new InvalidOperationException("org failure"));

        WallowUser createdUser = WallowUser.Create(_tenantId.Value, "SCIM", "User", "jdoe", _timeProvider);
        createdUser.UserName = "jdoe";
        _userManager.FindByIdAsync(Arg.Any<string>()).Returns(createdUser);

        ScimUser result = await _sut.CreateUserAsync(request);

        result.Should().NotBeNull();
    }

    #endregion

    #region UpdateUserAsync

    [Fact]
    public async Task UpdateUserAsync_UserNotFound_Throws()
    {
        ScimUserRequest request = new() { UserName = "jdoe" };
        _userManager.FindByIdAsync("missing").Returns((WallowUser?)null);

        Func<Task> act = () => _sut.UpdateUserAsync("missing", request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task UpdateUserAsync_Success_UpdatesAndReturns()
    {
        WallowUser user = WallowUser.Create(_tenantId.Value, "Old", "Name", "old@test.com", _timeProvider);
        string userId = user.Id.ToString();
        _userManager.FindByIdAsync(userId).Returns(user);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns((ScimConfiguration?)null);

        ScimUserRequest request = new()
        {
            UserName = "jdoe",
            Name = new ScimName { GivenName = "John", FamilyName = "Doe" },
            Emails = [new ScimEmail { Value = "jdoe@test.com", Primary = true }],
            Active = true
        };

        ScimUser result = await _sut.UpdateUserAsync(userId, request);

        result.UserName.Should().Be("jdoe");
        await _userManager.Received(1).SetLockoutEnabledAsync(user, false);
        await _userManager.Received(1).SetLockoutEndDateAsync(user, null);
    }

    [Fact]
    public async Task UpdateUserAsync_SetInactive_LocksOutUser()
    {
        WallowUser user = WallowUser.Create(_tenantId.Value, "John", "Doe", "jdoe@test.com", _timeProvider);
        string userId = user.Id.ToString();
        _userManager.FindByIdAsync(userId).Returns(user);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns((ScimConfiguration?)null);

        ScimUserRequest request = new() { UserName = "jdoe", Active = false };

        await _sut.UpdateUserAsync(userId, request);

        await _userManager.Received(1).SetLockoutEnabledAsync(user, true);
        await _userManager.Received(1).SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
    }

    [Fact]
    public async Task UpdateUserAsync_UpdateFails_Throws()
    {
        WallowUser user = WallowUser.Create(_tenantId.Value, "John", "Doe", "jdoe@test.com", _timeProvider);
        string userId = user.Id.ToString();
        _userManager.FindByIdAsync(userId).Returns(user);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Failed(new IdentityError { Code = "Err", Description = "Update failed" }));

        ScimUserRequest request = new() { UserName = "jdoe" };

        Func<Task> act = () => _sut.UpdateUserAsync(userId, request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Failed to update user*");
    }

    #endregion

    #region PatchUserAsync

    [Fact]
    public async Task PatchUserAsync_UserNotFound_Throws()
    {
        ScimPatchRequest request = new()
        {
            Operations = [new ScimPatchOperation { Op = "replace", Path = "active", Value = false }]
        };
        _userManager.FindByIdAsync("missing").Returns((WallowUser?)null);

        Func<Task> act = () => _sut.PatchUserAsync("missing", request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task PatchUserAsync_ReplaceActive_False_LocksOut()
    {
        WallowUser user = WallowUser.Create(_tenantId.Value, "John", "Doe", "jdoe@test.com", _timeProvider);
        string userId = user.Id.ToString();
        _userManager.FindByIdAsync(userId).Returns(user);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns((ScimConfiguration?)null);

        ScimPatchRequest request = new()
        {
            Operations = [new ScimPatchOperation { Op = "replace", Path = "active", Value = false }]
        };

        await _sut.PatchUserAsync(userId, request);

        await _userManager.Received(1).SetLockoutEnabledAsync(user, true);
        await _userManager.Received(1).SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
    }

    [Fact]
    public async Task PatchUserAsync_ReplaceActive_True_UnlocksUser()
    {
        WallowUser user = WallowUser.Create(_tenantId.Value, "John", "Doe", "jdoe@test.com", _timeProvider);
        string userId = user.Id.ToString();
        _userManager.FindByIdAsync(userId).Returns(user);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns((ScimConfiguration?)null);

        ScimPatchRequest request = new()
        {
            Operations = [new ScimPatchOperation { Op = "replace", Path = "active", Value = true }]
        };

        await _sut.PatchUserAsync(userId, request);

        await _userManager.Received(1).SetLockoutEnabledAsync(user, false);
        await _userManager.Received(1).SetLockoutEndDateAsync(user, null);
    }

    [Fact]
    public async Task PatchUserAsync_ReplaceUserName_UpdatesUserName()
    {
        WallowUser user = WallowUser.Create(_tenantId.Value, "John", "Doe", "jdoe@test.com", _timeProvider);
        string userId = user.Id.ToString();
        _userManager.FindByIdAsync(userId).Returns(user);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns((ScimConfiguration?)null);

        ScimPatchRequest request = new()
        {
            Operations = [new ScimPatchOperation { Op = "replace", Path = "userName", Value = "newname" }]
        };

        await _sut.PatchUserAsync(userId, request);

        user.UserName.Should().Be("newname");
    }

    [Fact]
    public async Task PatchUserAsync_ReplaceEmails_UpdatesEmail()
    {
        WallowUser user = WallowUser.Create(_tenantId.Value, "John", "Doe", "jdoe@test.com", _timeProvider);
        string userId = user.Id.ToString();
        _userManager.FindByIdAsync(userId).Returns(user);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns((ScimConfiguration?)null);

        ScimPatchRequest request = new()
        {
            Operations = [new ScimPatchOperation { Op = "replace", Path = "emails", Value = "new@test.com" }]
        };

        await _sut.PatchUserAsync(userId, request);

        user.Email.Should().Be("new@test.com");
    }

    [Fact]
    public async Task PatchUserAsync_AddOperation_WorksLikeReplace()
    {
        WallowUser user = WallowUser.Create(_tenantId.Value, "John", "Doe", "jdoe@test.com", _timeProvider);
        string userId = user.Id.ToString();
        _userManager.FindByIdAsync(userId).Returns(user);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns((ScimConfiguration?)null);

        ScimPatchRequest request = new()
        {
            Operations = [new ScimPatchOperation { Op = "add", Path = "userName", Value = "added" }]
        };

        await _sut.PatchUserAsync(userId, request);

        user.UserName.Should().Be("added");
    }

    [Fact]
    public async Task PatchUserAsync_RemoveOperation_IsNoOp()
    {
        WallowUser user = WallowUser.Create(_tenantId.Value, "John", "Doe", "jdoe@test.com", _timeProvider);
        string userId = user.Id.ToString();
        _userManager.FindByIdAsync(userId).Returns(user);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns((ScimConfiguration?)null);

        ScimPatchRequest request = new()
        {
            Operations = [new ScimPatchOperation { Op = "remove", Path = "userName" }]
        };

        await _sut.PatchUserAsync(userId, request);

        // remove is a no-op, so no lockout calls
        await _userManager.DidNotReceive().SetLockoutEnabledAsync(Arg.Any<WallowUser>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task PatchUserAsync_UpdateFails_Throws()
    {
        WallowUser user = WallowUser.Create(_tenantId.Value, "John", "Doe", "jdoe@test.com", _timeProvider);
        string userId = user.Id.ToString();
        _userManager.FindByIdAsync(userId).Returns(user);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Failed(new IdentityError { Code = "Err", Description = "Patch fail" }));

        ScimPatchRequest request = new()
        {
            Operations = [new ScimPatchOperation { Op = "replace", Path = "active", Value = true }]
        };

        Func<Task> act = () => _sut.PatchUserAsync(userId, request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Failed to patch user*");
    }

    [Fact]
    public async Task PatchUserAsync_ActiveAsString_ParsesCorrectly()
    {
        WallowUser user = WallowUser.Create(_tenantId.Value, "John", "Doe", "jdoe@test.com", _timeProvider);
        string userId = user.Id.ToString();
        _userManager.FindByIdAsync(userId).Returns(user);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns((ScimConfiguration?)null);

        ScimPatchRequest request = new()
        {
            Operations = [new ScimPatchOperation { Op = "replace", Path = "active", Value = "false" }]
        };

        await _sut.PatchUserAsync(userId, request);

        await _userManager.Received(1).SetLockoutEnabledAsync(user, true);
    }

    [Fact]
    public async Task PatchUserAsync_EmailWorkTypePath_UpdatesEmail()
    {
        WallowUser user = WallowUser.Create(_tenantId.Value, "John", "Doe", "jdoe@test.com", _timeProvider);
        string userId = user.Id.ToString();
        _userManager.FindByIdAsync(userId).Returns(user);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns((ScimConfiguration?)null);

        ScimPatchRequest request = new()
        {
            Operations = [new ScimPatchOperation { Op = "replace", Path = "emails[type eq \"work\"].value", Value = "work@test.com" }]
        };

        await _sut.PatchUserAsync(userId, request);

        user.Email.Should().Be("work@test.com");
    }

    [Fact]
    public async Task PatchUserAsync_EmailPrimaryPath_UpdatesEmail()
    {
        WallowUser user = WallowUser.Create(_tenantId.Value, "John", "Doe", "jdoe@test.com", _timeProvider);
        string userId = user.Id.ToString();
        _userManager.FindByIdAsync(userId).Returns(user);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns((ScimConfiguration?)null);

        ScimPatchRequest request = new()
        {
            Operations = [new ScimPatchOperation { Op = "replace", Path = "emails[primary eq true].value", Value = "primary@test.com" }]
        };

        await _sut.PatchUserAsync(userId, request);

        user.Email.Should().Be("primary@test.com");
    }

    #endregion

    #region DeleteUserAsync

    [Fact]
    public async Task DeleteUserAsync_UserNotFound_Throws()
    {
        _userManager.FindByIdAsync("missing").Returns((WallowUser?)null);

        Func<Task> act = () => _sut.DeleteUserAsync("missing");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task DeleteUserAsync_DeprovisionOnDelete_DeletesUser()
    {
        WallowUser user = WallowUser.Create(_tenantId.Value, "John", "Doe", "jdoe@test.com", _timeProvider);
        string userId = user.Id.ToString();
        _userManager.FindByIdAsync(userId).Returns(user);
        _userManager.DeleteAsync(user).Returns(IdentityResult.Success);

        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, Guid.Empty, _timeProvider);
        config.UpdateSettings(true, null, true, Guid.Empty, _timeProvider);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        await _sut.DeleteUserAsync(userId);

        await _userManager.Received(1).DeleteAsync(user);
    }

    [Fact]
    public async Task DeleteUserAsync_NoDeprovision_DisablesUser()
    {
        WallowUser user = WallowUser.Create(_tenantId.Value, "John", "Doe", "jdoe@test.com", _timeProvider);
        string userId = user.Id.ToString();
        _userManager.FindByIdAsync(userId).Returns(user);

        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, Guid.Empty, _timeProvider);
        config.UpdateSettings(true, null, false, Guid.Empty, _timeProvider);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        await _sut.DeleteUserAsync(userId);

        await _userManager.DidNotReceive().DeleteAsync(Arg.Any<WallowUser>());
        await _userManager.Received(1).SetLockoutEnabledAsync(user, true);
        await _userManager.Received(1).SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
    }

    [Fact]
    public async Task DeleteUserAsync_NullConfig_DisablesUser()
    {
        WallowUser user = WallowUser.Create(_tenantId.Value, "John", "Doe", "jdoe@test.com", _timeProvider);
        string userId = user.Id.ToString();
        _userManager.FindByIdAsync(userId).Returns(user);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns((ScimConfiguration?)null);

        await _sut.DeleteUserAsync(userId);

        await _userManager.DidNotReceive().DeleteAsync(Arg.Any<WallowUser>());
        await _userManager.Received(1).SetLockoutEnabledAsync(user, true);
    }

    [Fact]
    public async Task DeleteUserAsync_DeleteFails_Throws()
    {
        WallowUser user = WallowUser.Create(_tenantId.Value, "John", "Doe", "jdoe@test.com", _timeProvider);
        string userId = user.Id.ToString();
        _userManager.FindByIdAsync(userId).Returns(user);
        _userManager.DeleteAsync(user).Returns(IdentityResult.Failed(new IdentityError { Code = "Err", Description = "Delete fail" }));

        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, Guid.Empty, _timeProvider);
        config.UpdateSettings(true, null, true, Guid.Empty, _timeProvider);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        Func<Task> act = () => _sut.DeleteUserAsync(userId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Failed to delete user*");
    }

    #endregion

    #region GetUserAsync

    [Fact]
    public async Task GetUserAsync_UserNotFound_ReturnsNull()
    {
        _userManager.FindByIdAsync("missing").Returns((WallowUser?)null);

        ScimUser? result = await _sut.GetUserAsync("missing");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserAsync_UserFound_ReturnsMappedScimUser()
    {
        WallowUser user = WallowUser.Create(_tenantId.Value, "John", "Doe", "jdoe@test.com", _timeProvider);
        user.UserName = "jdoe";
        string userId = user.Id.ToString();
        _userManager.FindByIdAsync(userId).Returns(user);

        ScimUser? result = await _sut.GetUserAsync(userId);

        result.Should().NotBeNull();
        result!.UserName.Should().Be("jdoe");
        result.Name!.GivenName.Should().Be("John");
        result.Name!.FamilyName.Should().Be("Doe");
        result.Emails.Should().HaveCount(1);
        result.Emails![0].Value.Should().Be("jdoe@test.com");
        result.Meta!.ResourceType.Should().Be("User");
    }

    [Fact]
    public async Task GetUserAsync_Exception_ReturnsNull()
    {
        _userManager.FindByIdAsync("err").Returns<WallowUser?>(x => throw new InvalidOperationException("db error"));

        ScimUser? result = await _sut.GetUserAsync("err");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserAsync_UserWithNullEmail_ReturnsNullEmails()
    {
        WallowUser user = WallowUser.Create(_tenantId.Value, "John", "Doe", "placeholder@test.com", _timeProvider);
        user.Email = null;
        string userId = user.Id.ToString();
        _userManager.FindByIdAsync(userId).Returns(user);

        ScimUser? result = await _sut.GetUserAsync(userId);

        result.Should().NotBeNull();
        result!.Emails.Should().BeNull();
    }

    [Fact]
    public async Task GetUserAsync_LockedOutUser_ReturnsInactive()
    {
        WallowUser user = WallowUser.Create(_tenantId.Value, "John", "Doe", "jdoe@test.com", _timeProvider);
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        string userId = user.Id.ToString();
        _userManager.FindByIdAsync(userId).Returns(user);

        ScimUser? result = await _sut.GetUserAsync(userId);

        result.Should().NotBeNull();
        result!.Active.Should().BeFalse();
    }

    #endregion

    #region AssignDefaultRoleAsync (exercised through CreateUserAsync)

    [Fact]
    public async Task CreateUserAsync_DefaultRoleAssignThrows_DoesNotFail()
    {
        ScimUserRequest request = new() { UserName = "jdoe" };

        _userManager.CreateAsync(Arg.Any<WallowUser>()).Returns(IdentityResult.Success);

        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, Guid.Empty, _timeProvider);
        config.UpdateSettings(true, "member", false, Guid.Empty, _timeProvider);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        _roleManager.RoleExistsAsync("member").Returns(true);
        _userManager.AddToRoleAsync(Arg.Any<WallowUser>(), "member")
            .Returns<IdentityResult>(x => throw new InvalidOperationException("role assign error"));

        WallowUser createdUser = WallowUser.Create(_tenantId.Value, "SCIM", "User", "jdoe", _timeProvider);
        createdUser.UserName = "jdoe";
        _userManager.FindByIdAsync(Arg.Any<string>()).Returns(createdUser);

        ScimUser result = await _sut.CreateUserAsync(request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateUserAsync_DefaultRoleUserNotFoundForAssign_DoesNotFail()
    {
        ScimUserRequest request = new() { UserName = "jdoe" };

        _userManager.CreateAsync(Arg.Any<WallowUser>()).Returns(IdentityResult.Success);

        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, Guid.Empty, _timeProvider);
        config.UpdateSettings(true, "member", false, Guid.Empty, _timeProvider);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        _roleManager.RoleExistsAsync("member").Returns(true);

        // AssignDefaultRoleAsync calls FindByIdAsync first, then GetUserAsync calls it second
        int callCount = 0;
        WallowUser createdUser = WallowUser.Create(_tenantId.Value, "SCIM", "User", "jdoe", _timeProvider);
        createdUser.UserName = "jdoe";
        _userManager.FindByIdAsync(Arg.Any<string>()).Returns(callInfo =>
        {
            callCount++;
            // AssignDefaultRoleAsync is the first call; return null for it
            return callCount == 1 ? null : createdUser;
        });

        ScimUser result = await _sut.CreateUserAsync(request);

        result.Should().NotBeNull();
    }

    #endregion
}
