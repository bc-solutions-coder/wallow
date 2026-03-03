using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Domain.Enums;
using Foundry.Identity.Domain.Events;
using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Identity.Tests.Domain;

public class SsoConfigurationTests
{
    private static readonly TenantId _tenantId = TenantId.Create(Guid.NewGuid());
    private static readonly Guid _testUserId = Guid.NewGuid();

    private static SsoConfiguration CreateSamlConfig() =>
        SsoConfiguration.Create(_tenantId, "Test SAML", SsoProtocol.Saml, "email", "firstName", "lastName", _testUserId);

    private static SsoConfiguration CreateOidcConfig() =>
        SsoConfiguration.Create(_tenantId, "Test OIDC", SsoProtocol.Oidc, "email", "firstName", "lastName", _testUserId);

    [Fact]
    public void Create_WithValidParameters_CreatesDraftConfiguration()
    {
        SsoConfiguration config = CreateSamlConfig();

        config.TenantId.Should().Be(_tenantId);
        config.DisplayName.Should().Be("Test SAML");
        config.Protocol.Should().Be(SsoProtocol.Saml);
        config.Status.Should().Be(SsoStatus.Draft);
        config.EmailAttribute.Should().Be("email");
        config.FirstNameAttribute.Should().Be("firstName");
        config.LastNameAttribute.Should().Be("lastName");
        config.EnforceForAllUsers.Should().BeFalse();
        config.AutoProvisionUsers.Should().BeTrue();
        config.SyncGroupsAsRoles.Should().BeFalse();
    }

    [Fact]
    public void Create_WithEmptyDisplayName_ThrowsBusinessRuleException()
    {
        Action act = () => SsoConfiguration.Create(_tenantId, "", SsoProtocol.Saml, "email", "first", "last", _testUserId);

        act.Should().Throw<BusinessRuleException>().WithMessage("*display name*");
    }

    [Fact]
    public void Create_WithEmptyEmailAttribute_ThrowsBusinessRuleException()
    {
        Action act = () => SsoConfiguration.Create(_tenantId, "Test", SsoProtocol.Saml, "", "first", "last", _testUserId);

        act.Should().Throw<BusinessRuleException>().WithMessage("*Email attribute*");
    }

    [Fact]
    public void Create_WithEmptyFirstNameAttribute_ThrowsBusinessRuleException()
    {
        Action act = () => SsoConfiguration.Create(_tenantId, "Test", SsoProtocol.Saml, "email", "", "last", _testUserId);

        act.Should().Throw<BusinessRuleException>().WithMessage("*First name*");
    }

    [Fact]
    public void Create_WithEmptyLastNameAttribute_ThrowsBusinessRuleException()
    {
        Action act = () => SsoConfiguration.Create(_tenantId, "Test", SsoProtocol.Saml, "email", "first", "", _testUserId);

        act.Should().Throw<BusinessRuleException>().WithMessage("*Last name*");
    }

    [Fact]
    public void MoveToTesting_FromDraft_SetsStatusToTesting()
    {
        SsoConfiguration config = CreateSamlConfig();

        config.MoveToTesting(_testUserId);

        config.Status.Should().Be(SsoStatus.Testing);
    }

    [Fact]
    public void MoveToTesting_FromNonDraft_ThrowsBusinessRuleException()
    {
        SsoConfiguration config = CreateSamlConfig();
        config.UpdateSamlConfig("entity-id", "https://sso.example.com", null, "cert-data", SamlNameIdFormat.Email, _testUserId);
        config.Activate(_testUserId);

        Action act = () => config.MoveToTesting(_testUserId);

        act.Should().Throw<BusinessRuleException>().WithMessage("*Draft*");
    }

    [Fact]
    public void Activate_SamlWithCompleteConfig_SetsStatusToActive()
    {
        SsoConfiguration config = CreateSamlConfig();
        config.UpdateSamlConfig("entity-id", "https://sso.example.com", null, "cert-data", SamlNameIdFormat.Email, _testUserId);

        config.Activate(_testUserId);

        config.Status.Should().Be(SsoStatus.Active);
    }

    [Fact]
    public void Activate_SamlWithCompleteConfig_RaisesSsoConfigurationActivatedEvent()
    {
        SsoConfiguration config = CreateSamlConfig();
        config.UpdateSamlConfig("entity-id", "https://sso.example.com", null, "cert-data", SamlNameIdFormat.Email, _testUserId);

        config.Activate(_testUserId);

        config.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<SsoConfigurationActivatedEvent>()
            .Which.Should().Match<SsoConfigurationActivatedEvent>(e =>
                e.TenantId == _tenantId.Value &&
                e.DisplayName == "Test SAML" &&
                e.Protocol == "SAML");
    }

