using System.Text.Json;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// Checks that the seed admin explicitly declares global-admin status,
/// and that seeded assignable roles do not include a global-admin role.
/// </summary>
public sealed class SeedJsonGlobalAdminTests
{
    private const string GlobalAdminKey = "isGlobalAdmin";

    [Fact]
    public void SeedJson_BootstrapAdmin_DeclaresGlobalAdminExplicitly()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(SeedPath()));

        JsonElement admin = document.RootElement.GetProperty("admin");

        admin.TryGetProperty(GlobalAdminKey, out JsonElement flag).Should().BeTrue(
            $"the seeded bootstrap admin must declare \"{GlobalAdminKey}\" so global admin is provisioned, not inferred");
        flag.ValueKind.Should().Be(
            JsonValueKind.True,
            $"\"{GlobalAdminKey}\" must be a JSON boolean true, matching the seeded governance admin");
    }

    [Fact]
    public void SeedJson_Roles_ContainNoGlobalAdminRole()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(SeedPath()));

        IEnumerable<string> roles = document.RootElement.GetProperty("roles")
            .EnumerateArray()
            .Select(r => Normalize(r.GetString()));

        roles.Should().NotContain(
            "globaladmin",
            "global admin is a non-assignable claim; shipping it as a role would make it grantable through UsersController.AssignRole");
    }

    private static string Normalize(string? value) =>
        new string((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static string SeedPath()
    {
        string seedPath = Path.Combine(GetSolutionRoot(), "seed.json");
        File.Exists(seedPath).Should().BeTrue($"seed.json should exist at repo root ({seedPath})");
        return seedPath;
    }

    private static string GetSolutionRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Wallow.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Solution root not found");
    }
}
