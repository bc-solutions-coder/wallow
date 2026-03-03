using System.Security.Claims;
using Foundry.Configuration.Api.Contracts.Enums;
using Foundry.Configuration.Api.Contracts.Requests;
using Foundry.Configuration.Api.Contracts.Responses;
using Foundry.Configuration.Api.Controllers;
using Foundry.Configuration.Application.FeatureFlags.Commands.CreateFeatureFlag;
using Foundry.Configuration.Application.FeatureFlags.Commands.CreateOverride;
using Foundry.Configuration.Application.FeatureFlags.Commands.DeleteFeatureFlag;
using Foundry.Configuration.Application.FeatureFlags.Commands.DeleteOverride;
using Foundry.Configuration.Application.FeatureFlags.Commands.UpdateFeatureFlag;
using Foundry.Configuration.Application.FeatureFlags.Contracts;
using Foundry.Configuration.Application.FeatureFlags.DTOs;
using Foundry.Configuration.Application.FeatureFlags.Queries.GetAllFlags;
using Foundry.Configuration.Application.FeatureFlags.Queries.GetOverridesForFlag;
using Foundry.Configuration.Domain.Enums;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Shared.Kernel.Services;
using Foundry.Shared.Kernel.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Foundry.Configuration.Tests.Api.Controllers;

