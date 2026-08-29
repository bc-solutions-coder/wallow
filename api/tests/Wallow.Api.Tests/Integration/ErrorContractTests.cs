using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Wallow.Tests.Common.Factories;
using Wallow.Tests.Common.Helpers;

namespace Wallow.Api.Tests.Integration;

/// <summary>
/// Pins the browser-safe error contract: every 4xx the API hands an unauthenticated or lost
/// browser carries an application/problem+json body. The API sends
/// X-Content-Type-Options: nosniff, so an empty error response with no Content-Type makes a
/// browser navigation download a zero-byte file instead of showing anything.
/// </summary>
[Collection(nameof(ApiIntegrationTestCollection))]
[Trait("Category", "Integration")]
public sealed class ErrorContractTests(WallowApiFactory factory) : IDisposable
{
    private readonly HttpClient _client = factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
    }

    [Fact]
    public async Task Unmatched_Path_Returns_404_Problem_Not_A_Challenge()
    {
        // Browsers send Accept: text/html,...,*/* on navigations; */* admits JSON.
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/identity/setup");
        request.Headers.TryAddWithoutValidation(
            "Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

        HttpResponseMessage response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AssertProblemJsonBodyAsync(response, expectedStatus: 404);
    }

    [Fact]
    public async Task Unauthenticated_Protected_Endpoint_Returns_401_Problem()
    {
        HttpResponseMessage response = await _client.GetAsync("/v1/identity/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertProblemJsonBodyAsync(response, expectedStatus: 401);
    }

    [Fact]
    public async Task Unauthenticated_Browser_Navigation_Gets_A_Body_Not_A_Download()
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/v1/identity/users/me");
        request.Headers.TryAddWithoutValidation(
            "Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

        HttpResponseMessage response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertProblemJsonBodyAsync(response, expectedStatus: 401);
    }

    [Fact]
    public async Task Wrong_Method_Returns_405_With_A_Body()
    {
        // Authenticated: the framework's synthesized 405 endpoint carries no AllowAnonymous, so
        // an anonymous wrong-method request is challenged to 401 before the 405 is reached.
        using HttpRequestMessage request = new(HttpMethod.Delete, "/v1/identity/setup/status");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer test-token");
        request.Headers.TryAddWithoutValidation("X-Test-User-Id", TestConstants.AdminUserId.ToString());
        request.Headers.TryAddWithoutValidation("X-Test-Roles", "admin");

        HttpResponseMessage response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        await AssertProblemJsonBodyAsync(response, expectedStatus: 405);
    }

    private static async Task AssertProblemJsonBodyAsync(HttpResponseMessage response, int expectedStatus)
    {
        response.Content.Headers.ContentType.Should().NotBeNull();
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem.Status.Should().Be(expectedStatus);
        problem.Title.Should().NotBeNullOrEmpty();
    }
}
