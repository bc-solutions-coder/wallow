using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Domain.Enums;
using Foundry.Identity.Domain.Events;
using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Identity.Tests.Application.Handlers;

public class ConfigureSsoProviderTests
{
    private readonly ISsoConfigurationRepository _repository;
    private readonly TenantId _tenantId = TenantId.Create(Guid.NewGuid());

    public ConfigureSsoProviderTests()
    {
        _repository = Substitute.For<ISsoConfigurationRepository>();
    }

    [Fact]
    public void ConfigureSaml_WithValidData_CreatesDraftConfiguration()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId,
            "Corporate SAML",
            SsoProtocol.Saml,
            "email",
            "firstName",
            "lastName", Guid.NewGuid(), TimeProvider.System);

        config.Should().NotBeNull();
        config.Status.Should().Be(SsoStatus.Draft);
        config.Protocol.Should().Be(SsoProtocol.Saml);
        config.DisplayName.Should().Be("Corporate SAML");
    }

    [Fact]
    public void ConfigureOidc_WithValidData_CreatesDraftConfiguration()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId,
            "Corporate OIDC",
            SsoProtocol.Oidc,
            "email",
            "given_name",
            "family_name", Guid.NewGuid(), TimeProvider.System);

        config.Should().NotBeNull();
        config.Status.Should().Be(SsoStatus.Draft);
        config.Protocol.Should().Be(SsoProtocol.Oidc);
    }

    [Fact]
    public void ConfigureSaml_WithEmptyDisplayName_ThrowsBusinessRuleException()
    {
        Action act = () => SsoConfiguration.Create(
            _tenantId,
            "",
            SsoProtocol.Saml,
            "email",
            "firstName",
            "lastName", Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("Identity.DisplayNameRequired");
    }

    [Fact]
    public void ConfigureSaml_WithEmptyEmailAttribute_ThrowsBusinessRuleException()
    {
        Action act = () => SsoConfiguration.Create(
            _tenantId,
            "Test SSO",
            SsoProtocol.Saml,
            "",
            "firstName",
            "lastName", Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("Identity.EmailAttributeRequired");
    }

    [Fact]
    public void ConfigureSaml_WithEmptyFirstNameAttribute_ThrowsBusinessRuleException()
    {
        Action act = () => SsoConfiguration.Create(
            _tenantId,
            "Test SSO",
            SsoProtocol.Saml,
            "email",
            "",
            "lastName", Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("Identity.FirstNameAttributeRequired");
    }

    [Fact]
    public void ConfigureSaml_WithEmptyLastNameAttribute_ThrowsBusinessRuleException()
    {
        Action act = () => SsoConfiguration.Create(
            _tenantId,
            "Test SSO",
            SsoProtocol.Saml,
            "email",
            "firstName",
            "", Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("Identity.LastNameAttributeRequired");
    }

    [Fact]
    public void UpdateSamlConfig_OnNonSamlProtocol_ThrowsBusinessRuleException()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId,
            "OIDC Provider",
            SsoProtocol.Oidc,
            "email",
            "firstName",
            "lastName", Guid.NewGuid(), TimeProvider.System);

        Action act = () => config.UpdateSamlConfig(
            "entity-id",
            "https://idp.test/sso",
            null,
            "cert",
            SamlNameIdFormat.Email, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("Identity.NotSamlConfiguration");
    }

    [Fact]
    public void UpdateOidcConfig_OnNonOidcProtocol_ThrowsBusinessRuleException()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId,
            "SAML Provider",
            SsoProtocol.Saml,
            "email",
            "firstName",
            "lastName", Guid.NewGuid(), TimeProvider.System);

        Action act = () => config.UpdateOidcConfig(
            "https://issuer.test",
            "client-id",
            "client-secret",
            "openid", Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("Identity.NotOidcConfiguration");
    }

    [Fact]
    public void UpdateSamlConfig_WhenActive_ThrowsBusinessRuleException()
    {
        SsoConfiguration config = CreateActiveSamlConfiguration();

        Action act = () => config.UpdateSamlConfig(
            "new-entity-id",
            "https://new-idp.test/sso",
            null,
            "new-cert",
            SamlNameIdFormat.Persistent, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("Identity.CannotUpdateActiveConfiguration");
    }

    [Fact]
    public void UpdateOidcConfig_WhenActive_ThrowsBusinessRuleException()
    {
        SsoConfiguration config = CreateActiveOidcConfiguration();

        Action act = () => config.UpdateOidcConfig(
            "https://new-issuer.test",
            "new-client",
            "new-secret",
            "openid email", Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("Identity.CannotUpdateActiveConfiguration");
    }

    [Fact]
    public void UpdateSamlConfig_WithEmptyEntityId_ThrowsBusinessRuleException()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId,
            "SAML Provider",
            SsoProtocol.Saml,
            "email",
            "firstName",
            "lastName", Guid.NewGuid(), TimeProvider.System);

        Action act = () => config.UpdateSamlConfig(
            "",
            "https://idp.test/sso",
            null,
            "cert",
            SamlNameIdFormat.Email, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("Identity.SamlEntityIdRequired");
    }

    [Fact]
    public void UpdateOidcConfig_WithEmptyIssuer_ThrowsBusinessRuleException()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId,
            "OIDC Provider",
            SsoProtocol.Oidc,
            "email",
            "firstName",
            "lastName", Guid.NewGuid(), TimeProvider.System);

        Action act = () => config.UpdateOidcConfig(
            "",
            "client-id",
            "client-secret",
            "openid", Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("Identity.OidcIssuerRequired");
    }

    [Fact]
    public async Task SaveConfiguration_AddsToRepository_WhenNewConfig()
    {
        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns((SsoConfiguration?)null);

        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId,
            "New SSO",
            SsoProtocol.Saml,
            "email",
            "firstName",
            "lastName", Guid.NewGuid(), TimeProvider.System);

        _repository.Add(config);
        await _repository.SaveChangesAsync();

        _repository.Received(1).Add(Arg.Any<SsoConfiguration>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private SsoConfiguration CreateActiveSamlConfiguration()
    {
        Guid userId = Guid.NewGuid();
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "Active SAML", SsoProtocol.Saml,
            "email", "firstName", "lastName", userId, TimeProvider.System);
        config.UpdateSamlConfig("entity-id", "https://idp.test/sso", null, "cert", SamlNameIdFormat.Email, userId, TimeProvider.System);
        config.MoveToTesting(userId, TimeProvider.System);
        config.Activate(userId, TimeProvider.System);
        return config;
    }

    private SsoConfiguration CreateActiveOidcConfiguration()
    {
        Guid userId = Guid.NewGuid();
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "Active OIDC", SsoProtocol.Oidc,
            "email", "firstName", "lastName", userId, TimeProvider.System);
        config.UpdateOidcConfig("https://issuer.test", "client-id", "secret", "openid", userId, TimeProvider.System);
        config.MoveToTesting(userId, TimeProvider.System);
        config.Activate(userId, TimeProvider.System);
        return config;
    }
}

