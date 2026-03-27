using Wallow.Identity.Api.Contracts.Enums;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Domain.Enums;

namespace Wallow.Identity.Tests.Api.Contracts;

public class RequestContractTests
{
    private static readonly string[] _billingReadScope = ["billing:read"];
    private static readonly string[] _billingReadWriteScopes = ["billing:read", "billing:write"];
    private static readonly string[] _singleScope = ["scope1"];
    #region CreateUserRequest

    [Fact]
    public void CreateUserRequest_WithAllFields_CreatesInstance()
    {
        CreateUserRequest request = new("user@test.com", "John", "Doe", "pass123");

        request.Email.Should().Be("user@test.com");
        request.FirstName.Should().Be("John");
        request.LastName.Should().Be("Doe");
        request.Password.Should().Be("pass123");
    }

    [Fact]
    public void CreateUserRequest_WithNullPassword_CreatesInstance()
    {
        CreateUserRequest request = new("user@test.com", "John", "Doe", null);

        request.Password.Should().BeNull();
    }

    #endregion

    #region CreateOrganizationRequest

    [Fact]
    public void CreateOrganizationRequest_WithAllFields_CreatesInstance()
    {
        CreateOrganizationRequest request = new("Acme Corp", "acme.com");

        request.Name.Should().Be("Acme Corp");
        request.Domain.Should().Be("acme.com");
    }

    [Fact]
    public void CreateOrganizationRequest_WithNullDomain_CreatesInstance()
    {
        CreateOrganizationRequest request = new("Acme Corp", null);

        request.Domain.Should().BeNull();
    }

    #endregion

    #region AddMemberRequest

    [Fact]
    public void AddMemberRequest_WithUserId_CreatesInstance()
    {
        Guid userId = Guid.NewGuid();
        AddMemberRequest request = new(userId);

        request.UserId.Should().Be(userId);
    }

    #endregion

    #region AssignRoleRequest

    [Fact]
    public void AssignRoleRequest_WithRoleName_CreatesInstance()
    {
        AssignRoleRequest request = new("admin");

        request.RoleName.Should().Be("admin");
    }

    #endregion

    #region CreateServiceAccountRequest

    [Fact]
    public void CreateServiceAccountRequest_WithAllFields_CreatesInstance()
    {
        CreateServiceAccountRequest request = new("Backend", "Backend service", _billingReadScope);

        request.Name.Should().Be("Backend");
        request.Description.Should().Be("Backend service");
        request.Scopes.Should().HaveCount(1);
    }

    [Fact]
    public void CreateServiceAccountRequest_WithNullDescription_CreatesInstance()
    {
        CreateServiceAccountRequest request = new("Svc", null, _singleScope);

        request.Description.Should().BeNull();
    }

    #endregion

    #region UpdateScopesRequest

    [Fact]
    public void UpdateScopesRequest_WithScopes_CreatesInstance()
    {
        UpdateScopesRequest request = new(_billingReadWriteScopes);

        request.Scopes.Should().HaveCount(2);
    }

    #endregion

    #region ConfigureSamlSsoRequest

    [Fact]
    public void ConfigureSamlSsoRequest_WithAllFields_CreatesInstance()
    {
        ConfigureSamlSsoRequest request = new(
            "My IdP", "entity-id", "https://idp/sso", "https://idp/slo",
            "cert-data", ApiSamlNameIdFormat.Persistent,
            "mail", "firstName", "lastName", "groups",
            true, false, "admin", true);

        request.DisplayName.Should().Be("My IdP");
        request.EntityId.Should().Be("entity-id");
        request.SsoUrl.Should().Be("https://idp/sso");
        request.SloUrl.Should().Be("https://idp/slo");
        request.Certificate.Should().Be("cert-data");
        request.NameIdFormat.Should().Be(ApiSamlNameIdFormat.Persistent);
        request.EmailAttribute.Should().Be("mail");
        request.GroupsAttribute.Should().Be("groups");
        request.EnforceForAllUsers.Should().BeTrue();
        request.AutoProvisionUsers.Should().BeFalse();
        request.DefaultRole.Should().Be("admin");
        request.SyncGroupsAsRoles.Should().BeTrue();
    }

