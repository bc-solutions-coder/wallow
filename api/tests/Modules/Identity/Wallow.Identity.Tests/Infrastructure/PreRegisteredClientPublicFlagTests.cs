using System.Text.Json;
using Wallow.Identity.Infrastructure.Options;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// Missing secrets must fail validation unless the client explicitly declares itself public.
/// This prevents an unset secret from changing the client authentication requirement.
/// </summary>
public sealed class PreRegisteredClientPublicFlagTests
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void IsPublic_SecretlessWithoutExplicitFlag_IsNotPublic()
    {
        PreRegisteredClientDefinition client = new() { ClientId = "no-secret", DisplayName = "No Secret" };

        client.IsPublic.Should().BeFalse("a missing secret must never by itself make a client public");
    }

    [Fact]
    public void IsPublic_ExplicitPublicTrue_IsPublic()
    {
        PreRegisteredClientDefinition client = new() { ClientId = "spa", DisplayName = "SPA", Public = true };

        client.IsPublic.Should().BeTrue();
    }

    [Fact]
    public void IsPublic_ExplicitPublicFalseWithoutSecret_IsNotPublic()
    {
        PreRegisteredClientDefinition client = new() { ClientId = "broken", DisplayName = "Broken", Public = false };

        client.IsPublic.Should().BeFalse("an explicit public:false declaration wins over secret absence");
    }

    [Fact]
    public void IsPublic_ExplicitPublicTrueWithSecret_IsPublic()
    {
        PreRegisteredClientDefinition client = new()
        {
            ClientId = "declared-public",
            DisplayName = "Declared Public",
            Secret = "s",
            Public = true
        };

        client.IsPublic.Should().BeTrue("the explicit declaration is the source of truth, not secret presence");
    }

    [Fact]
    public void Validate_SecretlessClientWithoutExplicitFlag_Throws()
    {
        PreRegisteredClientOptions options = new();
        options.Clients.Add(new PreRegisteredClientDefinition { ClientId = "silent-public", DisplayName = "Silent" });

        Action validate = options.Validate;

        validate.Should().Throw<InvalidOperationException>()
            .WithMessage("*silent-public*", "the hard-fail must name the offending client");
    }

    [Fact]
    public void Validate_SecretlessClientDeclaredNotPublic_Throws()
    {
        PreRegisteredClientOptions options = new();
        options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "confidential-no-secret",
            DisplayName = "Confidential",
            Public = false
        });

        Action validate = options.Validate;

        validate.Should().Throw<InvalidOperationException>()
            .WithMessage("*confidential-no-secret*",
                "a confidential client with no secret is the same fail-open hazard");
    }

    [Fact]
    public void Validate_SecretlessClientDeclaredPublic_DoesNotThrow()
    {
        PreRegisteredClientOptions options = new();
        options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "spa",
            DisplayName = "SPA",
            Public = true,
            TenantName = "Wallow"
        });

        Action validate = options.Validate;

        validate.Should().NotThrow();
    }

    [Fact]
    public void Validate_ConfidentialClientWithSecret_DoesNotThrow()
    {
        PreRegisteredClientOptions options = new();
        options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "bff",
            DisplayName = "BFF",
            Secret = "s",
            TenantName = "Wallow"
        });

        Action validate = options.Validate;

        validate.Should().NotThrow();
    }

    [Fact]
    public void Validate_WhitespaceSecretWithoutExplicitFlag_Throws()
    {
        PreRegisteredClientOptions options = new();
        options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "blank-secret",
            DisplayName = "Blank Secret",
            Secret = "   "
        });

        Action validate = options.Validate;

        validate.Should().Throw<InvalidOperationException>(
            "a whitespace-only secret is an unset secret");
    }

    [Fact]
    public void Validate_OffendingClientAmongValidOnes_Throws()
    {
        PreRegisteredClientOptions options = new();
        options.Clients.Add(new PreRegisteredClientDefinition { ClientId = "ok", DisplayName = "Ok", Secret = "s" });
        options.Clients.Add(new PreRegisteredClientDefinition { ClientId = "leaky", DisplayName = "Leaky" });
        options.Clients.Add(new PreRegisteredClientDefinition { ClientId = "spa", DisplayName = "SPA", Public = true });

        Action validate = options.Validate;

        validate.Should().Throw<InvalidOperationException>().WithMessage("*leaky*");
    }

    [Fact]
    public void Validate_NoClients_DoesNotThrow()
    {
        PreRegisteredClientOptions options = new();

        Action validate = options.Validate;

        validate.Should().NotThrow();
    }

    [Fact]
    public void PublicFlag_BindsFromLowercaseJsonKey()
    {
        PreRegisteredClientDefinition? client = JsonSerializer.Deserialize<PreRegisteredClientDefinition>(
            """{"clientId":"spa","displayName":"SPA","public":true}""",
            _jsonOptions);

        client.Should().NotBeNull();
        client!.Public.Should().BeTrue("seed.json declares the flag as the lowercase \"public\" key");
    }

    [Fact]
    public void PublicFlag_AbsentFromJson_IsNull()
    {
        PreRegisteredClientDefinition? client = JsonSerializer.Deserialize<PreRegisteredClientDefinition>(
            """{"clientId":"legacy","displayName":"Legacy"}""",
            _jsonOptions);

        client.Should().NotBeNull();
        client!.Public.Should().BeNull("an undeclared flag must be distinguishable from an explicit false");
    }
}