    [Fact]
    public void Activate_WhenAlreadyActive_ThrowsBusinessRuleException()
    {
        SsoConfiguration config = CreateSamlConfig();
        config.UpdateSamlConfig("entity-id", "https://sso.example.com", null, "cert-data", SamlNameIdFormat.Email, _testUserId);
        config.Activate(_testUserId);

        Action act = () => config.Activate(_testUserId);

        act.Should().Throw<BusinessRuleException>().WithMessage("*already active*");
    }

    [Fact]
    public void Activate_SamlWithoutEntityId_ThrowsBusinessRuleException()
    {
        SsoConfiguration config = CreateSamlConfig();

        Action act = () => config.Activate(_testUserId);

        act.Should().Throw<BusinessRuleException>().WithMessage("*SAML configuration is incomplete*");
    }

    [Fact]
    public void Activate_OidcWithoutIssuer_ThrowsBusinessRuleException()
    {
        SsoConfiguration config = CreateOidcConfig();

        Action act = () => config.Activate(_testUserId);

        act.Should().Throw<BusinessRuleException>().WithMessage("*OIDC configuration is incomplete*");
    }

    [Fact]
    public void Activate_OidcWithCompleteConfig_SetsStatusToActive()
    {
        SsoConfiguration config = CreateOidcConfig();
        config.UpdateOidcConfig("https://issuer.example.com", "client-id", "client-secret", "openid profile", _testUserId);

        config.Activate(_testUserId);

        config.Status.Should().Be(SsoStatus.Active);
    }

    [Fact]
    public void Disable_WhenActive_SetsStatusToDisabled()
    {
        SsoConfiguration config = CreateSamlConfig();
        config.UpdateSamlConfig("entity-id", "https://sso.example.com", null, "cert-data", SamlNameIdFormat.Email, _testUserId);
        config.Activate(_testUserId);

        config.Disable(_testUserId);

        config.Status.Should().Be(SsoStatus.Disabled);
    }

    [Fact]
    public void Disable_WhenAlreadyDisabled_ThrowsBusinessRuleException()
    {
        SsoConfiguration config = CreateSamlConfig();
        config.UpdateSamlConfig("entity-id", "https://sso.example.com", null, "cert", SamlNameIdFormat.Email, _testUserId);
        config.Activate(_testUserId);
        config.Disable(_testUserId);

        Action act = () => config.Disable(_testUserId);

        act.Should().Throw<BusinessRuleException>().WithMessage("*already disabled*");
    }

    [Fact]
    public void UpdateSamlConfig_WithValidParams_UpdatesSamlFields()
    {
        SsoConfiguration config = CreateSamlConfig();

        config.UpdateSamlConfig("entity-id", "https://sso.example.com", "https://slo.example.com", "cert-data", SamlNameIdFormat.Persistent, _testUserId);

        config.SamlEntityId.Should().Be("entity-id");
        config.SamlSsoUrl.Should().Be("https://sso.example.com");
        config.SamlSloUrl.Should().Be("https://slo.example.com");
        config.SamlCertificate.Should().Be("cert-data");
        config.SamlNameIdFormat.Should().Be(SamlNameIdFormat.Persistent);
    }

    [Fact]
    public void UpdateSamlConfig_OnOidcProtocol_ThrowsBusinessRuleException()
    {
        SsoConfiguration config = CreateOidcConfig();

        Action act = () => config.UpdateSamlConfig("entity-id", "https://sso.example.com", null, "cert", SamlNameIdFormat.Email, _testUserId);

        act.Should().Throw<BusinessRuleException>().WithMessage("*non-SAML*");
    }

    [Fact]
    public void UpdateSamlConfig_WhenActive_ThrowsBusinessRuleException()
    {
        SsoConfiguration config = CreateSamlConfig();
        config.UpdateSamlConfig("entity-id", "https://sso.example.com", null, "cert", SamlNameIdFormat.Email, _testUserId);
        config.Activate(_testUserId);

        Action act = () => config.UpdateSamlConfig("new-entity", "https://new.example.com", null, "new-cert", SamlNameIdFormat.Persistent, _testUserId);

        act.Should().Throw<BusinessRuleException>().WithMessage("*active*");
    }

    [Fact]
    public void UpdateSamlConfig_WithEmptyEntityId_ThrowsBusinessRuleException()
    {
        SsoConfiguration config = CreateSamlConfig();

        Action act = () => config.UpdateSamlConfig("", "https://sso.example.com", null, "cert", SamlNameIdFormat.Email, _testUserId);

        act.Should().Throw<BusinessRuleException>().WithMessage("*entity ID*");
    }

