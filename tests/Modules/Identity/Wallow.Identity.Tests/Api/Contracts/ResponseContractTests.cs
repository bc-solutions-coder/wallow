using Wallow.Identity.Api.Contracts.Responses;

namespace Wallow.Identity.Tests.Api.Contracts;

public class ResponseContractTests
{
    private static readonly string[] _adminUserRoles = ["admin", "user"];
    private static readonly string[] _usersReadWritePermissions = ["users.read", "users.write"];
    private static readonly string[] _billingReadScope = ["billing:read"];
    private static readonly string[] _billingReadWriteScopes = ["billing:read", "billing:write"];
    #region TokenResponse

    [Fact]
    public void TokenResponse_WithAllFields_CreatesInstance()
    {
        TokenResponse response = new(
            "access-token", "refresh-token", "Bearer", 300, 1800, "openid profile");

        response.AccessToken.Should().Be("access-token");
        response.RefreshToken.Should().Be("refresh-token");
        response.TokenType.Should().Be("Bearer");
        response.ExpiresIn.Should().Be(300);
        response.RefreshExpiresIn.Should().Be(1800);
        response.Scope.Should().Be("openid profile");
    }

    [Fact]
    public void TokenResponse_WithNullOptionalFields_CreatesCorrectly()
    {
        TokenResponse response = new("token", null, "Bearer", 300, null, null);

        response.RefreshToken.Should().BeNull();
        response.RefreshExpiresIn.Should().BeNull();
        response.Scope.Should().BeNull();
    }

    #endregion

    #region CurrentUserResponse

    [Fact]
    public void CurrentUserResponse_WithAllFields_CreatesInstance()
    {
        Guid id = Guid.NewGuid();
        CurrentUserResponse response = new CurrentUserResponse()
        {
            Id = id,
            Email = "user@test.com",
            FirstName = "John",
            LastName = "Doe",
            Roles = _adminUserRoles,
            Permissions = _usersReadWritePermissions
        };

        response.Id.Should().Be(id);
        response.Email.Should().Be("user@test.com");
        response.FirstName.Should().Be("John");
        response.LastName.Should().Be("Doe");
        response.Roles.Should().HaveCount(2);
        response.Permissions.Should().HaveCount(2);
    }

    [Fact]
    public void CurrentUserResponse_Defaults_HaveEmptyValues()
    {
        CurrentUserResponse response = new CurrentUserResponse();

        response.Id.Should().Be(Guid.Empty);
        response.Email.Should().Be(string.Empty);
        response.FirstName.Should().Be(string.Empty);
        response.LastName.Should().Be(string.Empty);
        response.Roles.Should().BeEmpty();
        response.Permissions.Should().BeEmpty();
    }

    #endregion

    #region CreateOrganizationResponse

    [Fact]
    public void CreateOrganizationResponse_WithOrgId_CreatesInstance()
    {
        Guid orgId = Guid.NewGuid();
        CreateOrganizationResponse response = new(orgId);

        response.OrganizationId.Should().Be(orgId);
    }

    #endregion

    #region ApiKeyCreatedResponse

    [Fact]
    public void ApiKeyCreatedResponse_WithAllFields_CreatesInstance()
    {
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddDays(30);
        ApiKeyCreatedResponse response = new(
            "key-id-1", "fnd_full-api-key", "fnd_full", "My Key",
            _billingReadScope, expiresAt);

        response.KeyId.Should().Be("key-id-1");
        response.ApiKey.Should().Be("fnd_full-api-key");
        response.Prefix.Should().Be("fnd_full");
        response.Name.Should().Be("My Key");
        response.Scopes.Should().HaveCount(1);
        response.ExpiresAt.Should().Be(expiresAt);
    }

    [Fact]
    public void ApiKeyCreatedResponse_WithNullExpiry_CreatesCorrectly()
    {
        ApiKeyCreatedResponse response = new(
            "k1", "key", "pfx", "Name", [], null);

        response.ExpiresAt.Should().BeNull();
    }

    #endregion

    #region ApiKeyResponse

    [Fact]
    public void ApiKeyResponse_WithAllFields_CreatesInstance()
    {
        DateTimeOffset created = DateTimeOffset.UtcNow.AddDays(-7);
        DateTimeOffset expires = DateTimeOffset.UtcNow.AddDays(23);
        DateTimeOffset lastUsed = DateTimeOffset.UtcNow.AddHours(-2);
        ApiKeyResponse response = new(
            "key-id", "Prod Key", "fnd_prod",
            _billingReadWriteScopes, created, expires, lastUsed);

        response.KeyId.Should().Be("key-id");
        response.Name.Should().Be("Prod Key");
        response.Prefix.Should().Be("fnd_prod");
        response.Scopes.Should().HaveCount(2);
        response.CreatedAt.Should().Be(created);
        response.ExpiresAt.Should().Be(expires);
        response.LastUsedAt.Should().Be(lastUsed);
    }

    [Fact]
    public void ApiKeyResponse_WithNullOptionalFields_CreatesCorrectly()
    {
        DateTimeOffset created = DateTimeOffset.UtcNow;
        ApiKeyResponse response = new("k1", "Key", "pfx", [], created, null, null);

        response.ExpiresAt.Should().BeNull();
        response.LastUsedAt.Should().BeNull();
    }

    #endregion

    #region ServiceAccountCreatedResponse

    [Fact]
    public void ServiceAccountCreatedResponse_WithAllFields_CreatesInstance()
    {
        ServiceAccountCreatedResponse response = new ServiceAccountCreatedResponse()
        {
            Id = "sa-id",
            ClientId = "client-backend",
            ClientSecret = "secret-xyz",
            TokenEndpoint = "https://keycloak/token",
            Scopes = _billingReadScope
        };

        response.Id.Should().Be("sa-id");
        response.ClientId.Should().Be("client-backend");
        response.ClientSecret.Should().Be("secret-xyz");
        response.TokenEndpoint.Should().Be("https://keycloak/token");
        response.Scopes.Should().HaveCount(1);
        response.Warning.Should().Contain("Save this secret");
    }

    #endregion

    #region SecretRotatedResponse

    [Fact]
    public void SecretRotatedResponse_WithAllFields_CreatesInstance()
    {
        DateTime rotatedAt = DateTime.UtcNow;
        SecretRotatedResponse response = new SecretRotatedResponse()
        {
            NewClientSecret = "new-secret",
            RotatedAt = rotatedAt
        };

        response.NewClientSecret.Should().Be("new-secret");
        response.RotatedAt.Should().Be(rotatedAt);
        response.Warning.Should().Contain("Save this secret");
    }

    #endregion
}
