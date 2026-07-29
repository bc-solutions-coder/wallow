namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// The OpenIddict server registration lives inside an options lambda in
/// IdentityInfrastructureExtensions that cannot be invoked without bootstrapping the whole
/// module, so the guard on DisableTransportSecurityRequirement is asserted at the source
/// level instead: the call has to be reached through
/// <see cref="Wallow.Identity.Infrastructure.Extensions.OpenIddictTransportSecurityPolicy"/>
/// rather than made unconditionally.
/// </summary>
public sealed class OpenIddictTransportSecurityCallSiteTests
{
    private const string DisableCall = "DisableTransportSecurityRequirement()";
    private const string PolicyCall =
        "OpenIddictTransportSecurityPolicy.ShouldDisableTransportSecurityRequirement";

    [Fact]
    public void IdentityInfrastructureExtensions_StillWiresTheTransportSecuritySwitch()
    {
        ReadExtensionsSource().Should().Contain(
            DisableCall,
            "the switch itself must stay wired so development and opted-in deployments keep working");
    }

    [Fact]
    public void IdentityInfrastructureExtensions_ReachesTheDisableCallThroughThePolicy()
    {
        string source = ReadExtensionsSource();

        int policyIndex = source.IndexOf(PolicyCall, StringComparison.Ordinal);
        int disableIndex = source.IndexOf(DisableCall, StringComparison.Ordinal);

        policyIndex.Should().BeGreaterThanOrEqualTo(0,
            $"the OpenIddict server setup must ask {PolicyCall} whether plain HTTP is allowed");
        disableIndex.Should().BeGreaterThan(policyIndex,
            "the environment check must guard the call, not follow it");
    }

    [Fact]
    public void IdentityInfrastructureExtensions_DoesNotCallDisableUnconditionally()
    {
        string[] lines = ReadExtensionsSource().Split('\n');

        int callLine = Array.FindIndex(lines, line => line.Contains(DisableCall, StringComparison.Ordinal));
        callLine.Should().BeGreaterThan(0, $"{DisableCall} should still be present");

        int windowStart = Math.Max(0, callLine - 5);
        string context = string.Join('\n', lines.Skip(windowStart).Take(callLine - windowStart + 1));

        context.Should().Contain(
            "if (",
            $"{DisableCall} must sit inside a conditional branch, not run on every startup");
    }

    private static string ReadExtensionsSource()
    {
        string path = Path.Combine(
            GetSolutionRoot(),
            "src", "Modules", "Identity", "Wallow.Identity.Infrastructure", "Extensions",
            "IdentityInfrastructureExtensions.cs");

        File.Exists(path).Should().BeTrue($"the OpenIddict server registration should exist at {path}");

        return File.ReadAllText(path);
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
