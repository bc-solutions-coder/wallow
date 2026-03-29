using System.Net;
using Microsoft.AspNetCore.Http;
using RichardSzalay.MockHttp;
using Wallow.Web.Models;
using Wallow.Web.Services;

namespace Wallow.Web.Tests.Services;

public sealed class OrganizationApiServiceTests : IDisposable
{
    private const string BaseUrl = "http://localhost:5000";
    private const string OrganizationsPath = "api/v1/identity/organizations";
    private const string ClientsPath = "api/v1/identity/clients";
    private const string TestToken = "test-bearer-token";

    private readonly MockHttpMessageHandler _mockHttp = new();
    private readonly OrganizationApiService _sut;

    public OrganizationApiServiceTests()
    {
        HttpClient httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri(BaseUrl);

        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("WallowApi").Returns(httpClient);

        IHttpContextAccessor httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        TokenProvider tokenProvider = new(httpContextAccessor) { AccessToken = TestToken };

        _sut = new OrganizationApiService(factory, tokenProvider);
    }

    public void Dispose()
    {
        _mockHttp.Dispose();
    }

    // --- GetOrganizationsAsync ---

    [Fact]
    public async Task GetOrganizationsAsync_SuccessfulResponse_ReturnsOrganizationList()
    {
        _mockHttp.When(HttpMethod.Get, $"{BaseUrl}/{OrganizationsPath}")
            .Respond("application/json", """
                [{"id":"00000000-0000-0000-0000-000000000001","name":"Org One","domain":"org1.com","memberCount":5}]
                """);

        List<OrganizationModel> result = await _sut.GetOrganizationsAsync();

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Org One");
        result[0].Domain.Should().Be("org1.com");
        result[0].MemberCount.Should().Be(5);
    }

