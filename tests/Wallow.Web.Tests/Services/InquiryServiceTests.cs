using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RichardSzalay.MockHttp;
using Wallow.Web.Models;
using Wallow.Web.Services;

namespace Wallow.Web.Tests.Services;

public sealed class InquiryServiceTests : IDisposable
{
    private const string BaseUrl = "http://localhost:5000";
    private const string BasePath = "api/v1/inquiries";
    private const string TestToken = "test-bearer-token";

    private readonly MockHttpMessageHandler _mockHttp = new();
    private readonly InquiryService _sut;

    public InquiryServiceTests()
    {
        HttpClient httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri(BaseUrl);

        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("WallowApi").Returns(httpClient);

        IHttpContextAccessor httpContextAccessor = CreateHttpContextAccessor();

        _sut = new InquiryService(factory, httpContextAccessor);
    }

    public void Dispose()
    {
        _mockHttp.Dispose();
    }

    // --- SubmitInquiryAsync ---

    [Fact]
    public async Task SubmitInquiryAsync_SuccessfulResponse_ReturnsTrue()
    {
        _mockHttp.When(HttpMethod.Post, $"{BaseUrl}/{BasePath}")
            .Respond(HttpStatusCode.OK);

        InquiryModel model = new(
            "John Doe",
            "john@test.com",
            "555-0100",
            "Acme Corp",
            "Web Development",
            "$10k-$25k",
            "1-3 months",
            "I need a website.");

        bool result = await _sut.SubmitInquiryAsync(model);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task SubmitInquiryAsync_CreatedResponse_ReturnsTrue()
    {
        _mockHttp.When(HttpMethod.Post, $"{BaseUrl}/{BasePath}")
            .Respond(HttpStatusCode.Created);

        InquiryModel model = new(
            "Jane Doe",
            "jane@test.com",
            "555-0101",
            null,
            "Mobile App",
            "$25k-$50k",
            "3-6 months",
            "I need an app.");

        bool result = await _sut.SubmitInquiryAsync(model);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task SubmitInquiryAsync_ErrorResponse_ReturnsFalse()
    {
        _mockHttp.When(HttpMethod.Post, $"{BaseUrl}/{BasePath}")
            .Respond(HttpStatusCode.InternalServerError);

        InquiryModel model = new(
            "John Doe",
            "john@test.com",
            "555-0100",
            null,
            "Web Development",
            "$10k-$25k",
            "1-3 months",
            "I need a website.");

        bool result = await _sut.SubmitInquiryAsync(model);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SubmitInquiryAsync_BadRequest_ReturnsFalse()
    {
        _mockHttp.When(HttpMethod.Post, $"{BaseUrl}/{BasePath}")
            .Respond(HttpStatusCode.BadRequest);

        InquiryModel model = new(
            "John Doe",
            "invalid",
            "555-0100",
            null,
            "Web Development",
            "$10k-$25k",
            "1-3 months",
            "Message.");

        bool result = await _sut.SubmitInquiryAsync(model);

        result.Should().BeFalse();
    }

    // --- Helper ---

    private static IHttpContextAccessor CreateHttpContextAccessor()
    {
        AuthenticationProperties authProperties = new();
        authProperties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = TestToken }]);

        AuthenticationTicket ticket = new(
            new ClaimsPrincipal(new ClaimsIdentity("test")),
            authProperties,
            "test");

        IAuthenticationService authService = Substitute.For<IAuthenticationService>();
        authService.AuthenticateAsync(Arg.Any<HttpContext>(), Arg.Any<string?>())
            .Returns(AuthenticateResult.Success(ticket));

        ServiceCollection services = new();
        services.AddSingleton(authService);
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        DefaultHttpContext httpContext = new()
        {
            RequestServices = serviceProvider
        };

        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);
        return accessor;
    }
}
