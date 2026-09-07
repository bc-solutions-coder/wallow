namespace Wallow.Architecture.Tests;

public sealed class MigrationRemovalTests
{
    private static readonly string _solutionRoot = FindSolutionRoot();

    private static readonly string[] _moduleExtensionFiles =
    [
        "src/Modules/Identity/Wallow.Identity.Infrastructure/Extensions/IdentityModuleExtensions.cs",
        "src/Modules/Branding/Wallow.Branding.Infrastructure/Extensions/BrandingModuleExtensions.cs",
        "src/Modules/Notifications/Wallow.Notifications.Infrastructure/Extensions/NotificationsModuleExtensions.cs",
        "src/Modules/Storage/Wallow.Storage.Infrastructure/Extensions/StorageModuleExtensions.cs",
        "src/Modules/ApiKeys/Wallow.ApiKeys.Infrastructure/Extensions/ApiKeysModuleExtensions.cs",
        "src/Modules/Inquiries/Wallow.Inquiries.Infrastructure/Extensions/InquiriesModuleExtensions.cs",
    ];

    private static readonly string[] _auditingExtensionFiles =
    [
        "src/Shared/Wallow.Shared.Infrastructure.Core/Auditing/AuthAuditingExtensions.cs",
    ];



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
