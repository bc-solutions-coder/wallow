namespace Foundry.Architecture.Tests;

internal static class TestConstants
{
    public static readonly string[] AllModules = Directory
        .GetFiles(AppDomain.CurrentDomain.BaseDirectory, "Foundry.*.Domain.dll")
        .Select(Path.GetFileNameWithoutExtension)
        .Where(name => name is not null)
        .Select(name => name!.Split('.')[1])
        .Distinct()
        .Order()
        .ToArray();

    public static readonly string[] EventSourcedModules =
    [
    ];
}
