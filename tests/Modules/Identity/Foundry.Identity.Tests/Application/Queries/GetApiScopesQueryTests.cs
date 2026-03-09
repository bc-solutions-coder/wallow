using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Application.Queries.GetApiScopes;
using Foundry.Identity.Domain.Entities;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Identity.Tests.Application.Queries;

public class GetApiScopesQueryTests
{
    private readonly IApiScopeRepository _apiScopeRepository = Substitute.For<IApiScopeRepository>();

    [Fact]
    public async Task Handle_WithNoCategory_ReturnsAllScopes()
    {
        // Arrange
        List<ApiScope> scopes =
        [
            ApiScope.Create("invoices.read", "Read Invoices", "Billing", "Read invoice data"),
            ApiScope.Create("payments.write", "Write Payments", "Billing", "Create payments"),
            ApiScope.Create("users.read", "Read Users", "Identity", "Read user data")
        ];

        _apiScopeRepository.GetAllAsync(null, Arg.Any<CancellationToken>())
            .Returns(scopes);

        GetApiScopesQuery query = new GetApiScopesQuery();
        GetApiScopesHandler handler = new(_apiScopeRepository);

        // Act
        Result<IReadOnlyList<ApiScopeDto>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
        result.Value.Should().Contain(s => s.Code == "invoices.read");
        result.Value.Should().Contain(s => s.Code == "payments.write");
        result.Value.Should().Contain(s => s.Code == "users.read");

        await _apiScopeRepository.Received(1).GetAllAsync(null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithCategory_PassesCategoryToRepository()
    {
        // Arrange
        List<ApiScope> billingScopes =
        [
            ApiScope.Create("invoices.read", "Read Invoices", "Billing"),
            ApiScope.Create("payments.write", "Write Payments", "Billing")
        ];

        _apiScopeRepository.GetAllAsync("Billing", Arg.Any<CancellationToken>())
            .Returns(billingScopes);

        GetApiScopesQuery query = new("Billing");
        GetApiScopesHandler handler = new(_apiScopeRepository);

        // Act
        Result<IReadOnlyList<ApiScopeDto>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().OnlyContain(s => s.Category == "Billing");

        await _apiScopeRepository.Received(1).GetAllAsync("Billing", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MapsEntityToDto()
    {
        // Arrange
        ApiScope scope = ApiScope.Create(
            "test.read",
            "Test Read",
            "Testing",
            "Test description",
            isDefault: true);

        _apiScopeRepository.GetAllAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<ApiScope> { scope });

        GetApiScopesQuery query = new GetApiScopesQuery();
        GetApiScopesHandler handler = new(_apiScopeRepository);

        // Act
        Result<IReadOnlyList<ApiScopeDto>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        ApiScopeDto dto = result.Value.Single();
        dto.Id.Should().Be(scope.Id);
        dto.Code.Should().Be("test.read");
        dto.DisplayName.Should().Be("Test Read");
        dto.Category.Should().Be("Testing");
        dto.Description.Should().Be("Test description");
        dto.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithEmptyResult_ReturnsEmptyList()
    {
        // Arrange
        _apiScopeRepository.GetAllAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns([]);

        GetApiScopesQuery query = new GetApiScopesQuery();
        GetApiScopesHandler handler = new(_apiScopeRepository);

        // Act
        Result<IReadOnlyList<ApiScopeDto>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_PropagatesCancellationToken()
    {
        // Arrange
        using CancellationTokenSource cts = new CancellationTokenSource();
        _apiScopeRepository.GetAllAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns([]);

        GetApiScopesQuery query = new GetApiScopesQuery();
        GetApiScopesHandler handler = new(_apiScopeRepository);

        // Act
        await handler.Handle(query, cts.Token);

        // Assert
        await _apiScopeRepository.Received(1).GetAllAsync(Arg.Any<string?>(), cts.Token);
    }
}