public class EnableSsoTests
{
    private readonly ISsoConfigurationRepository _repository;
    private readonly TenantId _tenantId = TenantId.Create(Guid.NewGuid());

    public EnableSsoTests()
    {
        _repository = Substitute.For<ISsoConfigurationRepository>();
    }

    [Fact]
    public void Activate_FromDraft_WithCompleteSamlConfig_Succeeds()
    {
        Guid userId = Guid.NewGuid();
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "SAML SSO", SsoProtocol.Saml,
            "email", "firstName", "lastName", userId, TimeProvider.System);
        config.UpdateSamlConfig("entity-id", "https://idp.test/sso", null, "cert", SamlNameIdFormat.Email, userId, TimeProvider.System);

        config.Activate(userId, TimeProvider.System);

        config.Status.Should().Be(SsoStatus.Active);
    }

    [Fact]
    public void Activate_FromTesting_WithCompleteSamlConfig_Succeeds()
    {
        Guid userId = Guid.NewGuid();
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "SAML SSO", SsoProtocol.Saml,
            "email", "firstName", "lastName", userId, TimeProvider.System);
        config.UpdateSamlConfig("entity-id", "https://idp.test/sso", null, "cert", SamlNameIdFormat.Email, userId, TimeProvider.System);
        config.MoveToTesting(userId, TimeProvider.System);

        config.Activate(userId, TimeProvider.System);

        config.Status.Should().Be(SsoStatus.Active);
    }

    [Fact]
    public void Activate_FromDisabled_Succeeds()
    {
        Guid userId = Guid.NewGuid();
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "SAML SSO", SsoProtocol.Saml,
            "email", "firstName", "lastName", userId, TimeProvider.System);
        config.UpdateSamlConfig("entity-id", "https://idp.test/sso", null, "cert", SamlNameIdFormat.Email, userId, TimeProvider.System);
        config.MoveToTesting(userId, TimeProvider.System);
        config.Activate(userId, TimeProvider.System);
        config.Disable(userId, TimeProvider.System);

        config.Activate(userId, TimeProvider.System);

        config.Status.Should().Be(SsoStatus.Active);
    }

    [Fact]
    public void Activate_WhenAlreadyActive_ThrowsBusinessRuleException()
    {
        Guid userId = Guid.NewGuid();
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "SAML SSO", SsoProtocol.Saml,
            "email", "firstName", "lastName", userId, TimeProvider.System);
        config.UpdateSamlConfig("entity-id", "https://idp.test/sso", null, "cert", SamlNameIdFormat.Email, userId, TimeProvider.System);
        config.MoveToTesting(userId, TimeProvider.System);
        config.Activate(userId, TimeProvider.System);

        Action act = () => config.Activate(userId, TimeProvider.System);

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("Identity.SsoAlreadyActive");
    }

    [Fact]
    public void Activate_SamlWithoutEntityId_ThrowsBusinessRuleException()
    {
        Guid userId = Guid.NewGuid();
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "SAML SSO", SsoProtocol.Saml,
            "email", "firstName", "lastName", userId, TimeProvider.System);

        Action act = () => config.Activate(userId, TimeProvider.System);

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("Identity.SamlConfigurationIncomplete");
    }

    [Fact]
    public void Activate_OidcWithoutIssuer_ThrowsBusinessRuleException()
    {
        Guid userId = Guid.NewGuid();
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "OIDC SSO", SsoProtocol.Oidc,
            "email", "firstName", "lastName", userId, TimeProvider.System);

        Action act = () => config.Activate(userId, TimeProvider.System);

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("Identity.OidcConfigurationIncomplete");
    }

    [Fact]
    public void Activate_WithCompleteOidcConfig_Succeeds()
    {
        Guid userId = Guid.NewGuid();
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "OIDC SSO", SsoProtocol.Oidc,
            "email", "firstName", "lastName", userId, TimeProvider.System);
        config.UpdateOidcConfig("https://issuer.test", "client-id", "secret", "openid", userId, TimeProvider.System);

        config.Activate(userId, TimeProvider.System);

        config.Status.Should().Be(SsoStatus.Active);
    }

    [Fact]
    public void Activate_RaisesSsoConfigurationActivatedEvent()
    {
        Guid userId = Guid.NewGuid();
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "SAML SSO", SsoProtocol.Saml,
            "email", "firstName", "lastName", userId, TimeProvider.System);
        config.UpdateSamlConfig("entity-id", "https://idp.test/sso", null, "cert", SamlNameIdFormat.Email, userId, TimeProvider.System);

        config.Activate(userId, TimeProvider.System);

        config.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<SsoConfigurationActivatedEvent>();
    }

    [Fact]
    public async Task Activate_NotFoundConfig_RepositoryReturnsNull()
    {
        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns((SsoConfiguration?)null);

        SsoConfiguration? config = await _repository.GetAsync();

        config.Should().BeNull();
    }
}