    [Fact]
    public void UpdateSamlConfig_WithEmptySsoUrl_ThrowsBusinessRuleException()
    {
        SsoConfiguration config = CreateSamlConfig();

        Action act = () => config.UpdateSamlConfig("entity-id", "", null, "cert", SamlNameIdFormat.Email, _testUserId);

        act.Should().Throw<BusinessRuleException>().WithMessage("*SSO URL*");
    }

    [Fact]
    public void UpdateSamlConfig_WithEmptyCertificate_ThrowsBusinessRuleException()
    {
        SsoConfiguration config = CreateSamlConfig();

        Action act = () => config.UpdateSamlConfig("entity-id", "https://sso.example.com", null, "", SamlNameIdFormat.Email, _testUserId);

        act.Should().Throw<BusinessRuleException>().WithMessage("*certificate*");
    }

    [Fact]
    public void UpdateOidcConfig_WithValidParams_UpdatesOidcFields()
    {
        SsoConfiguration config = CreateOidcConfig();

        config.UpdateOidcConfig("https://issuer.example.com", "client-id", "client-secret", "openid profile email", _testUserId);

        config.OidcIssuer.Should().Be("https://issuer.example.com");
        config.OidcClientId.Should().Be("client-id");
        config.OidcClientSecret.Should().Be("client-secret");
        config.OidcScopes.Should().Be("openid profile email");
    }

    [Fact]
    public void UpdateOidcConfig_OnSamlProtocol_ThrowsBusinessRuleException()
    {
        SsoConfiguration config = CreateSamlConfig();

        Action act = () => config.UpdateOidcConfig("https://issuer.example.com", "client-id", "secret", "openid", _testUserId);

        act.Should().Throw<BusinessRuleException>().WithMessage("*non-OIDC*");
    }

    [Fact]
    public void UpdateOidcConfig_WhenActive_ThrowsBusinessRuleException()
    {
        SsoConfiguration config = CreateOidcConfig();
        config.UpdateOidcConfig("https://issuer.example.com", "client-id", "secret", "openid", _testUserId);
        config.Activate(_testUserId);

        Action act = () => config.UpdateOidcConfig("https://new.example.com", "new-client", "new-secret", "openid", _testUserId);

        act.Should().Throw<BusinessRuleException>().WithMessage("*active*");
    }

    [Fact]
    public void UpdateOidcConfig_WithEmptyIssuer_ThrowsBusinessRuleException()
    {
        SsoConfiguration config = CreateOidcConfig();

        Action act = () => config.UpdateOidcConfig("", "client-id", "secret", "openid", _testUserId);

        act.Should().Throw<BusinessRuleException>().WithMessage("*issuer*");
    }

    [Fact]
    public void UpdateOidcConfig_WithEmptyClientId_ThrowsBusinessRuleException()
    {
        SsoConfiguration config = CreateOidcConfig();

        Action act = () => config.UpdateOidcConfig("https://issuer.example.com", "", "secret", "openid", _testUserId);

        act.Should().Throw<BusinessRuleException>().WithMessage("*client ID*");
    }

    [Fact]
    public void UpdateOidcConfig_WithEmptyClientSecret_ThrowsBusinessRuleException()
    {
        SsoConfiguration config = CreateOidcConfig();

        Action act = () => config.UpdateOidcConfig("https://issuer.example.com", "client-id", "", "openid", _testUserId);

        act.Should().Throw<BusinessRuleException>().WithMessage("*client secret*");
    }

    [Fact]
    public void UpdateBehaviorSettings_WhenNotActive_UpdatesSettings()
    {
        SsoConfiguration config = CreateSamlConfig();

        config.UpdateBehaviorSettings(true, false, "viewer", true, "groups", _testUserId);

        config.EnforceForAllUsers.Should().BeTrue();
        config.AutoProvisionUsers.Should().BeFalse();
        config.DefaultRole.Should().Be("viewer");
        config.SyncGroupsAsRoles.Should().BeTrue();
        config.GroupsAttribute.Should().Be("groups");
    }

    [Fact]
    public void UpdateBehaviorSettings_WhenActive_ThrowsBusinessRuleException()
    {
        SsoConfiguration config = CreateSamlConfig();
        config.UpdateSamlConfig("entity-id", "https://sso.example.com", null, "cert", SamlNameIdFormat.Email, _testUserId);
        config.Activate(_testUserId);

        Action act = () => config.UpdateBehaviorSettings(true, false, "viewer", true, "groups", _testUserId);

        act.Should().Throw<BusinessRuleException>().WithMessage("*active*");
    }

    [Fact]
    public void SetKeycloakIdpAlias_SetsAlias()
    {
        SsoConfiguration config = CreateSamlConfig();

        config.SetKeycloakIdpAlias("saml-idp-alias", _testUserId);

        config.KeycloakIdpAlias.Should().Be("saml-idp-alias");
    }
}
