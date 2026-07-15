using Microsoft.Playwright;
using Wallow.E2E.Tests.Fixtures;

namespace Wallow.E2E.Tests.Infrastructure;

[Trait("Category", "E2E")]
public sealed class E2ETestBaseTests : IAsyncLifetime
{
    private readonly PlaywrightFixture _playwright = new();
    private readonly DockerComposeFixture _docker = new();
    private TestableE2ETestBase? _sut;

    public async Task InitializeAsync()
    {
        await _playwright.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        if (_sut is not null)
        {
            await _sut.DisposeAsync();
        }

        await _playwright.DisposeAsync();
    }

    [Fact]
    public async Task InitializeAsync_SetsPageAndContext()
    {
        _sut = new TestableE2ETestBase(_docker, _playwright);

        await _sut.InitializeAsync();

        Assert.NotNull(_sut.ExposedPage);
        Assert.NotNull(_sut.ExposedContext);
    }
}

/// <summary>
/// Minimal concrete subclass of E2ETestBase for testing the base class behavior.
/// </summary>
internal sealed class TestableE2ETestBase : E2ETestBase
{
    public TestableE2ETestBase(DockerComposeFixture docker, PlaywrightFixture playwright)
        : base(docker, playwright)
    {
    }

    public IPage ExposedPage => Page;
    public IBrowserContext ExposedContext => Context;
}