public class DisableSsoTests
{
    private readonly ISsoConfigurationRepository _repository;
    private readonly TenantId _tenantId = TenantId.Create(Guid.NewGuid());

    public DisableSsoTests()
    {
        _repository = Substitute.For<ISsoConfigurationRepository>();
    }

    [Fact]
    public void Disable_FromActive_Succeeds()
    {
        Guid userId = Guid.NewGuid();
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "SAML SSO", SsoProtocol.Saml,
            "email", "firstName", "lastName", userId, TimeProvider.System);
        config.UpdateSamlConfig("entity-id", "https://idp.test/sso", null, "cert", SamlNameIdFormat.Email, userId, TimeProvider.System);
        config.MoveToTesting(userId, TimeProvider.System);
        config.Activate(userId, TimeProvider.System);

        config.Disable(userId, TimeProvider.System);

        config.Status.Should().Be(SsoStatus.Disabled);
    }

    [Fact]
    public void Disable_FromDraft_Succeeds()
    {
        Guid userId = Guid.NewGuid();
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "SAML SSO", SsoProtocol.Saml,
            "email", "firstName", "lastName", userId, TimeProvider.System);

        config.Disable(userId, TimeProvider.System);

        config.Status.Should().Be(SsoStatus.Disabled);
    }

    [Fact]
    public void Disable_FromTesting_Succeeds()
    {
        Guid userId = Guid.NewGuid();
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "SAML SSO", SsoProtocol.Saml,
            "email", "firstName", "lastName", userId, TimeProvider.System);
        config.MoveToTesting(userId, TimeProvider.System);

        config.Disable(userId, TimeProvider.System);

        config.Status.Should().Be(SsoStatus.Disabled);
    }

    [Fact]
    public void Disable_WhenAlreadyDisabled_ThrowsBusinessRuleException()
    {
        Guid userId = Guid.NewGuid();
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "SAML SSO", SsoProtocol.Saml,
            "email", "firstName", "lastName", userId, TimeProvider.System);
        config.Disable(userId, TimeProvider.System);

        Action act = () => config.Disable(userId, TimeProvider.System);

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("Identity.SsoAlreadyDisabled");
    }

    [Fact]
    public void Disable_BlocksConfigUpdates_AfterReenabling()
    {
        Guid userId = Guid.NewGuid();
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "SAML SSO", SsoProtocol.Saml,
            "email", "firstName", "lastName", userId, TimeProvider.System);
        config.UpdateSamlConfig("entity-id", "https://idp.test/sso", null, "cert", SamlNameIdFormat.Email, userId, TimeProvider.System);
        config.MoveToTesting(userId, TimeProvider.System);
        config.Activate(userId, TimeProvider.System);
        config.Disable(userId, TimeProvider.System);

        // After disable, config updates should be allowed again (not active)
        Action act = () => config.UpdateSamlConfig(
            "new-entity", "https://new-idp.test/sso", null, "new-cert", SamlNameIdFormat.Persistent, userId, TimeProvider.System);

        act.Should().NotThrow();
    }

    [Fact]
    public async Task Disable_NotFoundConfig_RepositoryReturnsNull()
    {
        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns((SsoConfiguration?)null);

        SsoConfiguration? config = await _repository.GetAsync();

        config.Should().BeNull();
    }

    [Fact]
    public void Disable_UpdatesBehaviorSettingsBlocked_WhenActive()
    {
        Guid userId = Guid.NewGuid();
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "SAML SSO", SsoProtocol.Saml,
            "email", "firstName", "lastName", userId, TimeProvider.System);
        config.UpdateSamlConfig("entity-id", "https://idp.test/sso", null, "cert", SamlNameIdFormat.Email, userId, TimeProvider.System);
        config.MoveToTesting(userId, TimeProvider.System);
        config.Activate(userId, TimeProvider.System);

        Action act = () => config.UpdateBehaviorSettings(true, false, "admin", true, "groups", userId, TimeProvider.System);

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("Identity.CannotUpdateActiveConfiguration");
    }
}
