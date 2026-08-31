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
        "src/Shared/Wallow.Shared.Infrastructure.Core/Auditing/AuthAuditingExtensions.cs",
    ];

    // ModuleInitializeAsync_ShouldNotContainMigrateAsync and
    // AuditingInitializeAsync_ShouldNotContainMigrateAsync were deleted here: both did
    // File.ReadAllText over a src/ path and regex-matched the method bodies for "MigrateAsync",
    // which .claude/rules/TESTING.md bans. The first had also lost its subject — the seven
    // Initialize{Module}ModuleAsync methods it parsed were no-ops and are gone with the
    // IWallowModule registry. Migrations belong to Wallow.MigrationService, and
    // Wallow.MigrationService.Tests is what asserts that.

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
