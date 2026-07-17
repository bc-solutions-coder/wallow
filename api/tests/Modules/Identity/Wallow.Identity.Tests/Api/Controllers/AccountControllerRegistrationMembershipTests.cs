using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wolverine;

namespace Wallow.Identity.Tests.Api.Controllers;

/// <summary>
/// Wallow-vec7.8: the registration-time membership request. The caller is still anonymous when the
/// register screen's org-domain interstitial is answered, so the request is opted into on the
/// already-anonymous Register call and created server-side for the user Register just made. The
/// domain is derived from that user's own address, so the anonymous caller never names it.
/// </summary>
public sealed class AccountControllerRegistrationMembershipTests
{
    private readonly AccountController _controller;
    private readonly UserManager<WallowUser> _userManager;
    private readonly IDomainAssignmentService _domainAssignmentService;

    public AccountControllerRegistrationMembershipTests()
    {
        _userManager = Substitute.For<UserManager<WallowUser>>(
            Substitute.For<IUserStore<WallowUser>>(), null, null, null, null, null, null, null, null);

        SignInManager<WallowUser> signInManager = Substitute.For<SignInManager<WallowUser>>(
            _userManager,
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<WallowUser>>(),
            Substitute.For<IOptions<IdentityOptions>>(),
            Substitute.For<ILogger<SignInManager<WallowUser>>>(),
            Substitute.For<IAuthenticationSchemeProvider>(),
            Substitute.For<IUserConfirmation<WallowUser>>());

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuthUrl"] = "http://localhost:5002"
            })
            .Build();

        _domainAssignmentService = Substitute.For<IDomainAssignmentService>();

        _controller = new AccountController(
            signInManager,
            configuration,
            Substitute.For<IRedirectUriValidator>(),
            Substitute.For<IDataProtectionProvider>(),
            Substitute.For<IAuthenticationSchemeProvider>(),
            Substitute.For<IMessageBus>(),
            Substitute.For<IClientTenantResolver>(),
            Substitute.For<IOrganizationService>(),
            _domainAssignmentService,
            Substitute.For<IPasswordlessService>(),
            Substitute.For<IMfaExemptionChecker>(),
            Substitute.For<IMfaService>(),
            Substitute.For<IMfaPartialAuthService>(),
            Substitute.For<IOrganizationMfaPolicyService>(),
            Substitute.For<IMfaLockoutService>(),
            Substitute.For<IConnectionMultiplexer>(),
            Substitute.For<ILogger<AccountController>>(),
            TimeProvider.System);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    /// <summary>
    /// Stubs a successful registration and captures the user the controller built, whose Id is the
    /// one the membership request must be attributed to.
    /// </summary>
    private CapturedUser GivenRegistrationSucceeds(bool passwordless = false)
    {
        CapturedUser captured = new();

        if (passwordless)
        {
            _userManager.CreateAsync(Arg.Any<WallowUser>()).Returns(ci =>
            {
                captured.User = ci.Arg<WallowUser>();
                return IdentityResult.Success;
            });
        }
        else
        {
            _userManager.CreateAsync(Arg.Any<WallowUser>(), "Password1!").Returns(ci =>
            {
                captured.User = ci.Arg<WallowUser>();
                return IdentityResult.Success;
            });
        }

        _userManager.AddToRoleAsync(Arg.Any<WallowUser>(), "user").Returns(IdentityResult.Success);
        _userManager.GenerateEmailConfirmationTokenAsync(Arg.Any<WallowUser>()).Returns("token123");
        return captured;
    }

    private sealed class CapturedUser
    {
        public WallowUser? User { get; set; }
    }

    [Fact]
    public async Task Register_WithRequestOrgMembership_RequestsMembershipForTheNewUser()
    {
        CapturedUser captured = GivenRegistrationSucceeds();

        IActionResult result = await _controller.Register(new AccountRegisterRequest(
            "new@example.com", "Password1!", "Password1!", RequestOrgMembership: true));

        result.Should().BeOfType<OkObjectResult>();
        captured.User.Should().NotBeNull();
        await _domainAssignmentService.Received(1).RequestMembershipForRegistrationAsync(
            captured.User!.Id, "new@example.com", Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The request must be built from the address the user actually registered, not from anything
    /// the anonymous caller could name: this is what keeps the endpoint's [Authorize] meaningful.
    /// </summary>
    [Fact]
    public async Task Register_WithRequestOrgMembership_NeverRequestsMembershipByCallerSuppliedDomain()
    {
        GivenRegistrationSucceeds();

        await _controller.Register(new AccountRegisterRequest(
            "new@example.com", "Password1!", "Password1!", RequestOrgMembership: true));

        await _domainAssignmentService.Received(1).RequestMembershipForRegistrationAsync(
            Arg.Any<Guid>(), "new@example.com", Arg.Any<CancellationToken>());
        await _domainAssignmentService.DidNotReceive().RequestMembershipAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_WithRequestOrgMembership_StillReturnsSucceeded()
    {
        GivenRegistrationSucceeds();
        _domainAssignmentService.RequestMembershipForRegistrationAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        IActionResult result = await _controller.Register(new AccountRegisterRequest(
            "new@example.com", "Password1!", "Password1!", RequestOrgMembership: true));

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        System.Text.Json.JsonSerializer.Serialize(ok.Value).Should().Contain("succeeded");
    }

    /// <summary>
    /// A null result means "no verified organization owns that domain" — an ordinary answer, not a
    /// failure. The account was created either way, so registration must not report an error.
    /// </summary>
    [Fact]
    public async Task Register_WhenNoVerifiedDomainMatches_StillReturnsSucceeded()
    {
        GivenRegistrationSucceeds();
        _domainAssignmentService.RequestMembershipForRegistrationAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Guid?)null);

        IActionResult result = await _controller.Register(new AccountRegisterRequest(
            "new@nobody.com", "Password1!", "Password1!", RequestOrgMembership: true));

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Register_PasswordlessWithRequestOrgMembership_RequestsMembershipForTheNewUser()
    {
        CapturedUser captured = GivenRegistrationSucceeds(passwordless: true);

        IActionResult result = await _controller.Register(new AccountRegisterRequest(
            "new@example.com", string.Empty, string.Empty,
            LoginMethod: "passwordless", RequestOrgMembership: true));

        result.Should().BeOfType<OkObjectResult>();
        captured.User.Should().NotBeNull();
        await _domainAssignmentService.Received(1).RequestMembershipForRegistrationAsync(
            captured.User!.Id, "new@example.com", Arg.Any<CancellationToken>());
    }

    /// <summary>The interstitial's DECLINE path: registration proceeds, nothing is enqueued.</summary>
    [Fact]
    public async Task Register_WithoutRequestOrgMembership_EnqueuesNothing()
    {
        GivenRegistrationSucceeds();

        IActionResult result = await _controller.Register(new AccountRegisterRequest(
            "new@example.com", "Password1!", "Password1!"));

        result.Should().BeOfType<OkObjectResult>();
        await _domainAssignmentService.DidNotReceive().RequestMembershipForRegistrationAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Opting in must be explicit — the flag defaults off for existing callers.</summary>
    [Fact]
    public void AccountRegisterRequest_DefaultsRequestOrgMembershipToFalse()
    {
        new AccountRegisterRequest("new@example.com", "Password1!", "Password1!")
            .RequestOrgMembership.Should().BeFalse();
    }

    [Fact]
    public async Task Register_WhenUserCreationFails_EnqueuesNothing()
    {
        _userManager.CreateAsync(Arg.Any<WallowUser>(), "Password1!")
            .Returns(IdentityResult.Failed(new IdentityError { Code = "DuplicateEmail", Description = "Email taken" }));

        IActionResult result = await _controller.Register(new AccountRegisterRequest(
            "taken@example.com", "Password1!", "Password1!", RequestOrgMembership: true));

        BadRequestObjectResult bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        System.Text.Json.JsonSerializer.Serialize(bad.Value).Should().Contain("email_taken");
        await _domainAssignmentService.DidNotReceive().RequestMembershipForRegistrationAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