public class FeatureFlagsControllerTests
{
    private readonly IMessageBus _bus;
    private readonly IFeatureFlagService _featureFlagService;
    private readonly ICurrentUserService _currentUserService;
    private readonly FeatureFlagsController _controller;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public FeatureFlagsControllerTests()
    {
        _bus = Substitute.For<IMessageBus>();
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        _featureFlagService = Substitute.For<IFeatureFlagService>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _currentUserService.GetCurrentUserId().Returns(_userId);

        tenantContext.TenantId.Returns(new TenantId(_tenantId));

        _controller = new FeatureFlagsController(_bus, tenantContext, _featureFlagService, _currentUserService);

        ClaimsPrincipal user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _userId.ToString())
        }, "TestAuth"));
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    #region GetAll

    [Fact]
    public async Task GetAll_WhenSuccess_ReturnsOkWithFeatureFlagResponses()
    {
        List<FeatureFlagDto> flags = new()
        {
            CreateFlagDto("dark-mode", "Dark Mode"),
            CreateFlagDto("beta-feature", "Beta Feature")
        };
        _bus.InvokeAsync<Result<IReadOnlyList<FeatureFlagDto>>>(Arg.Any<GetAllFlagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<FeatureFlagDto>>(flags));

        IActionResult result = await _controller.GetAll(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<FeatureFlagResponse> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<FeatureFlagResponse>>().Subject;
        responses.Should().HaveCount(2);
        responses[0].Key.Should().Be("dark-mode");
        responses[1].Key.Should().Be("beta-feature");
    }

    [Fact]
    public async Task GetAll_WhenEmpty_ReturnsOkWithEmptyList()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<FeatureFlagDto>>>(Arg.Any<GetAllFlagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<FeatureFlagDto>>(new List<FeatureFlagDto>()));

        IActionResult result = await _controller.GetAll(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<FeatureFlagResponse> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<FeatureFlagResponse>>().Subject;
        responses.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAll_WhenFailure_ReturnsErrorResult()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<FeatureFlagDto>>>(Arg.Any<GetAllFlagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<IReadOnlyList<FeatureFlagDto>>(Error.Unauthorized()));

        IActionResult result = await _controller.GetAll(CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task GetAll_MapsAllFieldsCorrectly()
    {
        Guid flagId = Guid.NewGuid();
        DateTime createdAt = DateTime.UtcNow.AddDays(-1);
        DateTime updatedAt = DateTime.UtcNow;
        List<VariantWeightDto> variants = new() { new VariantWeightDto("control", 50), new VariantWeightDto("treatment", 50) };
        FeatureFlagDto dto = new(flagId, "ab-test", "A/B Test", "Testing", FlagType.Variant,
            false, null, variants, "control", createdAt, updatedAt);
        _bus.InvokeAsync<Result<IReadOnlyList<FeatureFlagDto>>>(Arg.Any<GetAllFlagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<FeatureFlagDto>>(new List<FeatureFlagDto> { dto }));

        IActionResult result = await _controller.GetAll(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<FeatureFlagResponse> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<FeatureFlagResponse>>().Subject;
        FeatureFlagResponse response = responses[0];
        response.Id.Should().Be(flagId);
        response.Key.Should().Be("ab-test");
        response.Name.Should().Be("A/B Test");
        response.Description.Should().Be("Testing");
        response.FlagType.Should().Be(ApiFlagType.Variant);
        response.DefaultEnabled.Should().BeFalse();
        response.RolloutPercentage.Should().BeNull();
        response.Variants.Should().HaveCount(2);
        response.DefaultVariant.Should().Be("control");
        response.CreatedAt.Should().Be(createdAt);
        response.UpdatedAt.Should().Be(updatedAt);
    }

    #endregion

    #region Create

    [Fact]
    public async Task Create_WithValidRequest_Returns201Created()
    {
        CreateFeatureFlagRequest request = new("dark-mode", "Dark Mode", null, ApiFlagType.Boolean, true, null, null, null);
        FeatureFlagDto dto = CreateFlagDto("dark-mode", "Dark Mode");
        _bus.InvokeAsync<Result<FeatureFlagDto>>(Arg.Any<CreateFeatureFlagCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.Create(request, CancellationToken.None);

        CreatedResult created = result.Should().BeOfType<CreatedResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        FeatureFlagResponse response = created.Value.Should().BeOfType<FeatureFlagResponse>().Subject;
        response.Key.Should().Be("dark-mode");
    }

    [Fact]
    public async Task Create_PassesCorrectFieldsToCommand()
    {
        List<VariantWeightDto> variants = new() { new VariantWeightDto("a", 50), new VariantWeightDto("b", 50) };
        CreateFeatureFlagRequest request = new("ab-test", "A/B Test", "Description", ApiFlagType.Variant, false, 50, variants, "a");
        FeatureFlagDto dto = CreateFlagDto("ab-test", "A/B Test");
        _bus.InvokeAsync<Result<FeatureFlagDto>>(Arg.Any<CreateFeatureFlagCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        await _controller.Create(request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<FeatureFlagDto>>(
            Arg.Is<CreateFeatureFlagCommand>(c =>
                c.Key == "ab-test" &&
                c.Name == "A/B Test" &&
                c.Description == "Description" &&
                c.FlagType == FlagType.Variant &&
!c.DefaultEnabled &&
                c.RolloutPercentage == 50 &&
                c.DefaultVariant == "a"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_WhenFailure_ThrowsDueToValueAccess()
    {
        CreateFeatureFlagRequest request = new("test", "Test", null, ApiFlagType.Boolean, true, null, null, null);
        _bus.InvokeAsync<Result<FeatureFlagDto>>(Arg.Any<CreateFeatureFlagCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<FeatureFlagDto>(Error.Validation("Invalid flag")));

        Func<Task> act = () => _controller.Create(request, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot access value of a failed result");
    }

    [Fact]
    public async Task Create_SetsLocationHeader()
    {
        Guid flagId = Guid.NewGuid();
        CreateFeatureFlagRequest request = new("dark-mode", "Dark Mode", null, ApiFlagType.Boolean, true, null, null, null);
        FeatureFlagDto dto = new(flagId, "dark-mode", "Dark Mode", null, FlagType.Boolean, true, null, null, null, DateTime.UtcNow, null);
        _bus.InvokeAsync<Result<FeatureFlagDto>>(Arg.Any<CreateFeatureFlagCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.Create(request, CancellationToken.None);

        CreatedResult created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/configuration/feature-flags/{flagId}");
    }

    #endregion

    #region Update

    [Fact]
    public async Task Update_WhenSuccess_ReturnsOkWithResponse()
    {
        Guid flagId = Guid.NewGuid();
        UpdateFeatureFlagRequest request = new("Updated Name", "Updated Desc", false, 75);
        FeatureFlagDto dto = CreateFlagDto("test-flag", "Updated Name", flagId);
        _bus.InvokeAsync<Result<FeatureFlagDto>>(Arg.Any<UpdateFeatureFlagCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.Update(flagId, request, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        FeatureFlagResponse response = ok.Value.Should().BeOfType<FeatureFlagResponse>().Subject;
        response.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task Update_PassesCorrectFieldsToCommand()
    {
        Guid flagId = Guid.NewGuid();
        UpdateFeatureFlagRequest request = new("Updated", "Desc", true, 80);
        FeatureFlagDto dto = CreateFlagDto("test", "Updated", flagId);
        _bus.InvokeAsync<Result<FeatureFlagDto>>(Arg.Any<UpdateFeatureFlagCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        await _controller.Update(flagId, request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<FeatureFlagDto>>(
            Arg.Is<UpdateFeatureFlagCommand>(c =>
                c.Id == flagId &&
                c.Name == "Updated" &&
                c.Description == "Desc" &&
                c.DefaultEnabled &&
                c.RolloutPercentage == 80),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_WhenNotFound_Returns404()
    {
        Guid flagId = Guid.NewGuid();
        UpdateFeatureFlagRequest request = new("Name", null, true, null);
        _bus.InvokeAsync<Result<FeatureFlagDto>>(Arg.Any<UpdateFeatureFlagCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<FeatureFlagDto>(Error.NotFound("FeatureFlag", flagId)));

        IActionResult result = await _controller.Update(flagId, request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Update_WhenValidationFailure_Returns400()
    {
        Guid flagId = Guid.NewGuid();
        UpdateFeatureFlagRequest request = new("", null, true, null);
        _bus.InvokeAsync<Result<FeatureFlagDto>>(Arg.Any<UpdateFeatureFlagCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<FeatureFlagDto>(Error.Validation("Name is required")));

        IActionResult result = await _controller.Update(flagId, request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    #endregion

    #region Delete

    [Fact]
    public async Task Delete_WhenSuccess_Returns204NoContent()
    {
        Guid flagId = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<DeleteFeatureFlagCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        IActionResult result = await _controller.Delete(flagId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Delete_PassesCorrectIdToCommand()
    {
        Guid flagId = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<DeleteFeatureFlagCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        await _controller.Delete(flagId, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result>(
            Arg.Is<DeleteFeatureFlagCommand>(c => c.Id == flagId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_WhenNotFound_Returns404()
    {
        Guid flagId = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<DeleteFeatureFlagCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(Error.NotFound("FeatureFlag", flagId)));

        IActionResult result = await _controller.Delete(flagId, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Delete_WhenValidationFailure_Returns400()
    {
        Guid flagId = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<DeleteFeatureFlagCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(Error.Validation("Cannot delete active flag")));

        IActionResult result = await _controller.Delete(flagId, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    #endregion

    #region GetOverrides

    [Fact]
    public async Task GetOverrides_WhenSuccess_ReturnsOkWithOverrides()
    {
        Guid flagId = Guid.NewGuid();
        List<FeatureFlagOverrideDto> overrides = new()
        {
            CreateOverrideDto(flagId),
            CreateOverrideDto(flagId)
        };
        _bus.InvokeAsync<Result<IReadOnlyList<FeatureFlagOverrideDto>>>(Arg.Any<GetOverridesForFlagQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<FeatureFlagOverrideDto>>(overrides));

        IActionResult result = await _controller.GetOverrides(flagId, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<FeatureFlagOverrideResponse> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<FeatureFlagOverrideResponse>>().Subject;
        responses.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetOverrides_PassesCorrectFlagIdToQuery()
    {
        Guid flagId = Guid.NewGuid();
        _bus.InvokeAsync<Result<IReadOnlyList<FeatureFlagOverrideDto>>>(Arg.Any<GetOverridesForFlagQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<FeatureFlagOverrideDto>>(new List<FeatureFlagOverrideDto>()));

        await _controller.GetOverrides(flagId, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<IReadOnlyList<FeatureFlagOverrideDto>>>(
            Arg.Is<GetOverridesForFlagQuery>(q => q.FlagId == flagId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetOverrides_WhenNotFound_Returns404()
    {
        Guid flagId = Guid.NewGuid();
        _bus.InvokeAsync<Result<IReadOnlyList<FeatureFlagOverrideDto>>>(Arg.Any<GetOverridesForFlagQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<IReadOnlyList<FeatureFlagOverrideDto>>(Error.NotFound("FeatureFlag", flagId)));

        IActionResult result = await _controller.GetOverrides(flagId, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetOverrides_MapsAllFieldsCorrectly()
    {
        Guid flagId = Guid.NewGuid();
        Guid overrideId = Guid.NewGuid();
        Guid overrideTenantId = Guid.NewGuid();
        Guid overrideUserId = Guid.NewGuid();
        DateTime expiresAt = DateTime.UtcNow.AddDays(30);
        DateTime createdAt = DateTime.UtcNow;
        FeatureFlagOverrideDto dto = new(overrideId, flagId, overrideTenantId, overrideUserId, true, "variant-a", expiresAt, createdAt);
        _bus.InvokeAsync<Result<IReadOnlyList<FeatureFlagOverrideDto>>>(Arg.Any<GetOverridesForFlagQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<FeatureFlagOverrideDto>>(new List<FeatureFlagOverrideDto> { dto }));

        IActionResult result = await _controller.GetOverrides(flagId, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<FeatureFlagOverrideResponse> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<FeatureFlagOverrideResponse>>().Subject;
        FeatureFlagOverrideResponse response = responses[0];
        response.Id.Should().Be(overrideId);
        response.FlagId.Should().Be(flagId);
        response.TenantId.Should().Be(overrideTenantId);
        response.UserId.Should().Be(overrideUserId);
        response.IsEnabled.Should().BeTrue();
        response.Variant.Should().Be("variant-a");
        response.ExpiresAt.Should().Be(expiresAt);
        response.CreatedAt.Should().Be(createdAt);
    }

    #endregion

    #region CreateOverride

    [Fact]
    public async Task CreateOverride_WithValidRequest_Returns201Created()
    {
        Guid flagId = Guid.NewGuid();
        CreateOverrideRequest request = new(Guid.NewGuid(), null, true, null, null);
        FeatureFlagOverrideDto dto = CreateOverrideDto(flagId);
        _bus.InvokeAsync<Result<FeatureFlagOverrideDto>>(Arg.Any<CreateOverrideCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.CreateOverride(flagId, request, CancellationToken.None);

        CreatedResult created = result.Should().BeOfType<CreatedResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
    }

    [Fact]
    public async Task CreateOverride_PassesCorrectFieldsToCommand()
    {
        Guid flagId = Guid.NewGuid();
        Guid overrideTenantId = Guid.NewGuid();
        Guid overrideUserId = Guid.NewGuid();
        DateTime expiresAt = DateTime.UtcNow.AddDays(7);
        CreateOverrideRequest request = new(overrideTenantId, overrideUserId, true, "variant-a", expiresAt);
        FeatureFlagOverrideDto dto = CreateOverrideDto(flagId);
        _bus.InvokeAsync<Result<FeatureFlagOverrideDto>>(Arg.Any<CreateOverrideCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        await _controller.CreateOverride(flagId, request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<FeatureFlagOverrideDto>>(
            Arg.Is<CreateOverrideCommand>(c =>
                c.FlagId == flagId &&
                c.TenantId == overrideTenantId &&
                c.UserId == overrideUserId &&
                c.IsEnabled == true &&
                c.Variant == "variant-a" &&
                c.ExpiresAt == expiresAt),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateOverride_WhenFailure_ThrowsDueToValueAccess()
    {
        Guid flagId = Guid.NewGuid();
        CreateOverrideRequest request = new(null, null, true, null, null);
        _bus.InvokeAsync<Result<FeatureFlagOverrideDto>>(Arg.Any<CreateOverrideCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<FeatureFlagOverrideDto>(Error.Validation("Invalid override")));

        Func<Task> act = () => _controller.CreateOverride(flagId, request, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot access value of a failed result");
    }

    [Fact]
    public async Task CreateOverride_SetsLocationHeader()
    {
        Guid flagId = Guid.NewGuid();
        Guid overrideId = Guid.NewGuid();
        CreateOverrideRequest request = new(null, null, true, null, null);
        FeatureFlagOverrideDto dto = new(overrideId, flagId, null, null, true, null, null, DateTime.UtcNow);
        _bus.InvokeAsync<Result<FeatureFlagOverrideDto>>(Arg.Any<CreateOverrideCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.CreateOverride(flagId, request, CancellationToken.None);

        CreatedResult created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/configuration/feature-flags/{flagId}/overrides/{overrideId}");
    }

    #endregion

    #region DeleteOverride

    [Fact]
    public async Task DeleteOverride_WhenSuccess_Returns204NoContent()
    {
        Guid flagId = Guid.NewGuid();
        Guid overrideId = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<DeleteOverrideCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        IActionResult result = await _controller.DeleteOverride(flagId, overrideId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteOverride_PassesOverrideIdToCommand()
    {
        Guid flagId = Guid.NewGuid();
        Guid overrideId = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<DeleteOverrideCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        await _controller.DeleteOverride(flagId, overrideId, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result>(
            Arg.Is<DeleteOverrideCommand>(c => c.Id == overrideId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteOverride_WhenNotFound_Returns404()
    {
        Guid flagId = Guid.NewGuid();
        Guid overrideId = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<DeleteOverrideCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(Error.NotFound("Override", overrideId)));

        IActionResult result = await _controller.DeleteOverride(flagId, overrideId, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    #endregion

    #region Evaluate

    [Fact]
    public async Task Evaluate_ReturnsOkWithEvaluatedFlags()
    {
        Dictionary<string, object> evaluatedFlags = new()
        {
            ["dark-mode"] = true,
            ["max-items"] = 100
        };
        _featureFlagService.GetAllFlagsAsync(_tenantId, _userId, Arg.Any<CancellationToken>())
            .Returns(evaluatedFlags);

        IActionResult result = await _controller.Evaluate(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        Dictionary<string, object> response = ok.Value.Should().BeOfType<Dictionary<string, object>>().Subject;
        response.Should().HaveCount(2);
        response["dark-mode"].Should().Be(true);
        response["max-items"].Should().Be(100);
    }

    [Fact]
    public async Task Evaluate_PassesTenantIdAndUserIdToService()
    {
        _featureFlagService.GetAllFlagsAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, object>());

        await _controller.Evaluate(CancellationToken.None);

        await _featureFlagService.Received(1).GetAllFlagsAsync(
            _tenantId,
            _userId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Evaluate_WithNoUserClaim_PassesNullUserId()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity())
            }
        };
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);
        _featureFlagService.GetAllFlagsAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, object>());

        await _controller.Evaluate(CancellationToken.None);

        await _featureFlagService.Received(1).GetAllFlagsAsync(
            _tenantId,
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Evaluate_WithSubClaim_UsesSubClaimAsUserId()
    {
        Guid subUserId = Guid.NewGuid();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim("sub", subUserId.ToString())
                }, "TestAuth"))
            }
        };
        _currentUserService.GetCurrentUserId().Returns(subUserId);
        _featureFlagService.GetAllFlagsAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, object>());

        await _controller.Evaluate(CancellationToken.None);

        await _featureFlagService.Received(1).GetAllFlagsAsync(
            _tenantId,
            subUserId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Evaluate_WithNonGuidUserClaim_PassesNullUserId()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "not-a-guid")
                }, "TestAuth"))
            }
        };
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);
        _featureFlagService.GetAllFlagsAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, object>());

        await _controller.Evaluate(CancellationToken.None);

        await _featureFlagService.Received(1).GetAllFlagsAsync(
            _tenantId,
            null,
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Helpers

    private FeatureFlagDto CreateFlagDto(string key, string name, Guid? id = null)
    {
        return new FeatureFlagDto(
            id ?? Guid.NewGuid(),
            key,
            name,
            null,
            FlagType.Boolean,
            true,
            null,
            null,
            null,
            DateTime.UtcNow,
            null);
    }

    private FeatureFlagOverrideDto CreateOverrideDto(Guid flagId, Guid? id = null)
    {
        return new FeatureFlagOverrideDto(
            id ?? Guid.NewGuid(),
            flagId,
            null,
            null,
            true,
            null,
            null,
            DateTime.UtcNow);
    }

    #endregion
}
