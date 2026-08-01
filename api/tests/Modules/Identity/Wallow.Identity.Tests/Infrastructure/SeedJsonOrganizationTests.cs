using System.Text.Json;
using System.Text.Json.Serialization;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Infrastructure.Options;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// The repo-root seed.json declares the organization every seeded client belongs to, and the terms
/// on which it admits people. A fork that runs the seeder unchanged gets these terms, so the shipped
/// default must not be one that admits anyone who can reach the sign-in page.
/// </summary>
public sealed class SeedJsonOrganizationTests
{
    private const string PlatformOrganization = "Wallow";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public void SeedJson_DeclaresThePlatformOrganization()
    {
        LoadSeededOrganizations().Select(o => o.Name).Should().Contain(PlatformOrganization);
    }

    [Fact]
    public void PlatformOrganization_DoesNotAdmitPeopleWithoutApproval()
    {
        SeedOrganizationDefinition organization = GetPlatformOrganization();

        organization.EnrollmentPolicy.Should().NotBe(
            EnrollmentPolicy.Open,
            "a fork running the shipped seed unchanged must not get a self-enrolling organization");
    }

    [Fact]
    public void PlatformOrganization_NamesSomewhereForAccessRequestsToLand()
    {
        SeedOrganizationDefinition organization = GetPlatformOrganization();

        if (organization.EnrollmentPolicy == EnrollmentPolicy.RequestApproval)
        {
            organization.AccessRequestEmail.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void EverySeededClientBelongsToADeclaredOrganization()
    {
        // A client whose tenantName names no declared organization still gets one created for it,
        // silently on the InviteOnly default — the case this seed section exists to make explicit.
        List<string> declared = LoadSeededOrganizations().Select(o => o.Name).ToList();

        foreach (string tenantName in LoadSeededClientTenantNames())
        {
            declared.Should().Contain(
                name => string.Equals(name, tenantName, StringComparison.OrdinalIgnoreCase),
                $"client tenantName '{tenantName}' must name a declared organization");
        }
    }

    private static SeedOrganizationDefinition GetPlatformOrganization()
    {
        SeedOrganizationDefinition? organization = LoadSeededOrganizations()
            .FirstOrDefault(o => string.Equals(o.Name, PlatformOrganization, StringComparison.OrdinalIgnoreCase));

        organization.Should().NotBeNull($"seed.json must declare the '{PlatformOrganization}' organization");
        return organization!;
    }

    private static List<SeedOrganizationDefinition> LoadSeededOrganizations()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(SeedFilePath()));

        return [.. document.RootElement.GetProperty("organizations").EnumerateArray()
            .Select(o => o.Deserialize<SeedOrganizationDefinition>(_jsonOptions))
            .OfType<SeedOrganizationDefinition>()];
    }

    private static List<string> LoadSeededClientTenantNames()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(SeedFilePath()));

        return [.. document.RootElement.GetProperty("clients").EnumerateArray()
            .Select(c => c.TryGetProperty("tenantName", out JsonElement name) ? name.GetString() : null)
            .OfType<string>()];
    }

    private static string SeedFilePath()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Wallow.slnx")))
        {
            directory = directory.Parent;
        }

        string root = directory?.FullName
            ?? throw new InvalidOperationException("Solution root not found");

        return Path.Combine(root, "seed.json");
    }
}
