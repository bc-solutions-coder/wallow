using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Domain.Enums;
using Foundry.Identity.Infrastructure;
using Foundry.Identity.Infrastructure.Services;
using Foundry.Shared.Kernel.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
#pragma warning disable CA2000 // HttpClient/HttpMessageHandler lifetime is managed by test framework

namespace Foundry.Identity.Tests.Infrastructure;

/// <summary>
/// Final coverage gap tests targeting specific uncovered lines in Identity.Infrastructure.
/// </summary>
public class IdentityCoverageGapFinalTests
{
    private readonly ILogger<KeycloakIdpService> _idpLogger = Substitute.For<ILogger<KeycloakIdpService>>();

    // --- ValidateSamlConfiguration: SamlSsoUrl missing (lines 187-189) ---

    [Fact]
    public void ValidateSamlConfiguration_WhenSsoUrlMissing_ReturnsFailure()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            TenantId.Create(Guid.NewGuid()), "Test", SsoProtocol.Saml,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);

        // Set SamlEntityId via reflection since UpdateSamlConfig requires all fields
        PropertyInfo? entityIdProp = typeof(SsoConfiguration).GetProperty("SamlEntityId");
        entityIdProp?.SetValue(config, "https://idp.test/entity");

        SsoValidationResult result = KeycloakIdpService.ValidateSamlConfiguration(config);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Be("SAML SSO URL not configured");
    }

    // --- ValidateSamlConfiguration: valid cert with expiry (lines 197-199, 207-212) ---

    [Fact]
    public void ValidateSamlConfiguration_WithValidCert_ReturnsSuccessWithExpiry()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            TenantId.Create(Guid.NewGuid()), "Test", SsoProtocol.Saml,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);

        string validCertBase64 = GenerateSelfSignedCertBase64(DateTimeOffset.UtcNow.AddYears(1));

        config.UpdateSamlConfig("entity-id", "https://idp.test/sso", null, validCertBase64,
            SamlNameIdFormat.Email, Guid.Empty, TimeProvider.System);

        SsoValidationResult result = KeycloakIdpService.ValidateSamlConfiguration(config);

        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.IdpEntityId.Should().Be("entity-id");
        result.IdpSsoUrl.Should().Be("https://idp.test/sso");
        result.CertificateExpiry.Should().NotBeNull();
    }

    // --- ValidateSamlConfiguration: no cert at all (lines 207-212 via null cert path) ---

    [Fact]
    public void ValidateSamlConfiguration_WithNoCert_ReturnsSuccessWithNullExpiry()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            TenantId.Create(Guid.NewGuid()), "Test", SsoProtocol.Saml,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);

        // Set EntityId and SsoUrl via reflection, leave cert null
        PropertyInfo? entityIdProp = typeof(SsoConfiguration).GetProperty("SamlEntityId");
        entityIdProp?.SetValue(config, "entity-id");
        PropertyInfo? ssoUrlProp = typeof(SsoConfiguration).GetProperty("SamlSsoUrl");
        ssoUrlProp?.SetValue(config, "https://idp.test/sso");

        SsoValidationResult result = KeycloakIdpService.ValidateSamlConfiguration(config);

        result.IsValid.Should().BeTrue();
        result.CertificateExpiry.Should().BeNull();
    }

    // --- TestSamlConnectionAsync: valid cert path (lines 129-130, 135, 141, 143) ---

    [Fact]
    public async Task TestSamlConnectionAsync_WithValidCert_ReturnsSuccess()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            TenantId.Create(Guid.NewGuid()), "Test", SsoProtocol.Saml,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);

        string validCertBase64 = GenerateSelfSignedCertBase64(DateTimeOffset.UtcNow.AddYears(1));

        config.UpdateSamlConfig("entity-id", "https://idp.test/sso", null, validCertBase64,
            SamlNameIdFormat.Email, Guid.Empty, TimeProvider.System);

        MockHttpHandler handler = new MockHttpHandler()
            .WithExternal("https://idp.test/sso", HttpStatusCode.OK);

        KeycloakIdpService service = CreateIdpService(handler);

        SsoTestResult result = await service.TestSamlConnectionAsync(config, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    // --- TestSamlConnectionAsync: expired cert (lines 131-133) ---

    [Fact]
    public async Task TestSamlConnectionAsync_WithExpiredCert_ReturnsFailure()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            TenantId.Create(Guid.NewGuid()), "Test", SsoProtocol.Saml,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);

        string expiredCertBase64 = GenerateSelfSignedCertBase64(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(-365));

        config.UpdateSamlConfig("entity-id", "https://idp.test/sso", null, expiredCertBase64,
            SamlNameIdFormat.Email, Guid.Empty, TimeProvider.System);

        MockHttpHandler handler = new MockHttpHandler()
            .WithExternal("https://idp.test/sso", HttpStatusCode.OK);

        KeycloakIdpService service = CreateIdpService(handler);

        SsoTestResult result = await service.TestSamlConnectionAsync(config, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Certificate expired");
    }

    // --- TestSamlConnectionAsync: no cert provided, success path (line 141, 143) ---

    [Fact]
    public async Task TestSamlConnectionAsync_WithNoCert_ReturnsSuccess()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            TenantId.Create(Guid.NewGuid()), "Test", SsoProtocol.Saml,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);

        // Set SAML fields but clear certificate via reflection
        config.UpdateSamlConfig("entity-id", "https://idp.test/sso", null, "placeholder",
            SamlNameIdFormat.Email, Guid.Empty, TimeProvider.System);
        PropertyInfo? certProp = typeof(SsoConfiguration).GetProperty("SamlCertificate");
        certProp?.SetValue(config, null);

        MockHttpHandler handler = new MockHttpHandler()
            .WithExternal("https://idp.test/sso", HttpStatusCode.OK);

        KeycloakIdpService service = CreateIdpService(handler);

        SsoTestResult result = await service.TestSamlConnectionAsync(config, CancellationToken.None);

        result.Success.Should().BeTrue();
    }

    private KeycloakIdpService CreateIdpService(HttpMessageHandler handler)
    {
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();

        HttpClient keycloakClient = new(handler)
        {
            BaseAddress = new Uri("https://keycloak.test/")
        };
        httpClientFactory.CreateClient("KeycloakAdminClient").Returns(keycloakClient);

        HttpClient externalClient = new(handler);
        httpClientFactory.CreateClient().Returns(externalClient);

        return new KeycloakIdpService(httpClientFactory, Options.Create(new KeycloakOptions()), _idpLogger);
    }

    private static string GenerateSelfSignedCertBase64(DateTimeOffset notAfter, DateTimeOffset? notBefore = null)
    {
        using RSA rsa = RSA.Create(2048);
        CertificateRequest req = new("CN=Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        DateTimeOffset startDate = notBefore ?? DateTimeOffset.UtcNow.AddDays(-1);
        X509Certificate2 cert = req.CreateSelfSigned(startDate, notAfter);
        return Convert.ToBase64String(cert.Export(X509ContentType.Cert));
    }

    private sealed class MockHttpHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode Status, object? Content)> _externalRoutes = new();

        public MockHttpHandler WithExternal(string url, HttpStatusCode status, object? content = null)
        {
            _externalRoutes[$"GET:{url}"] = (status, content);
            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string fullUrl = request.RequestUri?.ToString() ?? "";
            string externalKey = $"{request.Method}:{fullUrl}";

            if (_externalRoutes.TryGetValue(externalKey, out (HttpStatusCode Status, object? Content) externalRoute))
            {
                HttpResponseMessage response = new(externalRoute.Status);
                if (externalRoute.Content != null)
                {
                    response.Content = JsonContent.Create(externalRoute.Content);
                }
                return Task.FromResult(response);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
