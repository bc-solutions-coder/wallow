using Microsoft.AspNetCore.Mvc;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Contracts.Responses;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;

namespace Wallow.Identity.Tests.Api.Controllers;

public class InitialAccessTokensControllerTests
{
    private readonly IInitialAccessTokenRepository _repository;
    private readonly InitialAccessTokensController _controller;

    public InitialAccessTokensControllerTests()
    {
        _repository = Substitute.For<IInitialAccessTokenRepository>();
        _controller = new InitialAccessTokensController(_repository);
    }

    [Fact]
    public async Task Create_ReturnsCreated_WithRawTokenInBody()
    {
        CreateInitialAccessTokenRequest request = new("Test Token", DateTimeOffset.UtcNow.AddDays(30));

        ActionResult<InitialAccessTokenCreatedResponse> result = await _controller.Create(request, CancellationToken.None);

        ObjectResult objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(201);
        InitialAccessTokenCreatedResponse response = objectResult.Value.Should().BeOfType<InitialAccessTokenCreatedResponse>().Subject;
        response.Token.Should().NotBeNullOrEmpty();
        await _repository.Received(1).AddAsync(Arg.Any<InitialAccessToken>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_HashedToken_IsNotEqualToRawToken()
    {
        InitialAccessToken? capturedToken = null;
        _repository.AddAsync(Arg.Do<InitialAccessToken>(t => capturedToken = t), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        CreateInitialAccessTokenRequest request = new("Test Token");

        ActionResult<InitialAccessTokenCreatedResponse> result = await _controller.Create(request, CancellationToken.None);

        ObjectResult objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        InitialAccessTokenCreatedResponse response = objectResult.Value.Should().BeOfType<InitialAccessTokenCreatedResponse>().Subject;
        capturedToken.Should().NotBeNull();
        capturedToken!.TokenHash.Should().NotBe(response.Token);
    }

    [Fact]
    public async Task GetAll_ReturnsOk_WithTokenList_NoRawValues()
    {
        InitialAccessToken token = InitialAccessToken.Create("somehash", "My Token", DateTimeOffset.UtcNow.AddDays(7));
        _repository.ListAsync(Arg.Any<CancellationToken>())
            .Returns(new List<InitialAccessToken> { token });

        ActionResult<IReadOnlyList<InitialAccessTokenResponse>> result = await _controller.GetAll(CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        List<InitialAccessTokenResponse> responses = ok.Value.Should().BeOfType<List<InitialAccessTokenResponse>>().Subject;
        responses.Count.Should().Be(1);
        responses[0].DisplayName.Should().Be("My Token");

        // InitialAccessTokenResponse has no Token/RawToken property — only Id, DisplayName, ExpiresAt, IsRevoked
        Type responseType = typeof(InitialAccessTokenResponse);
        responseType.GetProperty("Token").Should().BeNull();
        responseType.GetProperty("RawToken").Should().BeNull();
    }

    [Fact]
    public async Task Delete_WhenExists_ReturnsNoContent()
    {
        Guid id = Guid.NewGuid();
        InitialAccessToken token = InitialAccessToken.Create("hash", "To Delete", null);
        _repository.GetByIdAsync(Arg.Any<InitialAccessTokenId>(), Arg.Any<CancellationToken>())
            .Returns(token);

        ActionResult result = await _controller.Delete(id.ToString(), CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_WhenNotFound_ReturnsNotFound()
    {
        Guid id = Guid.NewGuid();
        _repository.GetByIdAsync(Arg.Any<InitialAccessTokenId>(), Arg.Any<CancellationToken>())
            .Returns((InitialAccessToken?)null);

        ActionResult result = await _controller.Delete(id.ToString(), CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }
}
