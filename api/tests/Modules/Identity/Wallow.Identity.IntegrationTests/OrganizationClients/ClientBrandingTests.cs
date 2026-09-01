using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Wallow.Shared.Infrastructure.Core.Auditing;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.OrganizationClients;

/// <summary>
/// The branding sub-resource on an organization's developer application: registration creates the
/// branding row through the integration event, the organization edits it over multipart PUT with
/// a validated logo, a foreign organization sees nothing, and every write lands an audit row.
/// Backend-dependent because the row is created by a Wolverine handler off the registration
/// request and the logo round-trips through the real (local) storage provider.
/// </summary>
[Trait("Category", "Integration")]
public class ClientBrandingTests(WallowApiFactory factory) : OrganizationClientsTestBase(factory)
{
    private static readonly string[] _redirects = ["https://portal.example.com/callback"];
    private static readonly string[] _openidScope = ["openid"];

    private const string ValidTheme =
        """{"light":{"primary":"#336699","primaryForeground":"#ffffff"},"dark":{"primary":"oklch(0.7 0.1 250)","primaryForeground":"#000000"}}""";

    [Fact]
    public async Task Registration_CreatesTheBrandingRow_DefaultingToTheClientName()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Branding Default Org");
        await ActAsEnrolledAsync(orgId, "manager");
        (string clientId, _) = await RegisterApplicationAsync(orgId, "Fresh Portal");

        JsonElement branding = await BrandingRowAsync(orgId, clientId);

        branding.GetProperty("displayName").GetString().Should().Be("Fresh Portal");
        branding.GetProperty("logoUrl").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Registration_WithChosenBranding_CreatesTheRowFromIt()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Branding Chosen Org");
        await ActAsEnrolledAsync(orgId, "manager");
        (string clientId, _) = await RegisterAsync(orgId, new
        {
            kind = "application",
            name = "Internal Name",
            redirectUris = _redirects,
            postLogoutRedirectUris = Array.Empty<string>(),
            scopes = _openidScope,
            branding = new { displayName = "Shiny Portal", tagline = "Sign in to shine" },
        });

        JsonElement branding = await BrandingRowAsync(orgId, clientId);

