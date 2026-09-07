using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;

namespace Wallow.TelemetryGateway.Tests;

public sealed class SourceMapTests
{
    private static readonly string[] _sources = ["https://private.example/PRIVATE-PATH/src/operation.ts?secret=PRIVATE-QUERY"];
    private static readonly string[] _names = ["performOperation"];
    private static readonly string[] _content = ["PRIVATE-SOURCE-CONTENT"];
    private static readonly string[] _invalidSources = ["a.ts"];
    [Fact]
    public async Task PrivateMaps_AreAuthorizedBoundedSanitizedAndPersisted()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string database = Path.Combine(directory, "registry.db");
        Guid id = Guid.NewGuid();
        string path = $"/control/v1/source-maps/{id}/1.0.0/app.js";
        await using (WebApplication app = await GatewayControl.CreateAsync(database, "management", TimeProvider.System))
        {
            await app.StartAsync();
            using HttpClient client = new() { BaseAddress = new Uri(app.Urls.Single()) };
            using HttpResponseMessage unauthorized = await client.GetAsync(path + "?line=1&column=1");
            Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "management");
            using HttpResponseMessage registration = await client.PutAsJsonAsync($"/control/v1/registrations/{id}", new DesiredRegistration(
                1, "client-a", "application-a", "browser-a", "server-a", ["test"], RegistrationState.Disabled, [], null));
            registration.EnsureSuccessStatusCode();
            using HttpResponseMessage upload = await client.PutAsJsonAsync(path, new
            {
                version = 3,
                sources = _sources,
                names = _names,
                mappings = "AAAAA,KACEA",
                sourcesContent = _content,
            });
            Assert.Equal(HttpStatusCode.NoContent, upload.StatusCode);
            JsonElement result = await client.GetFromJsonAsync<JsonElement>(path + "?line=1&column=6");
            Assert.Equal("operation.ts", result.GetProperty("file").GetString());
            Assert.Equal(2, result.GetProperty("line").GetInt32());
            Assert.Equal(3, result.GetProperty("column").GetInt32());
            Assert.Equal("performOperation", result.GetProperty("function").GetString());
            using HttpResponseMessage malformed = await client.PutAsJsonAsync(path, new { version = 3, sources = _invalidSources, names = Array.Empty<string>(), mappings = "!!!!!" });
            Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
            using HttpResponseMessage nullMap = await client.PutAsJsonAsync(path, new { version = 3, sources = new string?[] { null }, names = _names, mappings = "AAAA" });
            Assert.Equal(HttpStatusCode.BadRequest, nullMap.StatusCode);
            using HttpResponseMessage other = await client.GetAsync($"/control/v1/source-maps/{Guid.NewGuid()}/1.0.0/app.js?line=1&column=1");
            Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
            using HttpResponseMessage absent = await client.GetAsync(path + "?line=2&column=1");
            Assert.Equal(HttpStatusCode.NotFound, absent.StatusCode);
        }
        foreach (string artifact in Directory.EnumerateFiles(Path.Combine(directory, "source-maps")))
        {
            Assert.DoesNotContain("PRIVATE-", await File.ReadAllTextAsync(artifact), StringComparison.Ordinal);
        }
        await using WebApplication restarted = await GatewayControl.CreateAsync(database, "management", TimeProvider.System);
        await restarted.StartAsync();
        using HttpClient after = new() { BaseAddress = new Uri(restarted.Urls.Single()) };
        after.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "management");
        using HttpResponseMessage stored = await after.GetAsync(path + "?line=1&column=1");
        Assert.Equal(HttpStatusCode.OK, stored.StatusCode);
    }
}
