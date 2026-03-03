using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Foundry.Identity.IntegrationTests.Scim;

/// <summary>
/// Mock Keycloak Admin API server for SCIM integration testing.
/// Uses WireMock.Net to simulate Keycloak Admin REST API endpoints.
/// </summary>
public class MockScimIdpFixture : IAsyncLifetime
{
    private WireMockServer? _server;
    private readonly ConcurrentDictionary<string, KeycloakUser> _users = new();

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string BaseUrl => _server?.Url ?? throw new InvalidOperationException("Server not started");

    public Task InitializeAsync()
    {
        _server = WireMockServer.Start();
        SetupDefaultEndpoints();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _server?.Stop();
        _server?.Dispose();
        return Task.CompletedTask;
    }

    public void Reset()
    {
        _users.Clear();
        _server?.Reset();
        SetupDefaultEndpoints();
    }

    private void SetupDefaultEndpoints()
    {
        SetupCreateUser();
        SetupGetUser();
        SetupUpdateUser();
        SetupDeleteUser();
        SetupListUsers();
        SetupUserCount();
        SetupOrganizationMembership();
        SetupRoles();
    }

    private void SetupCreateUser()
    {
        // POST /admin/realms/foundry/users - Create user
        _server!
            .Given(Request.Create()
                .WithPath("/admin/realms/foundry/users")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithCallback(request =>
                {
                    string? body = request.Body;
                    KeycloakUser? user;
                    try
                    {
                        user = body is not null ? JsonSerializer.Deserialize<KeycloakUser>(body, _jsonOptions) : null;
                    }
                    catch
                    {
                        return new WireMock.ResponseMessage { StatusCode = 400 };
                    }

                    if (user == null)
                    {
                        return new WireMock.ResponseMessage { StatusCode = 400 };
                    }

                    // Detect duplicate username/email (Keycloak returns 409)
                    if (_users.Values.Any(u =>
                        (!string.IsNullOrEmpty(u.Username) && u.Username == user.Username) ||
                        (!string.IsNullOrEmpty(u.Email) && u.Email == user.Email)))
                    {
                        return new WireMock.ResponseMessage { StatusCode = 409 };
                    }

                    user.Id = Guid.NewGuid().ToString();
                    _users[user.Id] = user;

                    return new WireMock.ResponseMessage
                    {
                        StatusCode = 201,
                        Headers = new Dictionary<string, WireMock.Types.WireMockList<string>>
                        {
                            { "Location", new WireMock.Types.WireMockList<string>($"/admin/realms/foundry/users/{user.Id}") }
                        }
                    };
                }));
    }

    private void SetupGetUser()
    {
        // GET /admin/realms/foundry/users/{id} - Get user by ID
        _server!
            .Given(Request.Create()
                .WithPath("/admin/realms/foundry/users/*")
                .UsingGet())
            .AtPriority(1)
            .RespondWith(Response.Create()
                .WithCallback(request =>
                {
                    string[] pathParts = request.Path.Split('/');
                    string userId = pathParts.Last();

                    // Skip count and query endpoints
                    if (userId == "count" || request.Path.Contains('?'))
                    {
                        return new WireMock.ResponseMessage { StatusCode = 404 };
                    }

                    if (_users.TryGetValue(userId, out KeycloakUser? user))
                    {
                        string json = JsonSerializer.Serialize(user, _jsonOptions);
                        return new WireMock.ResponseMessage
                        {
                            StatusCode = 200,
                            Headers = new Dictionary<string, WireMock.Types.WireMockList<string>>
                            {
                                { "Content-Type", new WireMock.Types.WireMockList<string>("application/json") }
                            },
                            BodyData = new WireMock.Util.BodyData
                            {
                                BodyAsString = json,
                                DetectedBodyType = WireMock.Types.BodyType.String,
                                Encoding = System.Text.Encoding.UTF8
                            }
                        };
                    }

                    return new WireMock.ResponseMessage { StatusCode = 404 };
                }));
    }

    private void SetupUpdateUser()
    {
        // PUT /admin/realms/foundry/users/{id} - Update user
        _server!
            .Given(Request.Create()
                .WithPath("/admin/realms/foundry/users/*")
                .UsingPut())
            .RespondWith(Response.Create()
                .WithCallback(request =>
                {
                    string userId = request.Path.Split('/').Last();

                    if (!_users.TryGetValue(userId, out KeycloakUser? existingUser))
                    {
                        return new WireMock.ResponseMessage { StatusCode = 404 };
                    }

                    KeycloakUser? updatedUser;
                    try
                    {
                        updatedUser = request.Body is not null ? JsonSerializer.Deserialize<KeycloakUser>(request.Body, _jsonOptions) : null;
                    }
                    catch
                    {
                        return new WireMock.ResponseMessage { StatusCode = 400 };
                    }

                    if (updatedUser != null)
                    {
                        // Merge the update into the existing user
                        existingUser.Username = updatedUser.Username ?? existingUser.Username;
                        existingUser.Email = updatedUser.Email ?? existingUser.Email;
                        existingUser.FirstName = updatedUser.FirstName ?? existingUser.FirstName;
                        existingUser.LastName = updatedUser.LastName ?? existingUser.LastName;
                        if (updatedUser.Enabled.HasValue)
                        {
                            existingUser.Enabled = updatedUser.Enabled;
                        }
                        if (updatedUser.Attributes != null)
                        {
                            existingUser.Attributes = updatedUser.Attributes;
                        }
                    }

                    return new WireMock.ResponseMessage { StatusCode = 204 };
                }));
    }

