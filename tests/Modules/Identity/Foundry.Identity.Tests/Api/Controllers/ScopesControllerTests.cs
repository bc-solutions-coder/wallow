using Foundry.Identity.Api.Controllers;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Foundry.Identity.Tests.Api.Controllers;

public class ScopesControllerTests
{
    private readonly IApiScopeRepository _apiScopeRepository;
    private readonly ScopesController _controller;

    public ScopesControllerTests()
    {
        _apiScopeRepository = Substitute.For<IApiScopeRepository>();
        _controller = new ScopesController(_apiScopeRepository);
    }

    #region List

    [Fact]
    public async Task List_WithNoCategory_ReturnsAllScopes()
    {
        List<ApiScope> scopes =
        [
            ApiScope.Create("billing:read", "Read Billing", "Billing", "Read billing data"),
            ApiScope.Create("billing:write", "Write Billing", "Billing", "Write billing data", true)
        ];
        _apiScopeRepository.GetAllAsync(null, Arg.Any<CancellationToken>())
            .Returns(scopes);

        ActionResult<IReadOnlyList<ApiScopeDto>> result = await _controller.List(null, CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        List<ApiScopeDto> response = ok.Value.Should().BeOfType<List<ApiScopeDto>>().Subject;
        response.Should().HaveCount(2);
        response[0].Code.Should().Be("billing:read");
        response[0].DisplayName.Should().Be("Read Billing");
        response[0].Category.Should().Be("Billing");
        response[0].Description.Should().Be("Read billing data");
        response[0].IsDefault.Should().BeFalse();
        response[1].IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task List_WithCategory_PassesCategoryToRepository()
    {
        _apiScopeRepository.GetAllAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await _controller.List("Billing", CancellationToken.None);

        await _apiScopeRepository.Received(1).GetAllAsync("Billing", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task List_WhenEmpty_ReturnsEmptyList()
    {
        _apiScopeRepository.GetAllAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns([]);

        ActionResult<IReadOnlyList<ApiScopeDto>> result = await _controller.List(null, CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        List<ApiScopeDto> response = ok.Value.Should().BeOfType<List<ApiScopeDto>>().Subject;
        response.Should().BeEmpty();
    }

    #endregion
}