    [Fact]
    public void ConfigureSamlSsoRequest_WithDefaults_HasCorrectAttributeDefaults()
    {
        ConfigureSamlSsoRequest request = new(
            "IdP", "entity", "https://sso", null, "cert", ApiSamlNameIdFormat.Email);

        request.EmailAttribute.Should().Be("email");
        request.FirstNameAttribute.Should().Be("firstName");
        request.LastNameAttribute.Should().Be("lastName");
        request.GroupsAttribute.Should().BeNull();
        request.EnforceForAllUsers.Should().BeFalse();
        request.AutoProvisionUsers.Should().BeTrue();
        request.DefaultRole.Should().BeNull();
        request.SyncGroupsAsRoles.Should().BeFalse();
    }

    #endregion

    #region UpdateOrganizationSettingsRequest

    [Fact]
    public void UpdateOrganizationSettingsRequest_WithAllFields_CreatesInstance()
    {
        UpdateOrganizationSettingsRequest request = new(true, 14, LoginMethod.Password, "member");

        request.RequireMfa.Should().BeTrue();
        request.MfaGracePeriodDays.Should().Be(14);
        request.AllowedLoginMethods.Should().Be(LoginMethod.Password);
        request.DefaultMemberRole.Should().Be("member");
    }

    [Fact]
    public void UpdateOrganizationSettingsRequest_WithNulls_CreatesInstance()
    {
        UpdateOrganizationSettingsRequest request = new(null, null, null, null);

        request.RequireMfa.Should().BeNull();
        request.MfaGracePeriodDays.Should().BeNull();
        request.AllowedLoginMethods.Should().BeNull();
        request.DefaultMemberRole.Should().BeNull();
    }

    #endregion

    #region UpdateOrganizationBrandingRequest

    [Fact]
    public void UpdateOrganizationBrandingRequest_WithAllFields_CreatesInstance()
    {
        UpdateOrganizationBrandingRequest request = new("Acme Inc", "https://cdn.example.com/logo.png", "#FF5733");

        request.DisplayName.Should().Be("Acme Inc");
        request.LogoUrl.Should().Be("https://cdn.example.com/logo.png");
        request.PrimaryColor.Should().Be("#FF5733");
    }

    [Fact]
    public void UpdateOrganizationBrandingRequest_WithNulls_CreatesInstance()
    {
        UpdateOrganizationBrandingRequest request = new(null, null, null);

        request.DisplayName.Should().BeNull();
        request.LogoUrl.Should().BeNull();
        request.PrimaryColor.Should().BeNull();
    }

    #endregion

    #region ConfigureOidcSsoRequest

    [Fact]
    public void ConfigureOidcSsoRequest_WithAllFields_CreatesInstance()
    {
        ConfigureOidcSsoRequest request = new(
            "Azure AD", "https://issuer", "client-id", "client-secret",
            "openid email", "email", "given_name", "family_name", "groups",
            true, false, "user", true);

        request.DisplayName.Should().Be("Azure AD");
        request.Issuer.Should().Be("https://issuer");
        request.ClientId.Should().Be("client-id");
        request.ClientSecret.Should().Be("client-secret");
        request.Scopes.Should().Be("openid email");
        request.GroupsAttribute.Should().Be("groups");
        request.EnforceForAllUsers.Should().BeTrue();
        request.AutoProvisionUsers.Should().BeFalse();
        request.DefaultRole.Should().Be("user");
        request.SyncGroupsAsRoles.Should().BeTrue();
    }

    [Fact]
    public void ConfigureOidcSsoRequest_WithDefaults_HasCorrectDefaults()
    {
        ConfigureOidcSsoRequest request = new(
            "IdP", "https://issuer", "cid", "csecret");

        request.Scopes.Should().Be("openid profile email");
        request.EmailAttribute.Should().Be("email");
        request.FirstNameAttribute.Should().Be("given_name");
        request.LastNameAttribute.Should().Be("family_name");
        request.GroupsAttribute.Should().BeNull();
        request.EnforceForAllUsers.Should().BeFalse();
        request.AutoProvisionUsers.Should().BeTrue();
        request.DefaultRole.Should().BeNull();
        request.SyncGroupsAsRoles.Should().BeFalse();
    }

    #endregion
}
