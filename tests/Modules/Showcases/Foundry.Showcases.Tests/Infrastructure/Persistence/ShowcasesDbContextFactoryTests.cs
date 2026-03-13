using Foundry.Showcases.Infrastructure.Persistence;

namespace Foundry.Showcases.Tests.Infrastructure.Persistence;

public class ShowcasesDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_WithArgs_ReturnsConfiguredDbContext()
    {
        ShowcasesDbContextFactory factory = new();

        ShowcasesDbContext dbContext = factory.CreateDbContext(Array.Empty<string>());

        dbContext.Should().NotBeNull();
        dbContext.Dispose();
    }

    [Fact]
    public void CreateDbContext_UsesEnvironmentVariableForPassword()
    {
        string previousValue = Environment.GetEnvironmentVariable("FOUNDRY_DB_PASSWORD") ?? string.Empty;
        try
        {
            Environment.SetEnvironmentVariable("FOUNDRY_DB_PASSWORD", "custom-password");
            ShowcasesDbContextFactory factory = new();

            ShowcasesDbContext dbContext = factory.CreateDbContext(Array.Empty<string>());

            dbContext.Should().NotBeNull();
            dbContext.Dispose();
        }
        finally
        {
            Environment.SetEnvironmentVariable("FOUNDRY_DB_PASSWORD",
                string.IsNullOrEmpty(previousValue) ? null : previousValue);
        }
    }
}
