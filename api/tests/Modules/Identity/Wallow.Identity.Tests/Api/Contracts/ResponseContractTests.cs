using Wallow.Identity.Api.Contracts.Responses;
using Wallow.Identity.Domain.Enums;

namespace Wallow.Identity.Tests.Api.Contracts;

public class ResponseContractTests
{
    private static readonly string[] _adminUserRoles = ["admin", "user"];
    private static readonly string[] _usersReadWritePermissions = ["users.read", "users.write"];
    private static readonly string[] _billingReadScope = ["billing:read"];

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

    #region ServiceAccountCreatedResponse

    [Fact]
    public void ServiceAccountCreatedResponse_WithAllFields_CreatesInstance()
    {
        ServiceAccountCreatedResponse response = new ServiceAccountCreatedResponse()
        {
            Id = "sa-id",
            ClientId = "client-backend",
            ClientSecret = "secret-xyz",
            TokenEndpoint = "https://auth.example.com/connect/token",
            Scopes = _billingReadScope
        };

        response.Id.Should().Be("sa-id");
        response.ClientId.Should().Be("client-backend");
        response.ClientSecret.Should().Be("secret-xyz");
        response.TokenEndpoint.Should().Be("https://auth.example.com/connect/token");
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

    #region OrganizationSettingsResponse

    [Fact]
    public void OrganizationSettingsResponse_WithAllFields_CreatesInstance()
    {
        OrganizationSettingsResponse response = new(true, false, 7, LoginMethod.Password, "user");

        response.RequireMfa.Should().BeTrue();
        response.AllowPasswordlessLogin.Should().BeFalse();
        response.MfaGracePeriodDays.Should().Be(7);
        response.AllowedLoginMethods.Should().Be(LoginMethod.Password);
        response.DefaultMemberRole.Should().Be("user");
    }

    [Fact]
    public void OrganizationSettingsResponse_WithNullRole_CreatesInstance()
    {
        OrganizationSettingsResponse response = new(false, true, 0, LoginMethod.None, null);

        response.DefaultMemberRole.Should().BeNull();
    }

    #endregion

    #region OrganizationBrandingResponse

    [Fact]
    public void OrganizationBrandingResponse_WithAllFields_CreatesInstance()
    {
        OrganizationBrandingResponse response = new("Acme Corp", "https://cdn.example.com/logo.png", "#1a2b3c", "#4d5e6f");

        response.DisplayName.Should().Be("Acme Corp");
        response.LogoUrl.Should().Be("https://cdn.example.com/logo.png");
        response.PrimaryColor.Should().Be("#1a2b3c");
        response.AccentColor.Should().Be("#4d5e6f");
    }

    [Fact]
    public void OrganizationBrandingResponse_WithNulls_CreatesInstance()
    {
        OrganizationBrandingResponse response = new(null, null, null, null);

        response.DisplayName.Should().BeNull();
        response.LogoUrl.Should().BeNull();
        response.PrimaryColor.Should().BeNull();
        response.AccentColor.Should().BeNull();
    }

    #endregion
}
