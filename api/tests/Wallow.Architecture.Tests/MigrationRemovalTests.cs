using System.Text.RegularExpressions;

namespace Wallow.Architecture.Tests;

public sealed class MigrationRemovalTests
{
    private static readonly string _solutionRoot = FindSolutionRoot();

    private static readonly string[] _moduleExtensionFiles =
    [
        "src/Modules/Identity/Wallow.Identity.Infrastructure/Extensions/IdentityModuleExtensions.cs",
        "src/Modules/Branding/Wallow.Branding.Infrastructure/Extensions/BrandingModuleExtensions.cs",
        "src/Modules/Notifications/Wallow.Notifications.Infrastructure/Extensions/NotificationsModuleExtensions.cs",
        "src/Modules/Announcements/Wallow.Announcements.Infrastructure/Extensions/AnnouncementsModuleExtensions.cs",
        "src/Modules/Storage/Wallow.Storage.Infrastructure/Extensions/StorageModuleExtensions.cs",
        "src/Modules/ApiKeys/Wallow.ApiKeys.Infrastructure/Extensions/ApiKeysModuleExtensions.cs",
        "src/Modules/Inquiries/Wallow.Inquiries.Infrastructure/Extensions/InquiriesModuleExtensions.cs",
    ];

    private static readonly string[] _auditingExtensionFiles =
    [
        "src/Shared/Wallow.Shared.Infrastructure.Core/Auditing/AuditingExtensions.cs",
        "src/Shared/Wallow.Shared.Infrastructure.Core/Auditing/AuthAuditingExtensions.cs",
    ];

    private static readonly Regex _initializeMethodPattern = new(
        @"(Initialize\w+(?:Module|Auditing)Async)\b",
        RegexOptions.Compiled);

    [Theory]
    [MemberData(nameof(GetModuleExtensionFiles))]
    public void ModuleInitializeAsync_ShouldNotContainMigrateAsync(string relativeFilePath)
    {
        string fullPath = Path.Combine(_solutionRoot, relativeFilePath);
        File.Exists(fullPath).Should().BeTrue($"expected file to exist: {fullPath}");

        string content = File.ReadAllText(fullPath);

        AssertNoMigrateAsyncInInitializeMethods(content, relativeFilePath);
    }

    [Theory]
    [MemberData(nameof(GetAuditingExtensionFiles))]
    public void AuditingInitializeAsync_ShouldNotContainMigrateAsync(string relativeFilePath)
    {
        string fullPath = Path.Combine(_solutionRoot, relativeFilePath);
        File.Exists(fullPath).Should().BeTrue($"expected file to exist: {fullPath}");

        string content = File.ReadAllText(fullPath);

        AssertNoMigrateAsyncInInitializeMethods(content, relativeFilePath);
    }

    [Fact]
    public void AllModuleExtensionFiles_ShouldExist()
    {
        foreach (string relativeFilePath in _moduleExtensionFiles)
        {
            string fullPath = Path.Combine(_solutionRoot, relativeFilePath);
            File.Exists(fullPath).Should().BeTrue($"expected module extension file to exist: {fullPath}");
        }
    }

    [Fact]
    public void AllAuditingExtensionFiles_ShouldExist()
    {
        foreach (string relativeFilePath in _auditingExtensionFiles)
        {
            string fullPath = Path.Combine(_solutionRoot, relativeFilePath);
            File.Exists(fullPath).Should().BeTrue($"expected auditing extension file to exist: {fullPath}");
        }
    }

    public static TheoryData<string> GetModuleExtensionFiles()
    {
        TheoryData<string> data = new();
        foreach (string file in _moduleExtensionFiles)
        {
            data.Add(file);
        }
        return data;
    }

    public static TheoryData<string> GetAuditingExtensionFiles()
    {
        TheoryData<string> data = new();
        foreach (string file in _auditingExtensionFiles)
        {
            data.Add(file);
        }
        return data;
    }

    private static void AssertNoMigrateAsyncInInitializeMethods(string fileContent, string filePath)
    {
        MatchCollection matches = _initializeMethodPattern.Matches(fileContent);
        matches.Count.Should().BeGreaterThan(0,
            $"expected to find at least one Initialize method in {filePath}");

        foreach (Match match in matches)
        {
            string methodName = match.Groups[1].Value;
            int methodStart = match.Index;

            string methodBody = ExtractMethodBody(fileContent, methodStart);
            methodBody.Should().NotBeEmpty($"expected to extract method body for {methodName} in {filePath}");

            bool containsMigrateAsync = methodBody.Contains("MigrateAsync", StringComparison.Ordinal);
            containsMigrateAsync.Should().BeFalse(
                $"method '{methodName}' in '{filePath}' should not contain MigrateAsync calls. " +
                "Migrations should be handled by the MigrationService, not in module initialization.");
        }
    }

    private static string ExtractMethodBody(string content, int startIndex)
    {
        int braceStart = content.IndexOf('{', startIndex);
        if (braceStart < 0)
        {
            return string.Empty;
        }

        int depth = 0;
        int position = braceStart;

        while (position < content.Length)
        {
            char c = content[position];
            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return content.Substring(braceStart, position - braceStart + 1);
                }
            }
            position++;
        }

        return string.Empty;
    }

    private static string FindSolutionRoot()
    {
        string? directory = AppDomain.CurrentDomain.BaseDirectory;
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory, "Wallow.slnx")))
            {
                return directory;
            }
            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new InvalidOperationException(
            "Could not find solution root (directory containing Wallow.slnx) " +
            $"starting from {AppDomain.CurrentDomain.BaseDirectory}");
    }
}
