using System.Text.Json;
using Wallow.Identity.Infrastructure.Options;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// First-party status is an explicit, seed-only property, never a client id prefix, and the
/// client ↔ organization invariant is enforced when the seed is read: a first-party client is
/// bound to no organization, every other client to exactly one. Seed members are how an
/// organization-bound client seeds its organization's roster, so they have no meaning on a
/// first-party client.
/// </summary>
public sealed class PreRegisteredClientInvariantTests
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void Validate_FirstPartyClientBoundToAnOrganization_ThrowsNamingTheClient()
    {
        PreRegisteredClientOptions options = new();
        options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "dashboard",
            DisplayName = "Dashboard",
            Secret = "s",
            FirstParty = true,
            TenantName = "Wallow"
        });

        Action validate = options.Validate;

        validate.Should().Throw<InvalidOperationException>().WithMessage("*dashboard*");
    }

    [Fact]
    public void Validate_FirstPartyClientBoundByTenantId_ThrowsNamingTheClient()
    {
        PreRegisteredClientOptions options = new();
        options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "dashboard",
            DisplayName = "Dashboard",
            Secret = "s",
            FirstParty = true,
            TenantId = Guid.NewGuid()
        });

        Action validate = options.Validate;

        validate.Should().Throw<InvalidOperationException>().WithMessage("*dashboard*");
    }

    [Fact]
    public void Validate_ThirdPartyClientWithoutAnOrganization_ThrowsNamingTheClient()
    {
        PreRegisteredClientOptions options = new();
        options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "partner-portal",
            DisplayName = "Partner Portal",
            Secret = "s"
        });

        Action validate = options.Validate;

        validate.Should().Throw<InvalidOperationException>().WithMessage("*partner-portal*");
    }

    [Fact]
    public void Validate_FirstPartyClientWithSeedMembers_ThrowsNamingTheClient()
    {
        PreRegisteredClientOptions options = new();
        options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "dashboard",
            DisplayName = "Dashboard",
            Secret = "s",
            FirstParty = true,
            SeedMembers = ["admin@wallow.dev"]
        });

        Action validate = options.Validate;

        validate.Should().Throw<InvalidOperationException>().WithMessage("*dashboard*");
    }

    [Fact]
    public void Validate_FirstPartyClientWithSeedMemberRoles_ThrowsNamingTheClient()
    {
        PreRegisteredClientOptions options = new();
        options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "dashboard",
            DisplayName = "Dashboard",
            Secret = "s",
            FirstParty = true,
            SeedMemberRoles = { ["admin@wallow.dev"] = "admin" }
        });

        Action validate = options.Validate;

        validate.Should().Throw<InvalidOperationException>().WithMessage("*dashboard*");
    }

    [Fact]
    public void Validate_OrgLessFirstPartyAndBoundThirdParty_DoesNotThrow()
    {
        PreRegisteredClientOptions options = new();
        options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "dashboard",
            DisplayName = "Dashboard",
            Secret = "s",
            FirstParty = true
        });
        options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "partner-portal",
            DisplayName = "Partner Portal",
            Secret = "s",
            TenantName = "Wallow",
            SeedMembers = ["member@example.com"]
        });
        options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "sa-worker",
            DisplayName = "Worker",
            Secret = "s",
            TenantId = Guid.NewGuid()
        });

        Action validate = options.Validate;

        validate.Should().NotThrow();
    }

    [Fact]
    public void Validate_ReportsEveryOffender_NotJustTheFirst()
    {
        PreRegisteredClientOptions options = new();
        options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "bound-first-party",
            DisplayName = "A",
            Secret = "s",
            FirstParty = true,
            TenantName = "Wallow"
        });
        options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "unbound-third-party",
            DisplayName = "B",
            Secret = "s"
        });

        Action validate = options.Validate;

        validate.Should().Throw<InvalidOperationException>()
            .WithMessage("*bound-first-party*")
            .WithMessage("*unbound-third-party*");
    }

    [Fact]
    public void FirstParty_BindsFromLowercaseJsonKey()
    {
        PreRegisteredClientDefinition? client = JsonSerializer.Deserialize<PreRegisteredClientDefinition>(
            """{"clientId":"dashboard","displayName":"Dashboard","firstParty":true}""",
            _jsonOptions);

        client.Should().NotBeNull();
        client!.FirstParty.Should().BeTrue("seed.json declares the flag as the lowercase \"firstParty\" key");
    }

    [Fact]
    public void FirstParty_AbsentFromJson_IsFalse()
    {
        PreRegisteredClientDefinition? client = JsonSerializer.Deserialize<PreRegisteredClientDefinition>(
            """{"clientId":"partner","displayName":"Partner"}""",
            _jsonOptions);

        client.Should().NotBeNull();
        client!.FirstParty.Should().BeFalse("a client is third-party unless the seed says otherwise");
    }
}
