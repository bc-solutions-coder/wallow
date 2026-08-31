using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Wallow.Tests.Common.Factories;

namespace Wallow.Api.Tests.Integration;

/// <summary>
/// Pins the #150 rate-limiter requirements: the limiter runs in the Testing environment, it
/// runs AFTER authentication (so the registration policy partitions per authenticated user,
/// not per IP), and organization create sits under the registration policy. The registration
/// window is tightened to two permits on a derived host; the shared fixture host keeps the
/// generous appsettings.Testing.json limits.
/// </summary>
[Collection(nameof(ApiIntegrationTestCollection))]
[Trait("Category", "Integration")]
public sealed class RateLimitPartitioningTests : IDisposable
{
    private const int TightPermitLimit = 2;
    private const string OrganizationsPath = "/v1/identity/organizations";

    private readonly WebApplicationFactory<Program> _tightened;

    public RateLimitPartitioningTests(WallowApiFactory factory)
    {
        _tightened = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("RateLimiting:Registration:PermitLimit", "2"));
    }

    public void Dispose()
    {
        _tightened.Dispose();
    }

    private HttpClient CreateUserClient(string userId)
    {
        HttpClient client = _tightened.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer test-token");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", userId);
        return client;
    }

    // An empty body fails [ApiController] model validation (Name is a non-nullable string),
    // so every attempt counts against the window without actually creating organizations.
    private static Task<HttpResponseMessage> PostOrganizationCreateAsync(HttpClient client)
    {
        return client.PostAsJsonAsync(OrganizationsPath, new { });
    }

    [Fact]
    public async Task Two_Users_On_The_Same_Address_Are_Limited_Independently()
    {
        string userA = Guid.NewGuid().ToString();
        string userB = Guid.NewGuid().ToString();

        using HttpClient clientA = CreateUserClient(userA);
        for (int attempt = 0; attempt < TightPermitLimit; attempt++)
        {
            using HttpResponseMessage allowed = await PostOrganizationCreateAsync(clientA);
            allowed.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests,
                "attempt {0} sits inside the {1}-permit window", attempt + 1, TightPermitLimit);
        }

        using HttpResponseMessage limited = await PostOrganizationCreateAsync(clientA);
        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            "the attempt beyond the window must be rejected — and a 429 here also proves the "
            + "limiter is enabled in the Testing environment");
        limited.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        limited.Headers.Contains("Retry-After").Should().BeTrue();

        // Both clients ride the same TestServer connection, so the transport address is
        // identical: only a per-user partition key can put user B in a fresh window.
        using HttpClient clientB = CreateUserClient(userB);
        using HttpResponseMessage unaffected = await PostOrganizationCreateAsync(clientB);
        unaffected.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests,
            "a second user on the same address must own an independent window");
    }
}