        branding.GetProperty("displayName").GetString().Should().Be("Shiny Portal");
        branding.GetProperty("tagline").GetString().Should().Be("Sign in to shine");
    }

    [Theory]
    [InlineData("Wallow")]
    [InlineData("  wallow  ")]
    public async Task Registration_RejectsTheForkNameAsBrandingDisplayName(string reserved)
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync($"Reserved Name Org {Guid.NewGuid():N}");
        await ActAsEnrolledAsync(orgId, "manager");

        HttpResponseMessage response = await Client.PostAsJsonAsync(
            $"/identity/organizations/{orgId}/clients",
            new
            {
                kind = "application",
                name = "Honest Name",
                redirectUris = _redirects,
                postLogoutRedirectUris = Array.Empty<string>(),
                scopes = _openidScope,
                branding = new { displayName = reserved },
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("reserved");
    }

    [Fact]
    public async Task Put_UpdatesBrandingAndLogo_AndLeavesTheLedgerNameAlone()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Branding Edit Org");
        await ActAsEnrolledAsync(orgId, "manager");
        (string clientId, _) = await RegisterApplicationAsync(orgId, "Ledger Name");

        HttpResponseMessage response = await PutBrandingAsync(
            orgId, clientId, "Customer Portal", "The friendly one", ValidTheme,
            logo: (PngBytes(), "image/png", "logo.png"));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        JsonElement branding = await response.Content.ReadFromJsonAsync<JsonElement>();
        branding.GetProperty("displayName").GetString().Should().Be("Customer Portal");
        branding.GetProperty("tagline").GetString().Should().Be("The friendly one");
        branding.GetProperty("themeJson").GetString().Should().Be(ValidTheme);
        branding.GetProperty("logoUrl").GetString().Should().NotBeNullOrWhiteSpace();

        JsonElement client = await GetClientAsync(orgId, clientId);
        client.GetProperty("name").GetString().Should().Be("Ledger Name");

        AuthAuditEntry audit = await AuditRowAsync("ClientBrandingUpdated", clientId);
        audit.ClientId.Should().Be(clientId);

        string? syncedName = await OpenIddictDisplayNameAsync(clientId, expected: "Customer Portal");
        syncedName.Should().Be(
            "Customer Portal",
            "the display-name sync should pull the branded name onto the OpenIddict application");
    }

    /// <summary>
    /// The display-name sync runs off the request through Wolverine; polls OpenIddict — a fresh
    /// scope per probe, so EF identity resolution cannot pin the pre-sync row — until the
    /// application carries the expected name, and hands back whatever it last observed.
    /// </summary>
    private async Task<string?> OpenIddictDisplayNameAsync(string clientId, string expected)
    {
        string? observed = null;
        await WaitForAsync(async () =>
        {
            using IServiceScope scope = Factory.Services.CreateScope();
            IOpenIddictApplicationManager applications =
                scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            object? application = await applications.FindByClientIdAsync(clientId);
            observed = application is null ? null : await applications.GetDisplayNameAsync(application);
            return string.Equals(observed, expected, StringComparison.Ordinal);
        });
        return observed;
    }

    [Theory]
    [InlineData("Wallow")]
    [InlineData("  WALLOW  ")]
    public async Task Put_RejectsTheForkNameAsDisplayName(string reserved)
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync($"Put Reserved Org {Guid.NewGuid():N}");
        await ActAsEnrolledAsync(orgId, "manager");
        (string clientId, _) = await RegisterApplicationAsync(orgId, "Honest App");

        HttpResponseMessage response = await PutBrandingAsync(orgId, clientId, reserved);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("reserved");
    }

    [Fact]
    public async Task Put_RejectsALogoWithAnUnsupportedContentType()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Gif Org");
        await ActAsEnrolledAsync(orgId, "manager");
        (string clientId, _) = await RegisterApplicationAsync(orgId, "Gif App");

        HttpResponseMessage response = await PutBrandingAsync(
            orgId, clientId, "Fine Name", logo: (PngBytes(), "image/gif", "logo.gif"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("logo");
    }

    [Fact]
    public async Task Put_RejectsALogoOverTwoMegabytes()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Oversize Org");
        await ActAsEnrolledAsync(orgId, "manager");
        (string clientId, _) = await RegisterApplicationAsync(orgId, "Oversize App");

        HttpResponseMessage response = await PutBrandingAsync(
            orgId, clientId, "Fine Name",
            logo: (PngBytes((2 * 1024 * 1024) + 1), "image/png", "logo.png"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("logo");
    }

    [Fact]
    public async Task Put_RejectsALogoWhoseBytesDoNotMatchItsContentType()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Magic Bytes Org");
        await ActAsEnrolledAsync(orgId, "manager");
        (string clientId, _) = await RegisterApplicationAsync(orgId, "Magic Bytes App");

        // JPEG bytes presented as PNG: the declared type and the magic bytes disagree.
        byte[] jpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46];
        HttpResponseMessage response = await PutBrandingAsync(
            orgId, clientId, "Fine Name", logo: (jpegBytes, "image/png", "logo.png"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("logo");
    }

    [Fact]
    public async Task ManagerOfAnotherOrganization_FindsNoBrandingToRead_OrWrite()
    {
        Guid ownerOrgId = await OrganizationOwnedBySomeoneElseAsync("Branding Owner Org");
        await ActAsEnrolledAsync(ownerOrgId, "manager");
        (string clientId, _) = await RegisterApplicationAsync(ownerOrgId, "Coveted App");
        await BrandingRowAsync(ownerOrgId, clientId);

        Guid foreignOrgId = await OrganizationOwnedBySomeoneElseAsync("Foreign Manager Org");
        await ActAsEnrolledAsync(foreignOrgId, "manager");

        HttpResponseMessage read = await Client.GetAsync(
            $"/identity/organizations/{ownerOrgId}/clients/{clientId}/branding");
        read.StatusCode.Should().Be(HttpStatusCode.NotFound);

        HttpResponseMessage write = await PutBrandingAsync(ownerOrgId, clientId, "Taken Over");
        write.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ServiceAccount_HasNoBrandingSubResource()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Service Account Org");
        await ActAsEnrolledAsync(orgId, "manager");
        (string clientId, _) = await RegisterServiceAccountAsync(orgId, "Robot");

        HttpResponseMessage response = await Client.GetAsync(
            $"/identity/organizations/{orgId}/clients/{clientId}/branding");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteLogo_ClearsTheLogo_AndKeepsTheRestOfTheBranding()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Logo Delete Org");
        await ActAsEnrolledAsync(orgId, "manager");
        (string clientId, _) = await RegisterApplicationAsync(orgId, "Logo App");

        HttpResponseMessage put = await PutBrandingAsync(
            orgId, clientId, "Logo Bearer", "Keeps its tagline",
            logo: (PngBytes(), "image/png", "logo.png"));
        put.StatusCode.Should().Be(HttpStatusCode.OK, await put.Content.ReadAsStringAsync());

        HttpResponseMessage delete = await Client.DeleteAsync(
            $"/identity/organizations/{orgId}/clients/{clientId}/branding/logo");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        JsonElement branding = await BrandingRowAsync(orgId, clientId);
        branding.GetProperty("logoUrl").ValueKind.Should().Be(JsonValueKind.Null);
        branding.GetProperty("displayName").GetString().Should().Be("Logo Bearer");
        branding.GetProperty("tagline").GetString().Should().Be("Keeps its tagline");
    }

    /// <summary>
    /// The branding row lands through the registration event's Wolverine handler, off the request;
    /// polls the sub-resource until it appears and hands the row back.
    /// </summary>
    private async Task<JsonElement> BrandingRowAsync(Guid orgId, string clientId)
    {
        string url = $"/identity/organizations/{orgId}/clients/{clientId}/branding";
        await WaitForAsync(async () =>
        {
            using HttpResponseMessage probe = await Client.GetAsync(url);
            return probe.StatusCode == HttpStatusCode.OK;
        });

        HttpResponseMessage response = await Client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK, "the registration event should have created the branding row");
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<HttpResponseMessage> PutBrandingAsync(
        Guid orgId,
        string clientId,
        string displayName,
        string? tagline = null,
        string? themeJson = null,
        (byte[] Bytes, string ContentType, string FileName)? logo = null)
    {
        // MultipartFormDataContent takes ownership of its parts, but CA2000 cannot see that;
        // the using declarations dispose after the awaited send, and double-dispose is harmless.
        using MultipartFormDataContent form = new();
        using StringContent displayNameContent = new(displayName);
        form.Add(displayNameContent, "DisplayName");

        using StringContent? taglineContent = tagline is null ? null : new StringContent(tagline);
        if (taglineContent is not null)
        {
            form.Add(taglineContent, "Tagline");
        }

        using StringContent? themeContent = themeJson is null ? null : new StringContent(themeJson);
        if (themeContent is not null)
        {
            form.Add(themeContent, "ThemeJson");
        }

        using ByteArrayContent? logoContent = logo is null ? null : new ByteArrayContent(logo.Value.Bytes);
        if (logo is not null && logoContent is not null)
        {
            logoContent.Headers.ContentType = new MediaTypeHeaderValue(logo.Value.ContentType);
            form.Add(logoContent, "logo", logo.Value.FileName);
        }

        return await Client.PutAsync(
            $"/identity/organizations/{orgId}/clients/{clientId}/branding", form);
    }

    private static byte[] PngBytes(int length = 64)
    {
        byte[] bytes = new byte[length];
        bytes[0] = 0x89;
        bytes[1] = 0x50;
        bytes[2] = 0x4E;
        bytes[3] = 0x47;
        return bytes;
    }
}
