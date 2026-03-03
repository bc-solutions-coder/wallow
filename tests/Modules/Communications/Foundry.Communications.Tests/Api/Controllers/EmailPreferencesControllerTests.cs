using System.Security.Claims;
using Foundry.Communications.Api.Contracts.Email.Enums;
using Foundry.Communications.Api.Contracts.Email.Requests;
using Foundry.Communications.Api.Contracts.Email.Responses;
using Foundry.Communications.Api.Controllers;
using Foundry.Communications.Application.Channels.Email.Commands.UpdateEmailPreferences;
using Foundry.Communications.Application.Channels.Email.DTOs;
using Foundry.Communications.Application.Channels.Email.Queries.GetEmailPreferences;
using Foundry.Communications.Domain.Enums;
using Foundry.Shared.Kernel.Results;
using Foundry.Shared.Kernel.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Foundry.Communications.Tests.Api.Controllers;

public class EmailPreferencesControllerTests
{
    private readonly IMessageBus _bus;
    private readonly ICurrentUserService _currentUserService;
    private readonly EmailPreferencesController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public EmailPreferencesControllerTests()
    {
        _bus = Substitute.For<IMessageBus>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _currentUserService.GetCurrentUserId().Returns(_userId);
        _controller = new EmailPreferencesController(_bus, _currentUserService);

        ClaimsPrincipal user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _userId.ToString())
        }, "TestAuth"));
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    #region GetPreferences

    [Fact]
    public async Task GetPreferences_WithValidUser_ReturnsOkWithPreferences()
    {
        EmailPreferenceDto dto = new(Guid.NewGuid(), _userId, NotificationType.TaskAssigned, true, DateTime.UtcNow, null);
        _bus.InvokeAsync<Result<IReadOnlyList<EmailPreferenceDto>>>(Arg.Any<GetEmailPreferencesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<EmailPreferenceDto>>(new List<EmailPreferenceDto> { dto }));

        IActionResult result = await _controller.GetPreferences(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<EmailPreferenceResponse> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<EmailPreferenceResponse>>().Subject;
        responses.Should().HaveCount(1);
        responses[0].NotificationType.Should().Be(ApiNotificationType.TaskAssigned);
        responses[0].IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task GetPreferences_WithNoUserClaim_ReturnsUnauthorized()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        _currentUserService.GetCurrentUserId().Returns((Guid?)null);
        IActionResult result = await _controller.GetPreferences(CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetPreferences_PassesUserIdToQuery()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<EmailPreferenceDto>>>(Arg.Any<GetEmailPreferencesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<EmailPreferenceDto>>(new List<EmailPreferenceDto>()));

        await _controller.GetPreferences(CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<IReadOnlyList<EmailPreferenceDto>>>(
            Arg.Is<GetEmailPreferencesQuery>(q => q.UserId == _userId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPreferences_MapsAllFieldsCorrectly()
    {
        DateTime createdAt = DateTime.UtcNow;
        DateTime updatedAt = DateTime.UtcNow.AddMinutes(5);
        Guid prefId = Guid.NewGuid();
        EmailPreferenceDto dto = new(prefId, _userId, NotificationType.BillingInvoice, false, createdAt, updatedAt);
        _bus.InvokeAsync<Result<IReadOnlyList<EmailPreferenceDto>>>(Arg.Any<GetEmailPreferencesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<EmailPreferenceDto>>(new List<EmailPreferenceDto> { dto }));

        IActionResult result = await _controller.GetPreferences(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<EmailPreferenceResponse> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<EmailPreferenceResponse>>().Subject;
        EmailPreferenceResponse response = responses[0];
        response.Id.Should().Be(prefId);
        response.UserId.Should().Be(_userId);
        response.NotificationType.Should().Be(ApiNotificationType.BillingInvoice);
        response.IsEnabled.Should().BeFalse();
        response.CreatedAt.Should().Be(createdAt);
        response.UpdatedAt.Should().Be(updatedAt);
    }

    [Fact]
    public async Task GetPreferences_WhenFailure_ReturnsErrorResult()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<EmailPreferenceDto>>>(Arg.Any<GetEmailPreferencesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<IReadOnlyList<EmailPreferenceDto>>(Error.Unauthorized()));

        IActionResult result = await _controller.GetPreferences(CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    #endregion

    #region UpdatePreference

    [Fact]
    public async Task UpdatePreference_WhenSuccess_Returns204NoContent()
    {
        UpdateEmailPreferenceRequest request = new(ApiNotificationType.TaskAssigned, true);
        _bus.InvokeAsync<Result>(Arg.Any<UpdateEmailPreferencesCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        IActionResult result = await _controller.UpdatePreference(request, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task UpdatePreference_WithNoUserClaim_ReturnsUnauthorized()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);
        UpdateEmailPreferenceRequest request = new(ApiNotificationType.TaskAssigned, true);

        IActionResult result = await _controller.UpdatePreference(request, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task UpdatePreference_PassesCorrectFieldsToCommand()
    {
        UpdateEmailPreferenceRequest request = new(ApiNotificationType.BillingInvoice, false);
        _bus.InvokeAsync<Result>(Arg.Any<UpdateEmailPreferencesCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        await _controller.UpdatePreference(request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result>(
            Arg.Is<UpdateEmailPreferencesCommand>(c =>
                c.UserId == _userId &&
                c.NotificationType == NotificationType.BillingInvoice &&
                !c.IsEnabled),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdatePreference_WhenFailure_ReturnsErrorResult()
    {
        UpdateEmailPreferenceRequest request = new(ApiNotificationType.TaskAssigned, true);
        _bus.InvokeAsync<Result>(Arg.Any<UpdateEmailPreferencesCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(Error.Validation("Invalid preference")));

        IActionResult result = await _controller.UpdatePreference(request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task UpdatePreference_MapsApiEnumToDomainEnum()
    {
        UpdateEmailPreferenceRequest request = new(ApiNotificationType.SystemNotification, true);
        _bus.InvokeAsync<Result>(Arg.Any<UpdateEmailPreferencesCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        await _controller.UpdatePreference(request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result>(
            Arg.Is<UpdateEmailPreferencesCommand>(c => c.NotificationType == NotificationType.SystemNotification),
            Arg.Any<CancellationToken>());
    }

    #endregion
}