    private void SetupDeleteUser()
    {
        // DELETE /admin/realms/foundry/users/{id} - Delete user
        _server!
            .Given(Request.Create()
                .WithPath("/admin/realms/foundry/users/*")
                .UsingDelete())
            .RespondWith(Response.Create()
                .WithCallback(request =>
                {
                    string userId = request.Path.Split('/').Last();

                    if (_users.TryRemove(userId, out _))
                    {
                        return new WireMock.ResponseMessage { StatusCode = 204 };
                    }

                    return new WireMock.ResponseMessage { StatusCode = 404 };
                }));
    }

    private void SetupListUsers()
    {
        // GET /admin/realms/foundry/users?first=X&max=Y&username=... - List users
        _server!
            .Given(Request.Create()
                .WithPath("/admin/realms/foundry/users")
                .UsingGet())
            .AtPriority(0)
            .RespondWith(Response.Create()
                .WithCallback(request =>
                {
                    List<KeycloakUser> users = _users.Values.ToList();

                    // Apply query filters
                    IDictionary<string, WireMock.Types.WireMockList<string>>? queryParams = request.Query;
                    if (queryParams != null)
                    {
                        if (queryParams.TryGetValue("username", out WireMock.Types.WireMockList<string>? usernameParam) && usernameParam.Count > 0)
                        {
                            string username = usernameParam[0];
                            users = users.Where(u => u.Username != null &&
                                u.Username.Contains(username, StringComparison.OrdinalIgnoreCase)).ToList();
                        }

                        if (queryParams.TryGetValue("email", out WireMock.Types.WireMockList<string>? emailParam) && emailParam.Count > 0)
                        {
                            string email = emailParam[0];
                            users = users.Where(u => u.Email != null &&
                                u.Email.Contains(email, StringComparison.OrdinalIgnoreCase)).ToList();
                        }

                        // Apply pagination
                        int first = 0;
                        int max = 100;
                        if (queryParams.TryGetValue("first", out WireMock.Types.WireMockList<string>? firstParam) && firstParam.Count > 0)
                        {
                            _ = int.TryParse(firstParam[0], out first);
                        }
                        if (queryParams.TryGetValue("max", out WireMock.Types.WireMockList<string>? maxParam) && maxParam.Count > 0)
                        {
                            _ = int.TryParse(maxParam[0], out max);
                        }

                        users = users.Skip(first).Take(max).ToList();
                    }

                    string json = JsonSerializer.Serialize(users, _jsonOptions);
                    return new WireMock.ResponseMessage
                    {
                        StatusCode = 200,
                        Headers = new Dictionary<string, WireMock.Types.WireMockList<string>>
                        {
                            { "Content-Type", new WireMock.Types.WireMockList<string>("application/json") }
                        },
                        BodyData = new WireMock.Util.BodyData
                        {
                            BodyAsString = json,
                            DetectedBodyType = WireMock.Types.BodyType.String,
                            Encoding = System.Text.Encoding.UTF8
                        }
                    };
                }));
    }

    private void SetupUserCount()
    {
        // GET /admin/realms/foundry/users/count - Get user count
        _server!
            .Given(Request.Create()
                .WithPath("/admin/realms/foundry/users/count")
                .UsingGet())
            .AtPriority(0)
            .RespondWith(Response.Create()
                .WithCallback(_ =>
                {
                    int count = _users.Count;
                    return new WireMock.ResponseMessage
                    {
                        StatusCode = 200,
                        Headers = new Dictionary<string, WireMock.Types.WireMockList<string>>
                        {
                            { "Content-Type", new WireMock.Types.WireMockList<string>("application/json") }
                        },
                        BodyData = new WireMock.Util.BodyData
                        {
                            BodyAsString = count.ToString(),
                            DetectedBodyType = WireMock.Types.BodyType.String,
                            Encoding = System.Text.Encoding.UTF8
                        }
                    };
                }));
    }

    private void SetupOrganizationMembership()
    {
        // POST /admin/realms/foundry/organizations/*/members - Add user to org (accept silently)
        _server!
            .Given(Request.Create()
                .WithPath("/admin/realms/foundry/organizations/*/members")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(204));
    }

    private void SetupRoles()
    {
        // GET /admin/realms/foundry/roles/* - Get realm role (return 404 to skip role assignment)
        _server!
            .Given(Request.Create()
                .WithPath("/admin/realms/foundry/roles/*")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(404));

        // POST /admin/realms/foundry/users/*/role-mappings/realm - Assign role (accept silently)
        _server!
            .Given(Request.Create()
                .WithPath("/admin/realms/foundry/users/*/role-mappings/realm")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(204));
    }

}

public class KeycloakUser
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("firstName")]
    public string? FirstName { get; set; }

    [JsonPropertyName("lastName")]
    public string? LastName { get; set; }

    [JsonPropertyName("enabled")]
    public bool? Enabled { get; set; } = true;

    [JsonPropertyName("attributes")]
    public Dictionary<string, ICollection<string>>? Attributes { get; set; }
}