    [Fact]
    public async Task GetOrganizationsAsync_EmptyResponse_ReturnsEmptyList()
    {
        _mockHttp.When(HttpMethod.Get, $"{BaseUrl}/{OrganizationsPath}")
            .Respond("application/json", "[]");

        List<OrganizationModel> result = await _sut.GetOrganizationsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOrganizationsAsync_ErrorResponse_ReturnsEmptyList()
    {
        _mockHttp.When(HttpMethod.Get, $"{BaseUrl}/{OrganizationsPath}")
            .Respond(HttpStatusCode.InternalServerError);

        List<OrganizationModel> result = await _sut.GetOrganizationsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOrganizationsAsync_NullDeserialization_ReturnsEmptyList()
    {
        _mockHttp.When(HttpMethod.Get, $"{BaseUrl}/{OrganizationsPath}")
            .Respond("application/json", "null");

        List<OrganizationModel> result = await _sut.GetOrganizationsAsync();

        result.Should().BeEmpty();
    }

    // --- GetOrganizationAsync ---

    [Fact]
    public async Task GetOrganizationAsync_ExistingOrg_ReturnsOrganization()
    {
        Guid orgId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        _mockHttp.When(HttpMethod.Get, $"{BaseUrl}/{OrganizationsPath}/{orgId}")
            .Respond("application/json", """
                {"id":"00000000-0000-0000-0000-000000000001","name":"Org One","domain":"org1.com","memberCount":3}
                """);

        OrganizationModel? result = await _sut.GetOrganizationAsync(orgId);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Org One");
        result.Id.Should().Be(orgId);
    }

    [Fact]
    public async Task GetOrganizationAsync_NotFound_ReturnsNull()
    {
        Guid orgId = Guid.Parse("00000000-0000-0000-0000-000000000099");

        _mockHttp.When(HttpMethod.Get, $"{BaseUrl}/{OrganizationsPath}/{orgId}")
            .Respond(HttpStatusCode.NotFound);

        OrganizationModel? result = await _sut.GetOrganizationAsync(orgId);

        result.Should().BeNull();
    }

    // --- GetMembersAsync ---

    [Fact]
    public async Task GetMembersAsync_SuccessfulResponse_ReturnsMemberList()
    {
        Guid orgId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        _mockHttp.When(HttpMethod.Get, $"{BaseUrl}/{OrganizationsPath}/{orgId}/members")
            .Respond("application/json", """
                [{"id":"00000000-0000-0000-0000-000000000002","email":"user@test.com","firstName":"John","lastName":"Doe","enabled":true,"roles":["Admin"]}]
                """);

        List<OrganizationMemberModel> result = await _sut.GetMembersAsync(orgId);

        result.Should().HaveCount(1);
        result[0].Email.Should().Be("user@test.com");
        result[0].FirstName.Should().Be("John");
        result[0].Roles.Should().Contain("Admin");
    }

    [Fact]
    public async Task GetMembersAsync_EmptyResponse_ReturnsEmptyList()
    {
        Guid orgId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        _mockHttp.When(HttpMethod.Get, $"{BaseUrl}/{OrganizationsPath}/{orgId}/members")
            .Respond("application/json", "[]");

        List<OrganizationMemberModel> result = await _sut.GetMembersAsync(orgId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMembersAsync_ErrorResponse_ReturnsEmptyList()
    {
        Guid orgId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        _mockHttp.When(HttpMethod.Get, $"{BaseUrl}/{OrganizationsPath}/{orgId}/members")
            .Respond(HttpStatusCode.InternalServerError);

        List<OrganizationMemberModel> result = await _sut.GetMembersAsync(orgId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMembersAsync_NullDeserialization_ReturnsEmptyList()
    {
        Guid orgId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        _mockHttp.When(HttpMethod.Get, $"{BaseUrl}/{OrganizationsPath}/{orgId}/members")
            .Respond("application/json", "null");

        List<OrganizationMemberModel> result = await _sut.GetMembersAsync(orgId);

        result.Should().BeEmpty();
    }

    // --- GetClientsByTenantAsync ---

    [Fact]
    public async Task GetClientsByTenantAsync_SuccessfulResponse_ReturnsClientList()
    {
        Guid tenantId = Guid.Parse("00000000-0000-0000-0000-000000000010");

        _mockHttp.When(HttpMethod.Get, $"{BaseUrl}/{ClientsPath}/by-tenant/{tenantId}")
            .Respond("application/json", """
                [{"id":"c1","name":"Client One","clientId":"client-1","clientSecret":null,"redirectUris":["http://localhost"],"postLogoutRedirectUris":[]}]
                """);

        List<ClientModel> result = await _sut.GetClientsByTenantAsync(tenantId);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Client One");
        result[0].ClientId.Should().Be("client-1");
    }

    [Fact]
    public async Task GetClientsByTenantAsync_EmptyResponse_ReturnsEmptyList()
    {
        Guid tenantId = Guid.Parse("00000000-0000-0000-0000-000000000010");

        _mockHttp.When(HttpMethod.Get, $"{BaseUrl}/{ClientsPath}/by-tenant/{tenantId}")
            .Respond("application/json", "[]");

        List<ClientModel> result = await _sut.GetClientsByTenantAsync(tenantId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetClientsByTenantAsync_ErrorResponse_ReturnsEmptyList()
    {
        Guid tenantId = Guid.Parse("00000000-0000-0000-0000-000000000010");

        _mockHttp.When(HttpMethod.Get, $"{BaseUrl}/{ClientsPath}/by-tenant/{tenantId}")
            .Respond(HttpStatusCode.InternalServerError);

        List<ClientModel> result = await _sut.GetClientsByTenantAsync(tenantId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetClientsByTenantAsync_NullDeserialization_ReturnsEmptyList()
    {
        Guid tenantId = Guid.Parse("00000000-0000-0000-0000-000000000010");

        _mockHttp.When(HttpMethod.Get, $"{BaseUrl}/{ClientsPath}/by-tenant/{tenantId}")
            .Respond("application/json", "null");

        List<ClientModel> result = await _sut.GetClientsByTenantAsync(tenantId);

        result.Should().BeEmpty();
    }

}
